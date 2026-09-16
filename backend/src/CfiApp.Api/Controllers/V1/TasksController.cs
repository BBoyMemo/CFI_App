using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Work;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Work;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Maintenance tasks: only the Maintenance Manager creates and assigns them, an engineer
/// only ever marks their own assignment done. Visibility follows one rule - an open task
/// (nobody has completed it yet) is visible only to whoever it is assigned to; once
/// anyone completes it, it becomes visible to every engineer and manager, which is what
/// turns it into shared history rather than a private to-do.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tasks")]
[Authorize]
public sealed class TasksController(
    CfiAppDbContext context,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.TaskManage)]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskDetailDto>> Create(CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var assigneeIds = request.AssignedUserIds.Distinct().ToArray();
        var knownUserCount = await context.Users.CountAsync(x => assigneeIds.Contains(x.Id), cancellationToken);

        if (knownUserCount != assigneeIds.Length)
        {
            return Problem(title: "One or more assignees are unknown", statusCode: StatusCodes.Status400BadRequest);
        }

        var task = new MaintenanceTask
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Kind = request.Kind,
            ScheduledDate = request.ScheduledDate,
            Priority = request.Priority
        };

        foreach (var userId in assigneeIds)
        {
            task.Assignments.Add(new TaskAssignment { UserId = userId });
        }

        context.MaintenanceTasks.Add(task);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Detail), new { id = task.Id }, await LoadDetailAsync(task.Id, cancellationToken));
    }

    /// <summary>Everything assigned to the signed-in engineer, open or already completed by them.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.TaskViewAssigned)]
    [ProducesResponseType(typeof(PagedResult<TaskSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<TaskSummaryDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;

        var query = context.MaintenanceTasks.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.Assignments.Any(a => a.UserId == userId));

        return ListAsync(query, page, pageSize, cancellationToken);
    }

    /// <summary>Every task anyone has finished - visible to the whole maintenance team, not just the assignee.</summary>
    [HttpGet("completed")]
    [Authorize(Policy = Permissions.TaskViewAssigned)]
    [ProducesResponseType(typeof(PagedResult<TaskSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<TaskSummaryDto>>> Completed(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var query = context.MaintenanceTasks.AsNoTracking()
            .Where(x => x.DeletedAt == null && x.Completions.Any());

        return ListAsync(query, page, pageSize, cancellationToken);
    }

    /// <summary>The manager's full board - every open and completed task, undeleted.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.TaskManage)]
    [ProducesResponseType(typeof(PagedResult<TaskSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<TaskSummaryDto>>> List(
        [FromQuery] TaskKind? kind = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = context.MaintenanceTasks.AsNoTracking().Where(x => x.DeletedAt == null);

        if (kind is not null) query = query.Where(x => x.Kind == kind);

        return ListAsync(query, page, pageSize, cancellationToken);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TaskDetailDto>> Detail(int id, CancellationToken cancellationToken)
    {
        var task = await context.MaintenanceTasks.AsNoTracking()
            .Include(x => x.Assignments)
            .Include(x => x.Completions)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (task is null) return NotFound();

        var userId = currentUser.UserId!.Value;
        var canManage = User.HasClaim(CfiApp.Api.Security.CfiClaimTypes.Permission, Permissions.TaskManage);
        var isAssignee = task.Assignments.Any(a => a.UserId == userId);
        var isCompleted = task.Completions.Count > 0;

        if (!canManage && !isAssignee && !isCompleted)
        {
            return Problem(
                title: "This task is not visible to you yet",
                detail: "Open tasks are only visible to the people they are assigned to.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPut("{id:int}/assignees")]
    [Authorize(Policy = Permissions.TaskManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reassign(int id, ReassignTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await context.MaintenanceTasks
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (task is null) return NotFound();

        var assigneeIds = request.AssignedUserIds.Distinct().ToArray();
        var knownUserCount = await context.Users.CountAsync(x => assigneeIds.Contains(x.Id), cancellationToken);

        if (knownUserCount != assigneeIds.Length)
        {
            return Problem(title: "One or more assignees are unknown", statusCode: StatusCodes.Status400BadRequest);
        }

        task.Assignments.Clear();
        foreach (var userId in assigneeIds)
        {
            task.Assignments.Add(new TaskAssignment { UserId = userId });
        }

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Not a hard delete - the row stays for anyone who already completed it, just off the manager's active board.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.TaskManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var task = await context.MaintenanceTasks.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);
        if (task is null) return NotFound();

        task.DeletedAt = clock.UtcNow;
        task.DeletedByUserId = currentUser.UserId;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>A short note, an optional photo, done - no notification goes to the manager, they check the board.</summary>
    [HttpPost("{id:int}/complete")]
    [Authorize(Policy = Permissions.TaskComplete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(int id, CompleteTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await context.MaintenanceTasks
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (task is null) return NotFound();

        var userId = currentUser.UserId!.Value;

        if (task.Assignments.All(a => a.UserId != userId))
        {
            return Problem(
                title: "You are not assigned to this task",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (request.PhotoAssetId is { } photoAssetId &&
            !await context.MediaAssets.AnyAsync(x => x.Id == photoAssetId, cancellationToken))
        {
            return Problem(title: "Photo not found", statusCode: StatusCodes.Status400BadRequest);
        }

        var completion = new TaskCompletion
        {
            MaintenanceTaskId = id,
            UserId = userId,
            Note = request.Note.Trim(),
            CompletedAt = clock.UtcNow
        };

        if (request.PhotoAssetId is { } assetId)
        {
            completion.Photos.Add(new TaskCompletionPhoto { MediaAssetId = assetId });
        }

        context.TaskCompletions.Add(completion);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ---------------------------------------------------------------- queries

    private async Task<ActionResult<PagedResult<TaskSummaryDto>>> ListAsync(
        IQueryable<MaintenanceTask> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        query = query.OrderByDescending(x => x.ScheduledDate);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new TaskSummaryDto(
                x.Id, x.Title, x.Kind, x.ScheduledDate, x.Priority,
                x.Assignments.Select(a => new TaskAssigneeDto(a.UserId, a.User!.FullName)).ToList(),
                x.Completions.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskSummaryDto>(items, total, page, pageSize);
    }

    private async Task<TaskDetailDto> LoadDetailAsync(int id, CancellationToken cancellationToken)
    {
        var task = await context.MaintenanceTasks.AsNoTracking()
            .Include(x => x.Assignments).ThenInclude(a => a.User)
            .Include(x => x.Completions).ThenInclude(c => c.User)
            .Include(x => x.Completions).ThenInclude(c => c.Photos)
            .FirstAsync(x => x.Id == id, cancellationToken);

        return new TaskDetailDto(
            task.Id, task.Title, task.Description, task.Kind, task.ScheduledDate, task.Priority,
            [.. task.Assignments.Select(a => new TaskAssigneeDto(a.UserId, a.User!.FullName))],
            [.. task.Completions.Select(c => new TaskCompletionDto(
                c.UserId, c.User!.FullName, c.Note, c.CompletedAt, c.Photos.Count > 0))]);
    }
}
