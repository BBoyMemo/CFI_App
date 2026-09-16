using CfiApp.Application.Abstractions;
using CfiApp.Application.Attendance;
using CfiApp.Domain.Attendance;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Attendance;

public sealed class ClockRejectedException(string reason) : Exception(reason);

/// <summary>
/// Clock In/Out. This module feeds payroll, so a device is trusted only as far as it can
/// be checked: an automatic event has to actually be inside the site boundary with a
/// GPS fix good enough to mean something, and a device clock that disagrees with the
/// server is flagged for a manager rather than silently believed.
/// </summary>
public sealed class AttendanceService(CfiAppDbContext context, IClock clock, ICurrentUser currentUser)
{
    public async Task<ClockEvent> RecordAsync(ClockRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        var lastEvent = await context.ClockEvents
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // The first ever event for someone must be a clock in; after that, In and Out
        // must alternate - two clock-ins in a row means a client retried instead of the
        // person actually leaving and coming back.
        var expectedNext = lastEvent is null ? ClockType.In : Opposite(lastEvent.Type);

        if (request.Type != expectedNext)
        {
            throw new ClockRejectedException(
                expectedNext == ClockType.In
                    ? "You are already clocked out. Clock in first."
                    : "You are already clocked in. Clock out first.");
        }

        bool isSuspect = false;
        string? suspectReason = null;

        if (request.Source == ClockSource.AutoGeofence)
        {
            var geofence = await context.GeofenceSettings
                .Where(x => x.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (geofence is null)
            {
                throw new ClockRejectedException(
                    "Automatic clock-in is not configured yet. Use manual clock in.");
            }

            if (request.IsMockLocation)
            {
                throw new ClockRejectedException("This location could not be trusted. Use manual clock in.");
            }

            if (request.AccuracyMeters is null || request.AccuracyMeters > geofence.RequiredAccuracyMeters)
            {
                throw new ClockRejectedException(
                    "GPS signal is not accurate enough right now. Use manual clock in.");
            }

            var distance = GeoDistance.HaversineMetres(
                request.Latitude!.Value, request.Longitude!.Value, geofence.Latitude, geofence.Longitude);

            if (distance > geofence.RadiusMeters)
            {
                throw new ClockRejectedException("You do not appear to be on site. Use manual clock in if this is wrong.");
            }
        }

        var driftMinutes = Math.Abs((request.OccurredAtUtc - now).TotalMinutes);
        var maxDrift = await context.GeofenceSettings
            .Where(x => x.IsActive)
            .Select(x => (int?)x.MaxClockDriftMinutes)
            .FirstOrDefaultAsync(cancellationToken) ?? 15;

        if (driftMinutes > maxDrift)
        {
            isSuspect = true;
            suspectReason = $"Device time differs from server time by {driftMinutes:F0} minutes.";
        }

        var clockEvent = new ClockEvent
        {
            UserId = userId,
            Type = request.Type,
            OccurredAtUtc = request.OccurredAtUtc,
            ReceivedAtUtc = now,
            Source = request.Source,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            AccuracyMeters = request.AccuracyMeters,
            IsMockLocation = request.IsMockLocation,
            DeviceId = request.DeviceId,
            ClientId = request.ClientId,
            IsSuspect = isSuspect,
            SuspectReason = suspectReason
        };

        context.ClockEvents.Add(clockEvent);
        await context.SaveChangesAsync(cancellationToken);

        return clockEvent;
    }

    public async Task CorrectAsync(int clockEventId, ClockCorrectionRequest request, CancellationToken cancellationToken)
    {
        if (!await context.ClockEvents.AnyAsync(x => x.Id == clockEventId, cancellationToken))
        {
            throw new ClockRejectedException($"Clock event {clockEventId} was not found.");
        }

        // The original row is never edited - a correction is a new, append-only record
        // that points at it, so payroll can always see what was first recorded.
        context.ClockCorrections.Add(new ClockCorrection
        {
            ClockEventId = clockEventId,
            CorrectedByUserId = currentUser.UserId!.Value,
            Reason = request.Reason.Trim(),
            NewOccurredAtUtc = request.NewOccurredAtUtc,
            CreatedAt = clock.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static ClockType Opposite(ClockType type) => type == ClockType.In ? ClockType.Out : ClockType.In;
}
