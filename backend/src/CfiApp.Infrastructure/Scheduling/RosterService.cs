using CfiApp.Application.Abstractions;
using CfiApp.Application.Scheduling;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Messaging;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Scheduling;

/// <summary>
/// Every write to the pool and the rota. The rules that make history trustworthy live here
/// rather than in the controller, because all of them are about what the existing rows mean,
/// not about HTTP:
///
/// a change is dated and never retroactive, so what was published stays published;
/// superseding closes the old row instead of deleting it, so last month still reads back;
/// and the person affected is told, because a shift change they find out about by turning up
/// is the whole reason the paper rota was a problem.
/// </summary>
public sealed class RosterService(
    CfiAppDbContext context,
    IClock clock,
    ICurrentUser currentUser)
{
    private const int MaxCoverBackdatingDays = 31;

    // ---------------------------------------------------------------- the pool

    /// <summary>
    /// Copies a drawn-up shift into the pool. A copy, not a link: the template is a sketch
    /// somebody can delete or rewrite afterwards, and neither must change a rota people are
    /// already working to.
    /// </summary>
    public async Task<ActiveShift> AddToPoolAsync(int shiftTypeId, CancellationToken cancellationToken)
    {
        var template = await context.ShiftTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == shiftTypeId, cancellationToken)
            ?? throw new ShiftRuleException("Unknown shift.");

        if (template.Weekdays == Weekdays.None)
        {
            throw new ShiftRuleException("This shift does not run on any day of the week.");
        }

        var alreadyRunning = await context.ActiveShifts.AsNoTracking().AnyAsync(
            x => x.SourceShiftTypeId == shiftTypeId && x.EndsOn == DateOnly.MaxValue, cancellationToken);

        if (alreadyRunning)
        {
            throw new ShiftClashException("That shift is already in the pool.");
        }

        var shift = new ActiveShift
        {
            Name = template.Name,
            StartTime = template.StartTime,
            EndTime = template.EndTime,
            Weekdays = template.Weekdays,
            StartsOn = template.StartsOn,
            DisplayOrder = template.DisplayOrder,
            SourceShiftTypeId = template.Id
        };

        context.ActiveShifts.Add(shift);
        await context.SaveChangesAsync(cancellationToken);

        return shift;
    }

    /// <summary>
    /// Takes a shift out of the pool. If nobody was ever on it the row goes entirely; if
    /// somebody was, it is closed instead, because the weeks they worked on it still have to
    /// read back correctly.
    /// </summary>
    public async Task<bool> RemoveFromPoolAsync(int activeShiftId, CancellationToken cancellationToken)
    {
        var shift = await context.ActiveShifts.FirstOrDefaultAsync(x => x.Id == activeShiftId, cancellationToken);
        if (shift is null) return false;

        var everUsed = await context.ShiftRosterEntries.AsNoTracking()
            .AnyAsync(x => x.ActiveShiftId == activeShiftId, cancellationToken)
            || await context.ShiftOverrides.AsNoTracking()
            .AnyAsync(x => x.ActiveShiftId == activeShiftId, cancellationToken);

        if (everUsed)
        {
            var today = SiteCalendar.Today(clock);
            shift.EndsOn = today < shift.StartsOn ? shift.StartsOn : today;

            var affected = await context.ShiftRosterEntries
                .Where(x => x.ActiveShiftId == activeShiftId && x.EffectiveTo == DateOnly.MaxValue)
                .ToListAsync(cancellationToken);

            foreach (var entry in affected)
            {
                entry.EffectiveTo = shift.EndsOn;
                Notify(entry.UserId, "shift.rosterChanged");
            }
        }
        else
        {
            context.ActiveShifts.Remove(shift);
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------------------------------------------------------------- the rota

    /// <summary>
    /// Puts somebody on a shift from a date. Which days of the week that means is the shift's
    /// business, not this person's, so there is nothing else to choose.
    /// </summary>
    public async Task SetAsync(SetRosterRequest request, CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);

        if (request.EffectiveFrom < today)
        {
            throw new ShiftRuleException(
                "A rota change cannot start in the past. Pick today or a later date.");
        }

        var shift = await context.ActiveShifts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ActiveShiftId, cancellationToken)
            ?? throw new ShiftRuleException("That shift is not in the pool.");

        if (shift.EndsOn < request.EffectiveFrom)
        {
            throw new ShiftRuleException("That shift is no longer running.");
        }

        var current = await CurrentEntriesAsync(request.UserId, cancellationToken);

        // Already on that shift and already in force: say nothing and write nothing, so
        // re-dropping somebody where they already are is not a change in the history.
        if (current.Any(x => x.ActiveShiftId == request.ActiveShiftId && x.EffectiveFrom <= request.EffectiveFrom))
        {
            return;
        }

        CloseOrDiscard(current, request.EffectiveFrom);

        context.ShiftRosterEntries.Add(new ShiftRosterEntry
        {
            UserId = request.UserId,
            ActiveShiftId = request.ActiveShiftId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = DateOnly.MaxValue
        });

        Notify(request.UserId, "shift.rosterChanged");
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Takes somebody off the rota from a date. The rows are closed, never removed: the
    /// question "who was on nights in March" has to stay answerable after they move on.
    /// </summary>
    public async Task EndAsync(int userId, EndRosterRequest request, CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);

        if (request.EffectiveFrom < today)
        {
            throw new ShiftRuleException(
                "A rota change cannot start in the past. Pick today or a later date.");
        }

        var current = await CurrentEntriesAsync(userId, cancellationToken);
        if (current.Count == 0) return;

        CloseOrDiscard(current, request.EffectiveFrom);

        Notify(userId, "shift.rosterChanged");
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Cover, or time off, for a run of days. Backdating is allowed here, unlike the rota:
    /// "he was off sick yesterday" is a correction somebody genuinely needs to record, and an
    /// override only ever adds to the picture - it never rewrites a rota row.
    /// </summary>
    public async Task<ShiftOverride> AddCoverAsync(CreateCoverRequest request, CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);

        if (request.FromDate < today.AddDays(-MaxCoverBackdatingDays))
        {
            throw new ShiftRuleException(
                $"Cover cannot be recorded more than {MaxCoverBackdatingDays} days back.");
        }

        if (request.ActiveShiftId is { } shiftId
            && !await context.ActiveShifts.AsNoTracking().AnyAsync(x => x.Id == shiftId, cancellationToken))
        {
            throw new ShiftRuleException("That shift is not in the pool.");
        }

        var clashesWithCover = await context.ShiftOverrides.AsNoTracking().AnyAsync(
            x => x.UserId == request.UserId && x.FromDate <= request.ToDate && x.ToDate >= request.FromDate,
            cancellationToken);

        if (clashesWithCover)
        {
            throw new ShiftClashException("This person already has cover recorded over those dates.");
        }

        // Booked leave wins the argument by being refused rather than overwritten: the manager
        // either cancels the leave or picks somebody else, and neither of those is the server's
        // decision to make silently.
        if (request.ActiveShiftId is not null)
        {
            var onLeave = await context.HolidayRequests.AsNoTracking().AnyAsync(
                x => x.UserId == request.UserId
                    && x.Status == ApprovalStatus.Approved
                    && x.StartDate <= request.ToDate && x.EndDate >= request.FromDate,
                cancellationToken);

            if (onLeave)
            {
                throw new ShiftClashException("This person has approved leave over those dates.");
            }
        }

        var cover = new ShiftOverride
        {
            UserId = request.UserId,
            ActiveShiftId = request.ActiveShiftId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };

        context.ShiftOverrides.Add(cover);
        Notify(request.UserId, "shift.coverAdded");
        await context.SaveChangesAsync(cancellationToken);

        return cover;
    }

    /// <summary>
    /// Removing cover is a hard delete on purpose: it is a correction to a plan, not a record
    /// of what happened. The standing rota underneath it is what history is kept in.
    /// </summary>
    public async Task<bool> RemoveCoverAsync(int id, CancellationToken cancellationToken)
    {
        var cover = await context.ShiftOverrides.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cover is null) return false;

        context.ShiftOverrides.Remove(cover);
        Notify(cover.UserId, "shift.coverRemoved");
        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    private Task<List<ShiftRosterEntry>> CurrentEntriesAsync(int userId, CancellationToken cancellationToken) =>
        context.ShiftRosterEntries
            .Where(x => x.UserId == userId && x.EffectiveTo == DateOnly.MaxValue)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// A row that had already started is closed the day before the new one opens. A row that
    /// was queued to start later never applied to anybody, so it is dropped rather than
    /// closed - closing it would leave an end date before its start date.
    /// </summary>
    private void CloseOrDiscard(IReadOnlyCollection<ShiftRosterEntry> current, DateOnly effectiveFrom)
    {
        foreach (var entry in current)
        {
            if (entry.EffectiveFrom >= effectiveFrom)
            {
                context.ShiftRosterEntries.Remove(entry);
            }
            else
            {
                entry.EffectiveTo = effectiveFrom.AddDays(-1);
            }
        }
    }

    /// <summary>
    /// The person finds out from the app rather than from the noticeboard. Deliberately no
    /// body text: the notification's job is to send them to their own week, which is the one
    /// place that is always right.
    /// </summary>
    private void Notify(int userId, string type)
    {
        // Nobody needs telling about a change they made to their own rota.
        if (currentUser.UserId == userId) return;

        context.NotificationLogs.Add(new NotificationLog
        {
            UserId = userId,
            Type = type,
            SentAt = clock.UtcNow,
            Delivery = NotificationDelivery.Sent
        });
    }
}
