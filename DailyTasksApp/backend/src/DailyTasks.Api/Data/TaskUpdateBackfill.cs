using DailyTasks.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Data;

// Tasks completed before update cards existed kept their completion on the task itself (who,
// when, comment, one photo; later several photos). This turns that into the task's first card so
// every task reads the same way. Safe to run on every start: it only touches tasks that are
// completed and have no cards yet.
public static class TaskUpdateBackfill
{
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        var tasks = await db.Tasks
            .Include(t => t.Photos)
            .Where(t => t.CompletedAt != null && !db.TaskUpdates.Any(u => u.TaskId == t.Id))
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            var author = task.CompletedById ?? task.CreatedById;
            var update = new TaskUpdate
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Outcome = WorkStatus.Completed,
                AuthorId = author,
                CreatedAt = task.CompletedAt!.Value,
                Comment = string.IsNullOrWhiteSpace(task.CompletionComment) ? null : task.CompletionComment,
            };
            db.TaskUpdates.Add(update);

            foreach (var photo in task.Photos.Where(p => p.Kind == TaskPhotoKind.Completion && p.UpdateId == null))
                photo.UpdateId = update.Id;

            if (task.CompletionPhotoKey is not null)
            {
                db.TaskPhotos.Add(new TaskPhoto
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UpdateId = update.Id,
                    Kind = TaskPhotoKind.Completion,
                    PhotoKey = task.CompletionPhotoKey,
                    CreatedById = author,
                    CreatedAt = task.CompletedAt.Value.AddTicks(-1),
                });
            }

            // The comment's translations now belong to the card.
            await db.Translations
                .Where(x => x.EntityType == TranslatedEntity.Task && x.EntityId == task.Id &&
                            x.Field == TranslatedField.Comment)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.EntityType, TranslatedEntity.TaskUpdate)
                    .SetProperty(x => x.EntityId, update.Id), ct);

            task.Status = WorkStatus.Completed;
            task.CompletionComment = null;
            task.CompletionPhotoKey = null;
        }

        await db.SaveChangesAsync(ct);
        return tasks.Count;
    }
}
