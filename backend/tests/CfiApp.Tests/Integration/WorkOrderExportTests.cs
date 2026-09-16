using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
/// The CSV a manager hands a BRC auditor who wants their own copy. Has to be a real,
/// parseable file - a comma or a line break typed into a free-text root cause field must
/// not silently corrupt the row after it.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderExportTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"export{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

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

    private static async Task<int> UnitIdAsync(HttpClient client)
    {
        var units = await client.GetFromJsonAsync<CfiApp.Api.Controllers.V1.PagedResult<CfiApp.Application.Admin.UnitDto>>(
            "/api/v1/admin/units");
        return units!.Items.First(x => x.Code == "UNIT3").Id;
    }

    [Fact]
    public async Task The_export_is_a_csv_file_with_a_header_row_and_one_row_per_closed_job()
    {
        var reporterClient = await SignedInAsAsync(Permissions.Roles.Operator);
        var engineerClient = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await UnitIdAsync(reporterClient);

        // Root cause deliberately contains a comma and a quote, to prove the CSV escaping
        // is real rather than assumed.
        var create = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, $"Export Test Rig {_counter}", Priority.Low, "For export test.", []));
        var workOrder = (await create.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrder.Id}/claim", null);
        await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrder.Id}/close", new CloseWorkOrderRequest(
            RootCause: "Belt, pulley \"A\" misaligned",
            CorrectiveAction: "Realigned, tightened",
            AbleToRepair: true,
            UnableToRepairReason: null,
            ContractorRequired: false,
            ContractorUsed: null,
            DowntimeMinutes: 12,
            ToolsAndPartsAccounted: true,
            MissingItemsNote: null,
            PostDeodorisationIntervention: false,
            PartsRequired: null,
            PartsPrice: null,
            LabourCostPerHour: null,
            PoNumber: null,
            PhotoAssetIds: []));

        var response = await engineerClient.GetAsync("/api/v1/workorders/history/export?pageSize=200");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/csv");

        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        lines[0].ShouldStartWith("Number,Status,Priority,Unit");

        var dataLine = lines.FirstOrDefault(line => line.StartsWith(workOrder.Number));
        dataLine.ShouldNotBeNull("the closed job must appear as its own row");

        // A comma and an embedded quote inside the free-text field must be escaped, not
        // break the row into extra columns.
        var row = dataLine!;
        row.ShouldContain("\"Belt, pulley \"\"A\"\" misaligned\"");
    }

    [Fact]
    public async Task An_operator_cannot_export_history()
    {
        var operatorClient = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await operatorClient.GetAsync("/api/v1/workorders/history/export");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
