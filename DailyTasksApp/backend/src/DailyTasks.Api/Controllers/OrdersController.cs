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
[Route("api/orders")]
[Authorize]
public class OrdersController(AppDbContext db, AppClock clock, PhotoService photos, TranslationQueue translations)
    : ControllerBase
{
    // Everyone sees every order, so the team does not request the same part twice.
    // Optional filters: status; q = text in the description (original or any translation) or the
    // name of who asked for it or ordered it (case-insensitive); date = factory-local day it was
    // asked for or ordered.
    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderDto>>> List(
        [FromQuery] OrderStatus? status, [FromQuery] string? q, [FromQuery] DateOnly? date,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = Math.Clamp(pageSize, 1, Limits.MaxPageSize);

        var query = db.Orders.AsNoTracking();
        if (status is not null) query = query.Where(o => o.Status == status);

        var text = q?.Trim().ToLower();
        if (!string.IsNullOrEmpty(text))
        {
            if (text.Length > Limits.TitleMax) text = text[..Limits.TitleMax];
            query = query.Where(o =>
                o.Description.ToLower().Contains(text) ||
                o.CreatedBy.Name.ToLower().Contains(text) ||
                (o.OrderedBy != null && o.OrderedBy.Name.ToLower().Contains(text)) ||
                db.Translations.Any(x => x.EntityType == TranslatedEntity.Order && x.EntityId == o.Id &&
                                         x.Text.ToLower().Contains(text)));
        }

        if (date is not null)
        {
            var (from, to) = clock.DayBounds(date.Value);
            query = query.Where(o =>
                (o.CreatedAt >= from && o.CreatedAt < to) ||
                (o.OrderedAt != null && o.OrderedAt >= from && o.OrderedAt < to));
        }

        var total = await query.CountAsync(ct);
        var orders = await query
            .Include(o => o.CreatedBy)
            .Include(o => o.OrderedBy)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(await ToDtosAsync(orders, ct), total, page, pageSize);
    }

    [HttpPost]
    [RequestSizeLimit(Limits.UploadRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Limits.UploadRequestBytes)]
    public async Task<ActionResult<OrderDto>> Create(
        [FromForm] string? description, IFormFile? photo, CancellationToken ct)
    {
        var validation = new Validation().Text("description", description, required: true, max: Limits.TextMax);
        if (!validation.IsValid) return validation.ToResult();

        string? photoKey = null;
        if (photo is not null)
        {
            var (key, photoError) = await photos.SaveAsync(photo, "orders", ct);
            if (photoError is not null) return new Validation().Add("photo", photoError).ToResult();
            photoKey = key;
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Description = description!.Trim(),
            PhotoKey = photoKey,
            Status = OrderStatus.New,
            CreatedById = User.UserId(),
            CreatedAt = clock.UtcNow,
        };
        db.Orders.Add(order);
        await translations.AddAsync(TranslatedEntity.Order, order.Id, ct);
        await db.SaveChangesAsync(ct);

        var created = await WithDetails().SingleAsync(o => o.Id == order.Id, ct);
        return StatusCode(StatusCodes.Status201Created, (await ToDtosAsync([created], ct))[0]);
    }

    [HttpPost("{id:guid}/mark-ordered")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<ActionResult<OrderDto>> MarkOrdered(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return ApiErrors.NotFound();
        if (order.Status == OrderStatus.Ordered)
            return ApiErrors.Conflict("order.alreadyOrdered", "The order is already marked as ordered.");

        order.Status = OrderStatus.Ordered;
        order.OrderedAt = clock.UtcNow;
        order.OrderedById = User.UserId();
        await db.SaveChangesAsync(ct);

        var updated = await WithDetails().SingleAsync(o => o.Id == id, ct);
        return (await ToDtosAsync([updated], ct))[0];
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return ApiErrors.NotFound();
        // Once ordered it is a purchase record and stays; only a request still marked New can go.
        if (order.Status == OrderStatus.Ordered)
            return ApiErrors.Conflict("order.ordered", "An order that has been placed cannot be deleted.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
        await translations.RemoveAllForAsync(id, ct);
        photos.Delete(order.PhotoKey);
        return NoContent();
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var key = await db.Orders.Where(o => o.Id == id).Select(o => o.PhotoKey).SingleOrDefaultAsync(ct);
        if (key is null) return ApiErrors.NotFound("photo.notFound");

        var file = photos.Open(key);
        return file is null ? ApiErrors.NotFound("photo.notFound") : File(file.Value.Content, file.Value.ContentType);
    }

    private IQueryable<Order> WithDetails() =>
        db.Orders.AsNoTracking().Include(o => o.CreatedBy).Include(o => o.OrderedBy);

    private async Task<List<OrderDto>> ToDtosAsync(IReadOnlyList<Order> orders, CancellationToken ct)
    {
        var set = await TranslationSet.LoadAsync(db, orders.Select(o => o.Id).ToList(), ct);
        return orders.Select(o => ToDto(o, set)).ToList();
    }

    private static OrderDto ToDto(Order o, TranslationSet set) => new(
        o.Id,
        o.Description,
        o.PhotoKey is not null,
        o.Status,
        o.CreatedAt,
        new UserRef(o.CreatedBy.Id, o.CreatedBy.Name),
        o.OrderedAt,
        o.OrderedBy is null ? null : new UserRef(o.OrderedBy.Id, o.OrderedBy.Name),
        set.For(o.Id, (TranslatedField.Description, o.Description)));
}
