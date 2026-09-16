using Asp.Versioning;
using CfiApp.Application.Abstractions;
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
/// Booking a holiday. Submitting the form is the employee's signature, approving it is
/// the manager's - there is no separate signature field, that was a deliberate choice
/// when the paper form was digitised. Working days exclude weekends and named UK bank
/// holidays, computed the same way for every request rather than typed in by hand.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/holiday")]
[Authorize]
public sealed class HolidayController(
    CfiAppDbContext context,
    IClock clock,
    IManagerScopeReader scopeReader,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.HolidayRequest)]
    [ProducesResponseType(typeof(HolidayRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<HolidayRequestDto>> Create(
        CreateHolidayRequest request, CancellationToken cancellationToken)
    {
        var publicHolidays = await context.PublicHolidays
            .Where(x => x.Date >= request.StartDate && x.Date <= request.EndDate)
            .Select(x => x.Date)
            .ToListAsync(cancellationToken);

        var workingDays = WorkingDaysCalculator.Count(request.StartDate, request.EndDate, publicHolidays.ToHashSet());
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        var holiday = new HolidayRequest
        {
            UserId = userId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            WorkingDays = workingDays,
            RequestedAt = now
        };

        context.HolidayRequests.Add(holiday);
        await context.SaveChangesAsync(cancellationToken);

        var name = await context.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstAsync(cancellationToken);
        return CreatedAtAction(nameof(Mine), new { }, ToDto(holiday, name));
    }

    [HttpGet("mine")]
    [Authorize(Policy = Permissions.HolidayRequest)]
    [ProducesResponseType(typeof(PagedResult<HolidayRequestDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<HolidayRequestDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        return ListAsync(BaseQuery().Where(x => x.UserId == userId), page, pageSize, cancellationToken);
    }

    /// <summary>Requests from the manager's own team - the same ManagerScope rule as everywhere else.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.HolidayApprove)]
    [ProducesResponseType(typeof(PagedResult<HolidayRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<HolidayRequestDto>>> List(
        [FromQuery] ApprovalStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var visibility = await scopeReader.GetVisibilityAsync(currentUser.UserId!.Value, cancellationToken);
        var query = BaseQuery();

        query = query.Where(x =>
            (x.User!.DepartmentId != null && visibility.DepartmentIds.Contains(x.User.DepartmentId.Value)) ||
            x.User.Areas.Any(a => visibility.UnitIds.Contains(a.Area!.UnitId)));

        if (status is not null) query = query.Where(x => x.Status == status);

        return await ListAsync(query, page, pageSize, cancellationToken);
    }

    [HttpPost("{id:int}/decide")]
    [Authorize(Policy = Permissions.HolidayApprove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Decide(int id, DecideRequest request, CancellationToken cancellationToken)
    {
        var holiday = await context.HolidayRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (holiday is null) return NotFound();

        holiday.Status = request.Approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        holiday.DecidedByUserId = currentUser.UserId;
        holiday.DecidedAt = clock.UtcNow;
        holiday.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<HolidayRequest> BaseQuery() => context.HolidayRequests.AsNoTracking();

    private static HolidayRequestDto ToDto(HolidayRequest x, string requestedByName) => new(
        x.Id, x.StartDate, x.EndDate, x.WorkingDays, x.Status.ToString(), requestedByName, x.RequestedAt, x.DecisionNote);

    private async Task<ActionResult<PagedResult<HolidayRequestDto>>> ListAsync(
        IQueryable<HolidayRequest> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        query = query.OrderByDescending(x => x.StartDate);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new HolidayRequestDto(
                x.Id, x.StartDate, x.EndDate, x.WorkingDays, x.Status.ToString(), x.User!.FullName,
                x.RequestedAt, x.DecisionNote))
            .ToListAsync(cancellationToken);

        return new PagedResult<HolidayRequestDto>(items, total, page, pageSize);
    }
}
