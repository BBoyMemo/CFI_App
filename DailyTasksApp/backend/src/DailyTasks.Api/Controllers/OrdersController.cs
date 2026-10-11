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
            .Include(o => o.Photos)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderDto>(await ToDtosAsync(orders, ct), total, page, pageSize);
    }

    // A description and up to Limits.MaxPhotos photos, each sent as a "photo" form field.
    [HttpPost]
    [RequestSizeLimit(Limits.MultiPhotoRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = Limits.MultiPhotoRequestBytes)]
    public async Task<ActionResult<OrderDto>> Create([FromForm] string? description, CancellationToken ct)
    {
        var validation = new Validation().Text("description", description, required: true, max: Limits.TextMax);
        var files = Request.HasFormContentType ? Request.Form.Files.GetFiles("photo") : [];
        if (files.Count > Limits.MaxPhotos) validation.Add("photo", "photo.tooMany");
        if (!validation.IsValid) return validation.ToResult();

        var savedKeys = new List<string>();
        foreach (var file in files)
        {
            var (key, photoError) = await photos.SaveAsync(file, "orders", ct);
            if (photoError is not null)
            {
                savedKeys.ForEach(photos.Delete);
                return new Validation().Add("photo", photoError).ToResult();
            }
            savedKeys.Add(key!);
        }

        var now = clock.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Description = description!.Trim(),
            Status = OrderStatus.New,
            CreatedById = User.UserId(),
            CreatedAt = now,
        };
        for (var i = 0; i < savedKeys.Count; i++)
        {
            // Keeps the order they were picked in.
            order.Photos.Add(new OrderPhoto { Id = Guid.NewGuid(), PhotoKey = savedKeys[i], CreatedAt = now.AddTicks(i) });
        }
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

    // A manager may take back "ordered" on the same factory-local day it was marked (a mistake
    // caught in time). From midnight on it is a purchase record and stays.
    [HttpPost("{id:guid}/undo-ordered")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<ActionResult<OrderDto>> UndoOrdered(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return ApiErrors.NotFound();
        if (order.Status != OrderStatus.Ordered)
            return ApiErrors.Conflict("order.notOrdered", "The order is not marked as ordered.");
        if (order.OrderedAt < clock.DayBounds(clock.Today).From)
            return ApiErrors.Conflict("order.undoExpired", "Ordered can only be undone on the same day.");

        order.Status = OrderStatus.New;
        order.OrderedAt = null;
        order.OrderedById = null;
        await db.SaveChangesAsync(ct);

        var updated = await WithDetails().SingleAsync(o => o.Id == id, ct);
        return (await ToDtosAsync([updated], ct))[0];
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Photos).SingleOrDefaultAsync(o => o.Id == id, ct);
        if (order is null) return ApiErrors.NotFound();
        // Once ordered it is a purchase record and stays; only a request still marked New can go.
        if (order.Status == OrderStatus.Ordered)
            return ApiErrors.Conflict("order.ordered", "An order that has been placed cannot be deleted.");

        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
        await translations.RemoveAllForAsync(id, ct);
        foreach (var photo in order.Photos) photos.Delete(photo.PhotoKey);
        return NoContent();
    }

    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> Photo(Guid id, Guid photoId, CancellationToken ct)
    {
        var key = await db.OrderPhotos
            .Where(p => p.Id == photoId && p.OrderId == id)
            .Select(p => p.PhotoKey)
            .SingleOrDefaultAsync(ct);
        if (key is null) return ApiErrors.NotFound("photo.notFound");

        var file = photos.Open(key);
        return file is null ? ApiErrors.NotFound("photo.notFound") : File(file.Value.Content, file.Value.ContentType);
    }

    private IQueryable<Order> WithDetails() =>
        db.Orders.AsNoTracking().Include(o => o.CreatedBy).Include(o => o.OrderedBy).Include(o => o.Photos);

    private async Task<List<OrderDto>> ToDtosAsync(IReadOnlyList<Order> orders, CancellationToken ct)
    {
        var set = await TranslationSet.LoadAsync(db, orders.Select(o => o.Id).ToList(), ct);
        return orders.Select(o => ToDto(o, set)).ToList();
    }

    private static OrderDto ToDto(Order o, TranslationSet set) => new(
        o.Id,
        o.Description,
        o.Photos.OrderBy(p => p.CreatedAt).Select(p => p.Id).ToList(),
        o.Status,
        o.CreatedAt,
        new UserRef(o.CreatedBy.Id, o.CreatedBy.Name),
        o.OrderedAt,
        o.OrderedBy is null ? null : new UserRef(o.OrderedBy.Id, o.OrderedBy.Name),
        set.For(o.Id, (TranslatedField.Description, o.Description)));
}
