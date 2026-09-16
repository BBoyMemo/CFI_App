using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Api.Controllers.V1;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Application.Maintenance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// History is what an auditor or a manager reaches for months later: closed jobs and
/// anything still moving through the QA cycle, searchable by number, location or
/// equipment - not the active work already covered by the pool and "mine" screens.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderHistoryTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"hist{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<HttpClient> SignedInAsAsync(string roleName)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test User", email, null, Password, "en"));

        await using (var scope = factory.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var user = await context.Users.SingleAsync(x => x.Email == email);
            var role = await context.Roles.SingleAsync(x => x.Name == roleName);
            user.RoleId = role.Id;
            user.Status = UserStatus.Active;
            await context.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task<int> SeededUnitIdAsync(HttpClient client)
    {
        var units = await client.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units");
        return units!.Items.First(x => x.Code == "UNIT2").Id;
    }

    private static CloseWorkOrderRequest StandardClosure() => new(
        RootCause: "Belt slipped off the drive pulley",
        CorrectiveAction: "Realigned and re-tensioned the belt",
        AbleToRepair: true,
        UnableToRepairReason: null,
        ContractorRequired: false,
        ContractorUsed: null,
        DowntimeMinutes: 20,
        ToolsAndPartsAccounted: true,
        MissingItemsNote: null,
        PostDeodorisationIntervention: false,
        PartsRequired: null,
        PartsPrice: null,
        LabourCostPerHour: null,
        PoNumber: null,
        PhotoAssetIds: []);

    private static async Task<int> ReportAndCloseAsync(
        HttpClient reporterClient, HttpClient engineerClient, int unitId, string equipmentFreeText)
    {
        var create = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, equipmentFreeText, Priority.Medium, "Reported for history search test.", []));
        var workOrder = (await create.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrder.Id}/claim", null);
        await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrder.Id}/close", StandardClosure());

        return workOrder.Id;
    }

    [Fact]
    public async Task History_contains_closed_jobs_but_not_jobs_still_open_in_the_pool()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var engineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var closedId = await ReportAndCloseAsync(
            reporterClient, engineerClient, unitId, $"History Blender {_counter}");

        var openCreate = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, $"Still Open Sack Filler {_counter}", Priority.Low, "Still open.", []));
        var openWorkOrder = (await openCreate.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;

        var history = await engineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/history?pageSize=200");

        history!.Items.ShouldContain(x => x.Id == closedId);
        history.Items.ShouldNotContain(x => x.Id == openWorkOrder.Id);
    }

    [Fact]
    public async Task History_is_visible_to_engineers_for_jobs_closed_by_someone_else()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var firstEngineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var secondEngineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var closedId = await ReportAndCloseAsync(
            reporterClient, firstEngineerClient, unitId, $"Shared History Motor {_counter}");

        var history = await secondEngineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/history?pageSize=200");

        history!.Items.ShouldContain(x => x.Id == closedId, "history covers everyone's closed jobs, not just the caller's own");
    }

    [Fact]
    public async Task Searching_history_matches_equipment_free_text_case_insensitively()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var engineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var marker = $"DeodoriserUnit{_counter}Special";
        var closedId = await ReportAndCloseAsync(reporterClient, engineerClient, unitId, marker);

        var results = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>(
            $"/api/v1/workorders/history?search={Uri.EscapeDataString(marker.ToLower())}");

        results!.Items.ShouldContain(x => x.Id == closedId);
    }

    [Fact]
    public async Task Searching_history_by_work_order_number_finds_exactly_that_job()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var engineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var closedId = await ReportAndCloseAsync(reporterClient, engineerClient, unitId, $"Numbered {_counter}");

        var detail = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{closedId}");

        var results = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>(
            $"/api/v1/workorders/history?search={detail!.Number}");

        results!.Items.ShouldHaveSingleItem();
        results.Items.Single().Id.ShouldBe(closedId);
    }

    [Fact]
    public async Task A_job_awaiting_qa_appears_in_history_even_though_it_is_not_closed_yet()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var engineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var create = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, $"Intrusive Area {_counter}", Priority.High, "Needs a swab test.", []));
        var workOrder = (await create.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrder.Id}/claim", null);
        await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrder.Id}/close",
            StandardClosure() with { PostDeodorisationIntervention = true });

        var history = await engineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/history?pageSize=200");

        history!.Items.ShouldContain(x => x.Id == workOrder.Id && x.Status == WorkOrderStatus.AwaitingQa);
    }

    [Fact]
    public async Task An_operator_cannot_read_history()
    {
        var operatorClient = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await operatorClient.GetAsync("/api/v1/workorders/history");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
