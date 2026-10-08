using System.Net;
using DailyTasks.Tests.Infrastructure;
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
        order.GetProperty("hasPhoto").GetBoolean().ShouldBeTrue();
        order.GetProperty("createdBy").GetProperty("id").GetGuid().ShouldBe(engineer.Id);
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
}
