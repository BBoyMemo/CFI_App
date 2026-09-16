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
/// "This part has run out." An engineer raises it, sees and can withdraw only their own
/// requests; the manager sees everyone's, can remove any of them, and marks one ordered
/// once it has actually been bought. No notifications either way - both sides just check
/// their own list.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Authorize]
public sealed class OrdersController(
    CfiAppDbContext context,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.OrderCreate)]
    [ProducesResponseType(typeof(PartOrderRequestDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<PartOrderRequestDto>> Create(
        CreatePartOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        var order = new PartOrderRequest
        {
            PartName = request.PartName.Trim(),
            Quantity = request.Quantity,
            IsUrgent = request.IsUrgent,
            RequestedByUserId = userId,
            RequestedAt = now
        };

        context.PartOrderRequests.Add(order);
        await context.SaveChangesAsync(cancellationToken);

        var requesterName = await context.Users.Where(x => x.Id == userId).Select(x => x.FullName).FirstAsync(cancellationToken);
        return CreatedAtAction(nameof(Mine), new { }, ToDto(order, requesterName));
    }

    /// <summary>The signed-in engineer's own requests, newest first, pending and already ordered alike.</summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.OrderCreate)]
    [ProducesResponseType(typeof(PagedResult<PartOrderRequestDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<PartOrderRequestDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        return ListAsync(BaseQuery().Where(x => x.RequestedByUserId == userId), page, pageSize, cancellationToken);
    }

    /// <summary>Every request from every engineer - the manager's ordering list.</summary>
    [HttpGet]
    [Authorize(Policy = Permissions.OrderManageAll)]
    [ProducesResponseType(typeof(PagedResult<PartOrderRequestDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<PartOrderRequestDto>>> List(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();
        if (status is not null) query = query.Where(x => x.Status == status);

        return ListAsync(query, page, pageSize, cancellationToken);
    }

    [HttpPost("{id:int}/mark-ordered")]
    [Authorize(Policy = Permissions.OrderManageAll)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkOrdered(int id, CancellationToken cancellationToken)
    {
        var order = await context.PartOrderRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (order is null) return NotFound();

        order.Status = OrderStatus.Ordered;
        order.OrderedAt = clock.UtcNow;
        order.OrderedByUserId = currentUser.UserId;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>An engineer withdraws their own request; a manager can remove anyone's.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var order = await context.PartOrderRequests
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (order is null) return NotFound();

        var userId = currentUser.UserId!.Value;
        var canManageAll = User.HasClaim(CfiApp.Api.Security.CfiClaimTypes.Permission, Permissions.OrderManageAll);

        if (order.RequestedByUserId != userId && !canManageAll)
        {
            return Problem(title: "You can only remove your own requests", statusCode: StatusCodes.Status403Forbidden);
        }

        order.DeletedAt = clock.UtcNow;
        order.DeletedByUserId = userId;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private IQueryable<PartOrderRequest> BaseQuery() =>
        context.PartOrderRequests.AsNoTracking().Where(x => x.DeletedAt == null);

    private static PartOrderRequestDto ToDto(PartOrderRequest order, string requesterName) => new(
        order.Id, order.PartName, order.Quantity, order.IsUrgent, requesterName,
        order.RequestedAt, order.Status.ToString(), order.OrderedAt);

    private async Task<ActionResult<PagedResult<PartOrderRequestDto>>> ListAsync(
        IQueryable<PartOrderRequest> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        query = query.OrderByDescending(x => x.RequestedAt);
        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new PartOrderRequestDto(
                x.Id, x.PartName, x.Quantity, x.IsUrgent, x.RequestedBy!.FullName,
                x.RequestedAt, x.Status.ToString(), x.OrderedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PartOrderRequestDto>(items, total, page, pageSize);
    }
}
