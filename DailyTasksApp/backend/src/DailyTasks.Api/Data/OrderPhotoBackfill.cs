using DailyTasks.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Data;

// Orders used to carry one photo on the order itself. This moves it into order_photos, where
// orders now keep several. Safe to run on every start: it only touches orders that still have one.
public static class OrderPhotoBackfill
{
    public static async Task<int> RunAsync(AppDbContext db, CancellationToken ct = default)
    {
        var orders = await db.Orders.Where(o => o.PhotoKey != null).ToListAsync(ct);
        foreach (var order in orders)
        {
            db.OrderPhotos.Add(new OrderPhoto
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                PhotoKey = order.PhotoKey!,
                CreatedAt = order.CreatedAt,
            });
            order.PhotoKey = null;
        }

        await db.SaveChangesAsync(ct);
        return orders.Count;
    }
}
