using System.Net;
using DailyTasks.Api.Data;
using DailyTasks.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace DailyTasks.Tests;

public class OrdersTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private static MultipartFormDataContent Form(string? description, byte[]? photo = null)
    {
        var form = new MultipartFormDataContent();
        if (description is not null) form.Add(new StringContent(description), "description");
        if (photo is not null) form.Add(TestData.File(photo), "photo", "part.jpg");
        return form;
    }

    [Fact]
    public async Task Engineer_and_manager_can_both_create_orders()
    {
        var engineer = await api.NewUserAsync("Engineer", "Charlie");
        var manager = await api.ManagerAsync();

        var fromEngineer = await engineer.Client.PostAsync("/api/orders", Form("2x V-belt SPA 1250", TestData.Jpeg));
        var fromManager = await manager.PostAsync("/api/orders", Form("Cable ties, 300mm"));

        fromEngineer.StatusCode.ShouldBe(HttpStatusCode.Created);
        fromManager.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await fromEngineer.JsonAsync();
        order.GetProperty("status").GetString().ShouldBe("New");
        var photoIds = order.GetProperty("photoIds").EnumerateArray().Select(p => p.GetGuid()).ToList();
        photoIds.Count.ShouldBe(1);
        order.GetProperty("createdBy").GetProperty("id").GetGuid().ShouldBe(engineer.Id);
        var photo = await manager.GetAsync($"/api/orders/{order.GetProperty("id").GetGuid()}/photos/{photoIds[0]}");
        photo.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await photo.Content.ReadAsByteArrayAsync()).ShouldBe(TestData.Jpeg);
    }

    [Fact]
    public async Task Description_is_required()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var response = await engineer.Client.PostAsync("/api/orders", Form("   "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.JsonAsync()).GetProperty("errors").GetProperty("description")[0].GetString().ShouldBe("required");
    }

    [Fact]
    public async Task Only_manager_marks_ordered_and_only_once()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await (await engineer.Client.PostAsync("/api/orders", Form("Grease cartridge"))).JsonAsync())
            .GetProperty("id").GetGuid();

        (await engineer.Client.PostAsync($"/api/orders/{id}/mark-ordered", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var manager = await api.ManagerAsync();
        var marked = await manager.PostAsync($"/api/orders/{id}/mark-ordered", null);
        marked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await marked.JsonAsync();
        body.GetProperty("status").GetString().ShouldBe("Ordered");
        body.GetProperty("orderedBy").GetProperty("name").GetString().ShouldBe(ApiFactory.ManagerName);

        (await manager.PostAsync($"/api/orders/{id}/mark-ordered", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Everyone_sees_all_orders_filtered_by_status_and_paged()
    {
        var a = await api.NewUserAsync("Engineer");
        var b = await api.NewUserAsync("Engineer");
        for (var i = 0; i < 3; i++) await a.Client.PostAsync("/api/orders", Form($"Fuse 10A #{i}"));

        var page = await (await b.Client.GetAsync("/api/orders?status=New&page=1&pageSize=2")).JsonAsync();

        page.GetProperty("items").GetArrayLength().ShouldBe(2);
        page.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(3);
        page.GetProperty("items").EnumerateArray().ShouldAllBe(o => o.GetProperty("status").GetString() == "New");
    }

    [Fact]
    public async Task Only_manager_deletes_orders()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await (await engineer.Client.PostAsync("/api/orders", Form("Spare hose"))).JsonAsync())
            .GetProperty("id").GetGuid();

        (await engineer.Client.DeleteAsync($"/api/orders/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var manager = await api.ManagerAsync();
        (await manager.DeleteAsync($"/api/orders/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await manager.DeleteAsync($"/api/orders/{id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Placed_order_cannot_be_deleted()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await (await engineer.Client.PostAsync("/api/orders", Form("Pressure gauge 0-10 bar"))).JsonAsync())
            .GetProperty("id").GetGuid();
        var manager = await api.ManagerAsync();
        await manager.PostAsync($"/api/orders/{id}/mark-ordered", null);

        var delete = await manager.DeleteAsync($"/api/orders/{id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await delete.JsonAsync()).GetProperty("code").GetString().ShouldBe("order.ordered");
    }

    [Fact]
    public async Task Search_finds_orders_by_text_people_and_day_across_statuses()
    {
        var asker = await api.NewUserAsync("Engineer", "Rosalind");
        var marker = Guid.NewGuid().ToString("N")[..8];
        var placed = (await (await asker.Client.PostAsync("/api/orders", Form($"Bearing 6205 {marker}"))).JsonAsync())
            .GetProperty("id").GetGuid();
        var pending = (await (await asker.Client.PostAsync("/api/orders", Form($"Seal kit {marker}"))).JsonAsync())
            .GetProperty("id").GetGuid();
        var manager = await api.ManagerAsync();
        await manager.PostAsync($"/api/orders/{placed}/mark-ordered", null);

        async Task<List<Guid>> Search(string query)
        {
            var body = await (await asker.Client.GetAsync($"/api/orders?pageSize=100&{query}")).JsonAsync();
            return body.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToList();
        }

        (await Search($"q={marker}")).ShouldBe([placed, pending], ignoreOrder: true);
        (await Search($"q=BEARING%206205")).ShouldContain(placed);
        (await Search($"q=6205%20{marker}")).ShouldBe([placed]);
        (await Search($"q={Uri.EscapeDataString(asker.Name.ToLowerInvariant())}")).ShouldBe([placed, pending], ignoreOrder: true);
        (await Search($"q={marker}&status=Ordered")).ShouldBe([placed]);
        (await Search($"q={Uri.EscapeDataString(ApiFactory.ManagerName)}")).ShouldContain(placed); // who ordered it

        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            api.Clock.Now, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).DateTime);
        (await Search($"q={marker}&date={day:yyyy-MM-dd}")).ShouldBe([placed, pending], ignoreOrder: true);
        (await Search($"q={marker}&date={day.AddDays(-1):yyyy-MM-dd}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_order_takes_up_to_ten_photos()
    {
        var engineer = await api.NewUserAsync("Engineer");
        MultipartFormDataContent WithPhotos(int count)
        {
            var form = Form("Bearings, assorted");
            for (var i = 0; i < count; i++) form.Add(TestData.File(TestData.Jpeg), "photo", $"p{i}.jpg");
            return form;
        }

        var ten = await engineer.Client.PostAsync("/api/orders", WithPhotos(10));
        ten.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await ten.JsonAsync()).GetProperty("photoIds").GetArrayLength().ShouldBe(10);

        var eleven = await engineer.Client.PostAsync("/api/orders", WithPhotos(11));
        eleven.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await eleven.JsonAsync()).GetProperty("errors").GetProperty("photo")[0].GetString().ShouldBe("photo.tooMany");
    }

    [Fact]
    public async Task Manager_can_undo_ordered_until_midnight_only()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        var original = api.Clock.Now;
        var local = TimeZoneInfo.ConvertTime(original, london);
        var midnight = local.Date.AddDays(1);
        var nextMidnight = new DateTimeOffset(midnight, london.GetUtcOffset(midnight)).ToUniversalTime();
        try
        {
            var engineer = await api.NewUserAsync("Engineer");
            var manager = await api.ManagerAsync();
            var id = (await (await engineer.Client.PostAsync("/api/orders", Form("Proximity sensor M18"))).JsonAsync())
                .GetProperty("id").GetGuid();

            api.Clock.Now = nextMidnight.AddMinutes(-5);
            (await manager.PostAsync($"/api/orders/{id}/mark-ordered", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
            (await engineer.Client.PostAsync($"/api/orders/{id}/undo-ordered", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

            var undone = await manager.PostAsync($"/api/orders/{id}/undo-ordered", null);
            undone.StatusCode.ShouldBe(HttpStatusCode.OK, await undone.Content.ReadAsStringAsync());
            var body = await undone.JsonAsync();
            body.GetProperty("status").GetString().ShouldBe("New");
            body.GetProperty("orderedBy").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
            (await manager.PostAsync($"/api/orders/{id}/undo-ordered", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

            await manager.PostAsync($"/api/orders/{id}/mark-ordered", null);
            api.Clock.Now = nextMidnight.AddMinutes(1);
            var late = await manager.PostAsync($"/api/orders/{id}/undo-ordered", null);
            late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await late.JsonAsync()).GetProperty("code").GetString().ShouldBe("order.undoExpired");
        }
        finally
        {
            api.Clock.Now = original;
        }
    }

    [Fact]
    public async Task Single_photo_orders_from_before_keep_their_photo()
    {
        var engineer = await api.NewUserAsync("Engineer");
        var id = (await (await engineer.Client.PostAsync("/api/orders", Form("Old style order"))).JsonAsync())
            .GetProperty("id").GetGuid();

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // As the database looked before orders had several photos.
            var order = await db.Orders.SingleAsync(o => o.Id == id);
            order.PhotoKey = "orders/legacy.jpg";
            await db.SaveChangesAsync();

            (await OrderPhotoBackfill.RunAsync(db)).ShouldBeGreaterThanOrEqualTo(1);
            (await OrderPhotoBackfill.RunAsync(db)).ShouldBe(0); // nothing done twice
            (await db.OrderPhotos.CountAsync(p => p.OrderId == id)).ShouldBe(1);
        }

        var list = await (await engineer.Client.GetAsync("/api/orders?q=Old%20style%20order")).JsonAsync();
        list.GetProperty("items").EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == id)
            .GetProperty("photoIds").GetArrayLength().ShouldBe(1);
    }
}
