using Asp.Versioning;
using CfiApp.Application.Attendance;
using CfiApp.Domain.Attendance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Scheduling;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The site boundary used for automatic clocking. Real coordinates are entered here once
/// the site provides them; until then no GeofenceSetting is active and automatic clock-in
/// is refused with a clear message, never silently accepted against a wrong default.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/geofences")]
[Authorize(Policy = Permissions.AdminManage)]
public sealed class GeofenceSettingsController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<GeofenceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<GeofenceDto>>> List(CancellationToken cancellationToken)
    {
        var geofences = await context.GeofenceSettings.AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .Select(x => new GeofenceDto(
                x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusMeters,
                x.ReentryToleranceMinutes, x.RequiredAccuracyMeters, x.MaxClockDriftMinutes, x.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(geofences);
    }

    [HttpPost]
    [ProducesResponseType(typeof(GeofenceDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<GeofenceDto>> Create(UpsertGeofenceRequest request, CancellationToken cancellationToken)
    {
        var geofence = new GeofenceSetting
        {
            Name = request.Name.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            RadiusMeters = request.RadiusMeters,
            ReentryToleranceMinutes = request.ReentryToleranceMinutes,
            RequiredAccuracyMeters = request.RequiredAccuracyMeters,
            MaxClockDriftMinutes = request.MaxClockDriftMinutes
        };

        context.GeofenceSettings.Add(geofence);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), new { }, ToDto(geofence));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpsertGeofenceRequest request, CancellationToken cancellationToken)
    {
        var geofence = await context.GeofenceSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (geofence is null) return NotFound();

        geofence.Name = request.Name.Trim();
        geofence.Latitude = request.Latitude;
        geofence.Longitude = request.Longitude;
        geofence.RadiusMeters = request.RadiusMeters;
        geofence.ReentryToleranceMinutes = request.ReentryToleranceMinutes;
        geofence.RequiredAccuracyMeters = request.RequiredAccuracyMeters;
        geofence.MaxClockDriftMinutes = request.MaxClockDriftMinutes;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Only one geofence is active at a time - activating this one switches every other one off.</summary>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        var target = await context.GeofenceSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (target is null) return NotFound();

        var others = await context.GeofenceSettings.Where(x => x.Id != id && x.IsActive).ToListAsync(cancellationToken);
        foreach (var other in others) other.IsActive = false;

        target.IsActive = true;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var geofence = await context.GeofenceSettings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (geofence is null) return NotFound();

        geofence.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static GeofenceDto ToDto(GeofenceSetting x) => new(
        x.Id, x.Name, x.Latitude, x.Longitude, x.RadiusMeters,
        x.ReentryToleranceMinutes, x.RequiredAccuracyMeters, x.MaxClockDriftMinutes, x.IsActive);
}

/// <summary>UK bank holidays, so a leave request spanning Christmas does not charge days nobody was going to work anyway.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/public-holidays")]
[Authorize]
public sealed class PublicHolidaysController(CfiAppDbContext context) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PublicHolidayDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PublicHolidayDto>>> List(
        [FromQuery] int? year = null, CancellationToken cancellationToken = default)
    {
        var query = context.PublicHolidays.AsNoTracking().AsQueryable();
        if (year is not null) query = query.Where(x => x.Date.Year == year);

        var holidays = await query.OrderBy(x => x.Date)
            .Select(x => new PublicHolidayDto(x.Id, x.Date, x.Name, x.Region))
            .ToListAsync(cancellationToken);

        return Ok(holidays);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(typeof(PublicHolidayDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PublicHolidayDto>> Create(
        CreatePublicHolidayRequest request, CancellationToken cancellationToken)
    {
        var holiday = new PublicHoliday
        {
            Date = request.Date,
            Name = request.Name.Trim(),
            Region = string.IsNullOrWhiteSpace(request.Region) ? "England and Wales" : request.Region.Trim()
        };

        context.PublicHolidays.Add(holiday);
        await context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(List), new { }, new PublicHolidayDto(holiday.Id, holiday.Date, holiday.Name, holiday.Region));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.AdminManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var holiday = await context.PublicHolidays.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (holiday is null) return NotFound();

        // A calendar fact, not a business record - removing a wrongly entered date is a
        // real deletion, unlike everything work-order or attendance related.
        context.PublicHolidays.Remove(holiday);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public sealed record PublicHolidayDto(int Id, DateOnly Date, string Name, string Region);
public sealed record CreatePublicHolidayRequest(DateOnly Date, string Name, string? Region);
