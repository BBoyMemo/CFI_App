using DailyTasks.Api.Auth;
using DailyTasks.Api.Common;
using DailyTasks.Api.Contracts;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using DailyTasks.Api.Localization;
using DailyTasks.Api.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public class TasksController(AppDbContext db, AppClock clock, PhotoService photos, TranslationQueue translations)
    : ControllerBase
{
    // Managers see every task of the day; engineers only the ones assigned to them.
    [HttpGet]
    public async Task<IReadOnlyList<TaskDto>> List([FromQuery] DateOnly? date, CancellationToken ct)
    {
        await CarryOverAsync(ct);

        var day = date ?? clock.Today;
        var query = WithDetails().Where(t => t.Date == day);
        if (!User.IsManager())
        {
            var me = User.UserId();
            query = query.Where(t => t.Assignees.Any(a => a.UserId == me));
        }

        var tasks = await query.ToListAsync(ct);
        return await ToDtosAsync(tasks
            .OrderBy(t => t.Shift)
            .ThenBy(t => t.IsCompleted)
            .ThenByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToList(), ct);
    }

    // Completed tasks, most recently finished first. Everyone sees all of them: finished work is
    // shared knowledge for the team, while open tasks stay visible only to their assignees.
    // Optional filters: q = text in the title, description, completion comment (original or any
    // translation) or a person's name (who completed it or any assignee), case-insensitive;
    // date = the factory-local completion day.
    [HttpGet("history")]
    public async Task<PagedResult<TaskDto>> History(
        [FromQuery] string? q, [FromQuery] DateOnly? date,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, Limits.MaxPageSize);

        var query = db.Tasks.AsNoTracking().Where(t => t.CompletedAt != null);

        var text = q?.Trim().ToLower();
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Length > Limits.TitleMax) text = text[..Limits.TitleMax];
            query = query.Where(t =>
                t.Title.ToLower().Contains(text) ||
                (t.Description != null && t.Description.ToLower().Contains(text)) ||
                (t.CompletionComment != null && t.CompletionComment.ToLower().Contains(text)) ||
                (t.CompletedBy != null && t.CompletedBy.Name.ToLower().Contains(text)) ||
                t.Assignees.Any(a => a.User.Name.ToLower().Contains(text)) ||
                db.Translations.Any(x => x.EntityType == TranslatedEntity.Task && x.EntityId == t.Id &&
                                         x.Text.ToLower().Contains(text)));
        }

        if (date is not null)
        {
            var (from, to) = clock.DayBounds(date.Value);
            query = query.Where(t => t.CompletedAt >= from && t.CompletedAt < to);
        }

        var total = await query.CountAsync(ct);
        var ids = await query
            .OrderByDescending(t => t.CompletedAt)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.Id)
            .ToListAsync(ct);
        var tasks = await WithDetails().Where(t => ids.Contains(t.Id)).ToListAsync(ct);

        var items = await ToDtosAsync(tasks.OrderBy(t => ids.IndexOf(t.Id)).ToList(), ct);
        return new PagedResult<TaskDto>(items, total, page, pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDto>> Get(Guid id, CancellationToken ct)
    {
        await CarryOverAsync(ct);
        var task = await WithDetails().SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (!CanSee(task)) return ApiErrors.Forbidden();
        return await ToDtoAsync(task, ct);
    }

    [HttpPost]
    [Authorize(Policy = Policies.Manager)]
    public async Task<ActionResult<TaskDto>> Create(TaskRequest request, CancellationToken ct)
    {
        var (error, assignees) = await ValidateAsync(request, ct);
        if (error is not null) return error;

        var now = clock.UtcNow;
        var task = new DailyTask
        {
            Id = Guid.NewGuid(),
            CreatedById = User.UserId(),
            CreatedAt = now,
            OriginalDate = request.Date!.Value,
        };
        Apply(task, request, assignees, now);
        db.Tasks.Add(task);
        await translations.AddAsync(TranslatedEntity.Task, task.Id, ct);
        await db.SaveChangesAsync(ct);

        var created = await WithDetails().SingleAsync(t => t.Id == task.Id, ct);
        return StatusCode(StatusCodes.Status201Created, await ToDtoAsync(created, ct));
    }

    // Editing covers moving a task to another date. Completed tasks are a record and stay as they are.
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<ActionResult<TaskDto>> Update(Guid id, TaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Assignees).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (task.IsCompleted) return ApiErrors.Conflict("task.completed", "A completed task cannot be changed.");

        var (error, assignees) = await ValidateAsync(request, ct);
        if (error is not null) return error;

        // A deliberate move resets the carry-over history: the new date is the plan now.
        if (request.Date!.Value != task.Date) task.OriginalDate = request.Date.Value;
        Apply(task, request, assignees, clock.UtcNow);
        await translations.AddAsync(TranslatedEntity.Task, task.Id, ct);
        await db.SaveChangesAsync(ct);

        var updated = await WithDetails().SingleAsync(t => t.Id == id, ct);
        return await ToDtoAsync(updated, ct);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Photos).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        // Completed work is the team's record (History); it stays.
        if (task.IsCompleted) return ApiErrors.Conflict("task.completed", "A completed task cannot be deleted.");

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        await translations.RemoveAllForAsync(id, ct);
        photos.Delete(task.CompletionPhotoKey);
        foreach (var photo in task.Photos) photos.Delete(photo.PhotoKey);
        return NoContent();
    }

    // Photos the manager attaches while planning: one per request, up to Limits.MaxTaskPhotos.
    // Like the rest of the task they are fixed once it is completed.
    [HttpPost("{id:guid}/photos")]
    [Authorize(Policy = Policies.Manager)]
    [RequestSizeLimit(Limits.UploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Limits.UploadRequestBytes)]
    public async Task<ActionResult<TaskDto>> AddPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.Photos).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (task.IsCompleted) return ApiErrors.Conflict("task.completed", "A completed task cannot be changed.");
        if (photo is null) return new Validation().Add("photo", "required").ToResult();
        if (task.Photos.Count(p => p.Kind == TaskPhotoKind.Plan) >= Limits.MaxTaskPhotos)
            return new Validation().Add("photo", "photo.tooMany").ToResult();

        var (key, error) = await photos.SaveAsync(photo, "tasks", ct);
        if (error is not null) return new Validation().Add("photo", error).ToResult();

        db.TaskPhotos.Add(new TaskPhoto
        {
            Id = Guid.NewGuid(),
            TaskId = id,
            Kind = TaskPhotoKind.Plan,
            PhotoKey = key!,
            CreatedById = User.UserId(),
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        var updated = await WithDetails().SingleAsync(t => t.Id == id, ct);
        return await ToDtoAsync(updated, ct);
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<ActionResult<TaskDto>> DeletePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (task.IsCompleted) return ApiErrors.Conflict("task.completed", "A completed task cannot be changed.");
        var photo = await db.TaskPhotos.SingleOrDefaultAsync(
            p => p.Id == photoId && p.TaskId == id && p.Kind == TaskPhotoKind.Plan, ct);
        if (photo is null) return ApiErrors.NotFound("photo.notFound");

        db.TaskPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        photos.Delete(photo.PhotoKey);

        var updated = await WithDetails().SingleAsync(t => t.Id == id, ct);
        return await ToDtoAsync(updated, ct);
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> TaskPhotoFile(Guid id, Guid photoId, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().Include(t => t.Assignees).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (!CanSee(task)) return ApiErrors.Forbidden();
        var key = await db.TaskPhotos.Where(p => p.Id == photoId && p.TaskId == id).Select(p => p.PhotoKey)
            .SingleOrDefaultAsync(ct);
        if (key is null) return ApiErrors.NotFound("photo.notFound");

        var file = photos.Open(key);
        return file is null ? ApiErrors.NotFound("photo.notFound") : File(file.Value.Content, file.Value.ContentType);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = Policies.Engineer)]
    [RequestSizeLimit(Limits.MultiPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Limits.MultiPhotoRequestBytes)]
    public async Task<ActionResult<TaskDto>> Complete(Guid id, [FromForm] string? comment, CancellationToken ct)
    {
        var me = User.UserId();
        var task = await db.Tasks.AsNoTracking().Include(t => t.Assignees).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (task.Assignees.All(a => a.UserId != me)) return ApiErrors.Forbidden("task.notAssigned");
        if (task.IsCompleted) return ApiErrors.Conflict("task.alreadyCompleted", "The task is already completed.");

        var validation = new Validation().Text("comment", comment, required: false, max: Limits.TextMax);
        if (!validation.IsValid) return validation.ToResult();

        // Up to Limits.MaxTaskPhotos files, each sent as a "photo" form field.
        var files = Request.HasFormContentType ? Request.Form.Files.GetFiles("photo") : [];
        if (files.Count > Limits.MaxTaskPhotos) return new Validation().Add("photo", "photo.tooMany").ToResult();
        var savedKeys = new List<string>();
        foreach (var file in files)
        {
            var (key, photoError) = await photos.SaveAsync(file, "tasks", ct);
            if (photoError is not null)
            {
                savedKeys.ForEach(photos.Delete);
                return new Validation().Add("photo", photoError).ToResult();
            }
            savedKeys.Add(key!);
        }

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        var now = clock.UtcNow;

        // Conditional update: if two assignees finish at the same moment, exactly one wins.
        var updated = await db.Tasks
            .Where(t => t.Id == id && t.CompletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CompletedAt, now)
                .SetProperty(t => t.CompletedById, me)
                .SetProperty(t => t.CompletionComment, trimmed)
                .SetProperty(t => t.UpdatedAt, now), ct);
        if (updated == 0)
        {
            savedKeys.ForEach(photos.Delete);
            return ApiErrors.Conflict("task.alreadyCompleted", "The task is already completed.");
        }

        for (var i = 0; i < savedKeys.Count; i++)
        {
            db.TaskPhotos.Add(new TaskPhoto
            {
                Id = Guid.NewGuid(),
                TaskId = id,
                Kind = TaskPhotoKind.Completion,
                PhotoKey = savedKeys[i],
                CreatedById = me,
                // Keeps the order they were picked in.
                CreatedAt = now.AddTicks(i),
            });
        }
        if (trimmed is not null) await translations.AddAsync(TranslatedEntity.Task, id, ct);
        await db.SaveChangesAsync(ct);

        var completed = await WithDetails().SingleAsync(t => t.Id == id, ct);
        return await ToDtoAsync(completed, ct);
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var task = await db.Tasks.AsNoTracking().Include(t => t.Assignees).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null) return ApiErrors.NotFound();
        if (!CanSee(task)) return ApiErrors.Forbidden();
        if (task.CompletionPhotoKey is null) return ApiErrors.NotFound("photo.notFound");

        var file = photos.Open(task.CompletionPhotoKey);
        return file is null ? ApiErrors.NotFound("photo.notFound") : File(file.Value.Content, file.Value.ContentType);
    }

    // Unfinished tasks from earlier days move to today. Runs before every read, so the board is
    // always correct without a scheduler; it is a single idempotent UPDATE that touches nothing
    // once the day's carry-over is done.
    private async Task CarryOverAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var now = clock.UtcNow;
        await db.Tasks
            .Where(t => t.CompletedAt == null && t.Date < today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Date, today)
                .SetProperty(t => t.UpdatedAt, now), ct);
    }

    private async Task<(ActionResult? Error, List<Guid> Assignees)> ValidateAsync(TaskRequest r, CancellationToken ct)
    {
        var v = new Validation()
            .Text("title", r.Title, required: true, max: Limits.TitleMax)
            .Text("description", r.Description, required: false, max: Limits.TextMax);

        if (r.Date is null) v.Add("date", "required");
        else if (r.Date.Value < clock.Today) v.Add("date", "inPast");
        if (r.Priority is null || !Enum.IsDefined(r.Priority.Value)) v.Add("priority", "required");
        if (r.Shift is null || !Enum.IsDefined(r.Shift.Value)) v.Add("shift", "required");

        var ids = (r.AssigneeIds ?? []).Distinct().ToList();
        if (ids.Count == 0)
        {
            v.Add("assigneeIds", "required");
        }
        else
        {
            var engineers = await db.Users
                .CountAsync(u => ids.Contains(u.Id) && u.Role == Role.Engineer, ct);
            if (engineers != ids.Count) v.Add("assigneeIds", "notEngineer");
        }

        return (v.IsValid ? null : v.ToResult(), ids);
    }

    private static void Apply(DailyTask task, TaskRequest r, List<Guid> assignees, DateTimeOffset now)
    {
        task.Title = r.Title!.Trim();
        task.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        task.Date = r.Date!.Value;
        task.Priority = r.Priority!.Value;
        task.Shift = r.Shift!.Value;
        task.UpdatedAt = now;

        task.Assignees.RemoveAll(a => !assignees.Contains(a.UserId));
        foreach (var userId in assignees.Where(id => task.Assignees.All(a => a.UserId != id)))
            task.Assignees.Add(new DailyTaskAssignee { TaskId = task.Id, UserId = userId });
    }

    private bool CanSee(DailyTask task) =>
        User.IsManager() || task.IsCompleted || task.Assignees.Any(a => a.UserId == User.UserId());

    private IQueryable<DailyTask> WithDetails() =>
        db.Tasks.AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Include(t => t.CompletedBy)
            .Include(t => t.Photos);

    private async Task<TaskDto> ToDtoAsync(DailyTask task, CancellationToken ct) =>
        (await ToDtosAsync([task], ct))[0];

    private async Task<List<TaskDto>> ToDtosAsync(IReadOnlyList<DailyTask> tasks, CancellationToken ct)
    {
        var set = await TranslationSet.LoadAsync(db, tasks.Select(t => t.Id).ToList(), ct);
        return tasks.Select(t => ToDto(t, set)).ToList();
    }

    private static List<Guid> PhotoIdsOf(DailyTask t, TaskPhotoKind kind) =>
        t.Photos.Where(p => p.Kind == kind).OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Select(p => p.Id).ToList();

    private static TaskDto ToDto(DailyTask t, TranslationSet set) => new(
        t.Id,
        t.Title,
        t.Description,
        t.Date,
        t.OriginalDate,
        t.Priority,
        t.Shift,
        t.Assignees.Select(a => new UserRef(a.UserId, a.User.Name)).OrderBy(u => u.Name).ToList(),
        t.CreatedAt,
        t.IsCompleted,
        t.CompletedAt,
        t.CompletedBy is null ? null : new UserRef(t.CompletedBy.Id, t.CompletedBy.Name),
        t.CompletionComment,
        t.CompletionPhotoKey is not null,
        PhotoIdsOf(t, TaskPhotoKind.Plan),
        PhotoIdsOf(t, TaskPhotoKind.Completion),
        set.For(t.Id,
            (TranslatedField.Title, t.Title),
            (TranslatedField.Description, t.Description),
            (TranslatedField.Comment, t.CompletionComment)));
}
