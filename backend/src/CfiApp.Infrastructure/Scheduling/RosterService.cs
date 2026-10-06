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
    /// Puts a drawn-up shift into the pool - which is what "running" means - and its crew with
    /// it. This is the moment history starts: the crew becomes dated rota from today, or from
    /// the shift's first day if that is later. Until now the crew was only a draft.
    ///
    /// The pool entry is a copy of the shift, not a link to it, so deleting or rewriting the
    /// drawn-up shift afterwards does not change a rota people are already working to.
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

        var today = SiteCalendar.Today(clock);

        var alreadyRunning = await context.ActiveShifts.AsNoTracking().AnyAsync(
            x => x.SourceShiftTypeId == shiftTypeId && x.EndsOn >= today, cancellationToken);

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

        var from = template.StartsOn > today ? template.StartsOn : today;

        var crew = await context.ShiftTypeMembers.AsNoTracking()
            .Where(x => x.ShiftTypeId == shiftTypeId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        // Nobody on this crew can also be on a shift that shares its days - that was settled
        // when they were put on it - so this only adds; it never has to move anybody.
        foreach (var userId in crew)
        {
            AddRotaRow(userId, shift.Id, from);
            Notify(userId, "shift.rosterChanged");
        }

        await context.SaveChangesAsync(cancellationToken);
        return shift;
    }

    /// <summary>
    /// Takes a shift out of the pool. Its last day is yesterday, so it is gone from today's
    /// board the moment the button is pressed - closing it "as of today" left it sitting there
    /// for the rest of the day, which reads as the button doing nothing.
    ///
    /// What survives is only what was actually worked. A shift that never ran a day before
    /// today has no history to keep, so it goes entirely, along with anybody queued onto it.
    /// One that did run is closed, and the days people worked on it still read back.
    /// </summary>
    public async Task<bool> RemoveFromPoolAsync(int activeShiftId, CancellationToken cancellationToken)
    {
        var shift = await context.ActiveShifts.FirstOrDefaultAsync(x => x.Id == activeShiftId, cancellationToken);
        if (shift is null) return false;

        var today = SiteCalendar.Today(clock);
        var yesterday = today.AddDays(-1);

        var rota = await context.ShiftRosterEntries
            .Where(x => x.ActiveShiftId == activeShiftId)
            .ToListAsync(cancellationToken);

        var covers = await context.ShiftOverrides
            .Where(x => x.ActiveShiftId == activeShiftId)
            .ToListAsync(cancellationToken);

        foreach (var userId in rota.Where(x => x.EffectiveTo >= today).Select(x => x.UserId)
                     .Concat(covers.Where(x => x.ToDate >= today).Select(x => x.UserId))
                     .Distinct())
        {
            Notify(userId, "shift.rosterChanged");
        }

        if (shift.StartsOn > yesterday)
        {
            context.ShiftRosterEntries.RemoveRange(rota);
            context.ShiftOverrides.RemoveRange(covers);
            context.ActiveShifts.Remove(shift);
        }
        else
        {
            shift.EndsOn = yesterday;

            CloseOrDiscard([.. rota.Where(x => x.EffectiveTo >= today)], today);

            foreach (var cover in covers.Where(x => x.ToDate >= today))
            {
                if (cover.FromDate >= today) context.ShiftOverrides.Remove(cover);
                else cover.ToDate = yesterday;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------------------------------------------------------------- the crew

    /// <summary>
    /// Puts somebody on a shift's crew. It assigns, it does not move: they stay on every other
    /// shift that never shares a day with this one - weekday mornings and a weekend shift is a
    /// normal rota.
    ///
    /// A shift that does share a day is a clash, because nobody works two shifts on one day.
    /// That is refused until the manager has seen it and confirmed (MoveFromClashing); then
    /// they come off the clashing shifts from the date - closed on the rota, never deleted.
    ///
    /// Nothing here is dated unless a running shift is involved: one they join, or one they
    /// are moved off. Changes to shifts outside the pool are a draft and leave no history.
    /// </summary>
    public async Task PlaceOnCrewAsync(PlaceOnCrewRequest request, CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);
        var date = request.EffectiveFrom ?? today;

        var target = await context.ShiftTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ShiftTypeId, cancellationToken)
            ?? throw new ShiftRuleException("Unknown shift.");

        var memberships = await context.ShiftTypeMembers
            .Include(x => x.ShiftType)
            .Where(x => x.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        var running = await RunningCopyAsync(target.Id, today, cancellationToken);
        var alreadyOnIt = memberships.Any(x => x.ShiftTypeId == target.Id);

        List<ShiftRosterEntry> ownRows = running is null
            ? []
            : await RowsOnShiftFromAsync(request.UserId, running.Id, date, cancellationToken);

        // On a crew once. Dropping somebody where they already are changes nothing - unless
        // their place on this running shift is queued for later and is now being brought
        // forward, which is a real change.
        if (alreadyOnIt
            && (running is null
                || (ownRows.Count == 1 && ownRows[0].EffectiveFrom <= date && ownRows[0].EffectiveTo == DateOnly.MaxValue)))
        {
            return;
        }

        var clashingCrews = memberships
            .Where(x => x.ShiftTypeId != target.Id && (x.ShiftType!.Weekdays & target.Weekdays) != Weekdays.None)
            .ToList();

        var clashingRows = (await RowsFromAsync(request.UserId, date, cancellationToken))
            .Where(x => x.ActiveShiftId != running?.Id && (x.ActiveShift!.Weekdays & target.Weekdays) != Weekdays.None)
            .ToList();

        if ((clashingCrews.Count > 0 || clashingRows.Count > 0) && !request.MoveFromClashing)
        {
            var names = clashingCrews.Select(x => x.ShiftType!.Name)
                .Concat(clashingRows.Select(x => x.ActiveShift!.Name))
                .Distinct();

            throw new ShiftClashException(
                $"Already on {string.Join(", ", names)} on the same days. Confirm to move them.");
        }

        if ((running is not null || clashingRows.Count > 0) && date < today)
        {
            throw new ShiftRuleException(
                "A rota change cannot start in the past. Pick today or a later date.");
        }

        context.ShiftTypeMembers.RemoveRange(clashingCrews);
        CloseOrDiscard(clashingRows, date);

        if (!alreadyOnIt)
        {
            context.ShiftTypeMembers.Add(new ShiftTypeMember
            {
                UserId = request.UserId,
                ShiftTypeId = target.Id
            });
        }

        if (running is not null)
        {
            CloseOrDiscard(ownRows, date);
            AddRotaRow(request.UserId, running.Id, running.StartsOn > date ? running.StartsOn : date);
        }

        if (running is not null || clashingRows.Count > 0)
        {
            Notify(request.UserId, "shift.rosterChanged");
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Takes somebody off one shift's crew - only that one; any other shift they are on is
    /// untouched. If it is running they also come off its rota from the date, closed and never
    /// deleted, so the days they worked still read back.
    /// </summary>
    public async Task RemoveFromCrewAsync(
        int userId, RemoveFromCrewRequest request, CancellationToken cancellationToken)
    {
        var today = SiteCalendar.Today(clock);
        var date = request.EffectiveFrom ?? today;

        var membership = await context.ShiftTypeMembers.FirstOrDefaultAsync(
            x => x.UserId == userId && x.ShiftTypeId == request.ShiftTypeId, cancellationToken);

        var running = await RunningCopyAsync(request.ShiftTypeId, today, cancellationToken);

        List<ShiftRosterEntry> rows = running is null
            ? []
            : await RowsOnShiftFromAsync(userId, running.Id, date, cancellationToken);

        if (rows.Count > 0 && date < today)
        {
            throw new ShiftRuleException(
                "A rota change cannot start in the past. Pick today or a later date.");
        }

        if (membership is not null) context.ShiftTypeMembers.Remove(membership);

        if (rows.Count > 0)
        {
            CloseOrDiscard(rows, date);
            Notify(userId, "shift.rosterChanged");
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The pool entry a drawn-up shift is running as, if it is in the pool.</summary>
    private Task<ActiveShift?> RunningCopyAsync(int shiftTypeId, DateOnly today, CancellationToken cancellationToken) =>
        context.ActiveShifts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SourceShiftTypeId == shiftTypeId && x.EndsOn >= today, cancellationToken);

    /// <summary>
    /// This person's rows on one shift that are still in force on or after a date - not only
    /// the open-ended one: a row already given an end date still covers the days until then.
    /// </summary>
    private Task<List<ShiftRosterEntry>> RowsOnShiftFromAsync(
        int userId, int activeShiftId, DateOnly fromDate, CancellationToken cancellationToken) =>
        context.ShiftRosterEntries
            .Where(x => x.UserId == userId && x.ActiveShiftId == activeShiftId && x.EffectiveTo >= fromDate)
            .ToListAsync(cancellationToken);

    /// <summary>All of this person's rows in force on or after a date, with their shifts.</summary>
    private Task<List<ShiftRosterEntry>> RowsFromAsync(
        int userId, DateOnly fromDate, CancellationToken cancellationToken) =>
        context.ShiftRosterEntries
            .Include(x => x.ActiveShift)
            .Where(x => x.UserId == userId && x.EffectiveTo >= fromDate)
            .ToListAsync(cancellationToken);

    private void AddRotaRow(int userId, int activeShiftId, DateOnly from) =>
        context.ShiftRosterEntries.Add(new ShiftRosterEntry
        {
            UserId = userId,
            ActiveShiftId = activeShiftId,
            EffectiveFrom = from,
            EffectiveTo = DateOnly.MaxValue
        });

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
