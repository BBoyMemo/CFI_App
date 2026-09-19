using CfiApp.Api.Controllers.V1;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CfiApp.Application.Admin;
using CfiApp.Application.Auth;
using CfiApp.Application.Maintenance;
using CfiApp.Application.Messaging;
using CfiApp.Application.Media;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Domain.Messaging;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CfiApp.Tests.Integration;

/// <summary>
/// The full breakdown lifecycle over HTTP, the way a client actually drives it: report,
/// pool, claim, work, close, and - for intrusive jobs - the QA fail/resubmit loop. These
/// tests exist to prove the state machine and the permission rules hold together as one
/// workflow, not just as isolated area checks.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkOrderLifecycleTests(CfiAppApiFactory factory)
{
    private const string Password = "Widnes-Shift-2026!";
    private static int _counter;

    private static string NewEmail() =>
        $"wo{Interlocked.Increment(ref _counter)}-{Guid.NewGuid():N}@example.test";

    private async Task<(HttpClient Client, int UserId)> SignedInAsAsync(string roleName)
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Test User", email, null, Password, "en"));

        int userId;

        await using (var scope = factory.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var user = await context.Users.SingleAsync(x => x.Email == email);
            var role = await context.Roles.SingleAsync(x => x.Name == roleName);
            user.RoleId = role.Id;
            user.Status = UserStatus.Active;
            await context.SaveChangesAsync();
            userId = user.Id;
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, Password, null));
        var tokens = (await login.Content.ReadFromJsonAsync<AuthTokens>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        return (client, userId);
    }

    private static async Task<int> SeededUnitIdAsync(HttpClient client)
    {
        var units = await client.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units");
        return units!.Items.First(x => x.Code == "UNIT1").Id;
    }

    private static async Task<int> UploadPhotoAsync(HttpClient client)
    {
        using var content = new MultipartFormDataContent();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F' };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "photo.jpg");

        var response = await client.PostAsync("/api/v1/media", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var asset = (await response.Content.ReadFromJsonAsync<MediaAssetDto>())!;
        return asset.Id;
    }

    private async Task<int> ReportBreakdownAsync(HttpClient reporterClient, int unitId, bool withPhoto = false)
    {
        var photoIds = withPhoto ? new[] { await UploadPhotoAsync(reporterClient) } : [];

        var response = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, "Conveyor drive motor", Priority.High,
            "Motor overheats and trips out under load.", photoIds));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;
        return created.Id;
    }

    private static CloseWorkOrderRequest NonIntrusiveClosure(bool ableToRepair = true) => new(
        RootCause: "Bearing seized",
        CorrectiveAction: "Replaced bearing and lubricated",
        AbleToRepair: ableToRepair,
        UnableToRepairReason: ableToRepair ? null : "Awaiting a replacement part from the supplier",
        ContractorRequired: false,
        ContractorUsed: null,
        DowntimeMinutes: 45,
        ToolsAndPartsAccounted: true,
        MissingItemsNote: null,
        PostDeodorisationIntervention: false,
        PartsRequired: "6205 bearing",
        PartsPrice: 12.50m,
        LabourCostPerHour: 18m,
        PoNumber: "PO-4471",
        PhotoAssetIds: []);

    private static CloseWorkOrderRequest IntrusiveClosure() => NonIntrusiveClosure() with
    {
        PostDeodorisationIntervention = true
    };

    [Fact]
    public async Task A_report_lands_in_the_pool_and_not_on_the_engineers_own_desk_even_when_they_reported_it()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(engineerClient);

        var workOrderId = await ReportBreakdownAsync(engineerClient, unitId);

        var pool = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/pool");
        pool!.Items.ShouldContain(x => x.Id == workOrderId);

        var mine = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine");
        mine!.Items.ShouldNotContain(x => x.Id == workOrderId, "even the reporter's own job goes to the pool first");
    }

    [Fact]
    public async Task An_operator_can_report_but_cannot_see_the_pool_or_claim_anything()
    {
        var (operatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(operatorClient);

        var workOrderId = await ReportBreakdownAsync(operatorClient, unitId);

        (await operatorClient.GetAsync("/api/v1/workorders/pool")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await operatorClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reporting_without_naming_a_machine_or_describing_it_is_rejected()
    {
        var (operatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(operatorClient);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, null, Priority.Low, "Something is wrong.", []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reporting_with_a_photo_that_was_never_uploaded_is_rejected()
    {
        var (operatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(operatorClient);

        var response = await operatorClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, "Sieve", Priority.Low, "Blocked.", [999_999]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Claiming_puts_a_job_on_the_engineers_list_but_leaves_it_in_the_pool_showing_who_has_it()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, engineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId, withPhoto: true);

        (await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Taking a job does not hide it. It stays in the pool until it is finished, now
        // carrying its status and the name of whoever has it, so the shift can see the job
        // is in hand rather than wondering whether anybody ever looked at it.
        var pool = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/pool");
        var stillListed = pool!.Items.Single(x => x.Id == workOrderId);
        stillListed.Status.ShouldBe(WorkOrderStatus.Accepted);
        stillListed.AssignedEngineerName.ShouldNotBeNullOrWhiteSpace();

        // It is no longer one of the free ones though - that is the separate count the
        // dashboard's "in pool" tile shows.
        var free = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>(
            "/api/v1/workorders/pool?unclaimed=true");
        free!.Items.ShouldNotContain(x => x.Id == workOrderId);

        var mine = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine");
        var claimed = mine!.Items.Single(x => x.Id == workOrderId);
        claimed.ClaimedAt.ShouldNotBeNull();
        claimed.PhotoCount.ShouldBe(1);

        var detail = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.AssignedEngineerName.ShouldNotBeNullOrWhiteSpace();
        detail.Events.ShouldContain(e => e.Type == WorkOrderEventType.Claimed);
        _ = engineerId;
    }

    [Fact]
    public async Task Two_engineers_racing_to_claim_the_same_job_only_one_wins()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (firstEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (secondEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var attempts = new[]
        {
            firstEngineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null),
            secondEngineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null)
        };

        var results = await Task.WhenAll(attempts);
        var statuses = results.Select(x => x.StatusCode).ToArray();

        statuses.ShouldContain(HttpStatusCode.NoContent);
        statuses.Count(x => x == HttpStatusCode.NoContent).ShouldBe(1, "exactly one claim can win the race");
        statuses.ShouldContain(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task An_engineer_can_hold_more_than_one_active_job_at_once()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var first = await ReportBreakdownAsync(reporterClient, unitId);
        var second = await ReportBreakdownAsync(reporterClient, unitId);

        await engineerClient.PostAsync($"/api/v1/workorders/{first}/claim", null);
        await engineerClient.PostAsync($"/api/v1/workorders/{second}/claim", null);

        var mine = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine");
        mine!.Items.ShouldContain(x => x.Id == first);
        mine.Items.ShouldContain(x => x.Id == second);
    }

    [Fact]
    public async Task A_manager_can_assign_directly_and_later_reassign_to_someone_else()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        var (firstEngineerClient, firstEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (_, secondEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var assign = await managerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/assign", new AssignWorkOrderRequest(firstEngineerId));
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mineFirst = await firstEngineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine");
        mineFirst!.Items.ShouldContain(x => x.Id == workOrderId);

        var reassign = await managerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/assign", new AssignWorkOrderRequest(secondEngineerId));
        reassign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var mineFirstAfter = await firstEngineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine");
        mineFirstAfter!.Items.ShouldNotContain(x => x.Id == workOrderId);

        var detail = await managerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.Events.Count(e => e.Type is WorkOrderEventType.Assigned or WorkOrderEventType.Reassigned).ShouldBe(2);
    }

    [Fact]
    public async Task An_engineer_cannot_assign_work_to_someone_else()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (_, otherEngineerId) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var response = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/assign", new AssignWorkOrderRequest(otherEngineerId));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Only_the_assigned_engineer_can_send_a_busy_or_on_my_way_update()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (otherEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        var wrongEngineer = await otherEngineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/notify", new NotifyReporterRequest(EngineerNotificationKind.Busy));
        wrongEngineer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rightEngineer = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/notify", new NotifyReporterRequest(EngineerNotificationKind.OnMyWay));
        rightEngineer.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_job_can_go_to_waiting_parts_and_resume()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/start", null);

        (await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/waiting-parts", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.Status.ShouldBe(WorkOrderStatus.WaitingParts);

        (await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/resume", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_non_intrusive_close_finishes_the_job_and_computes_labour_time_automatically()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        var close = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/close", NonIntrusiveClosure());
        close.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");

        detail!.Status.ShouldBe(WorkOrderStatus.Completed);
        detail.ClosedAt.ShouldNotBeNull();
        // Not manually entered - tracked from claim to close, so this must never be null
        // once a job is closed, even if the elapsed time in a fast test run rounds to zero.
        detail.LabourMinutes.ShouldNotBeNull();
        detail.LabourMinutes!.Value.ShouldBeGreaterThanOrEqualTo(0);
        detail.Closures.Single().DowntimeMinutes.ShouldBe(45, "downtime is what the engineer typed, not the labour time");

        // Finished is the one thing that does take a job off the pool screen.
        var pool = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/pool");
        pool!.Items.ShouldNotContain(x => x.Id == workOrderId);
    }

    [Fact]
    public async Task Closing_without_a_repair_reason_when_unable_to_repair_is_rejected()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        var badRequest = NonIntrusiveClosure() with { AbleToRepair = false, UnableToRepairReason = null };
        var response = await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/close", badRequest);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Someone_who_is_not_the_assigned_engineer_cannot_close_the_job()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (otherEngineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        var response = await otherEngineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/close", NonIntrusiveClosure());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_full_intrusive_loop_fail_then_resubmit_then_pass_closes_the_job_with_two_closure_versions()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (qaClient, _) = await SignedInAsAsync(Permissions.Roles.Qa);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        var firstClose = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/close", IntrusiveClosure());
        firstClose.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterFirstClose = await engineerClient
            .GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        afterFirstClose!.Status.ShouldBe(WorkOrderStatus.AwaitingQa);
        afterFirstClose.ClosedAt.ShouldBeNull("not really closed until QA passes it");

        // QA fails it without a note - rejected, the engineer needs something to act on.
        var failWithoutNote = await qaClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/qa-result", new QaResultRequest(QaResult.Fail, null));
        failWithoutNote.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var fail = await qaClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/qa-result",
            new QaResultRequest(QaResult.Fail, "Swab test detected residue near the seal."));
        fail.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterFail = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        afterFail!.Status.ShouldBe(WorkOrderStatus.QaFailed);

        // A sent-back job stays on the engineer's own list rather than moving to a separate
        // screen, and sorts ahead of newer work because it is the one waiting on them.
        var mine = await engineerClient
            .GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>("/api/v1/workorders/mine?pageSize=100");
        mine!.Items.ShouldContain(x => x.Id == workOrderId);
        mine.Items.First().Id.ShouldBe(workOrderId);

        // The engineer edits and resubmits the same form.
        var resubmit = await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/close",
            IntrusiveClosure() with { CorrectiveAction = "Replaced seal and resanitised the unit" });
        resubmit.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterResubmit = await engineerClient
            .GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        afterResubmit!.Status.ShouldBe(WorkOrderStatus.AwaitingQa);
        afterResubmit.Closures.Count.ShouldBe(2, "the earlier submission is kept, not overwritten");

        var pass = await qaClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/qa-result", new QaResultRequest(QaResult.Pass, null));
        pass.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var final = await engineerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        final!.Status.ShouldBe(WorkOrderStatus.Completed);
        final.ClosedAt.ShouldNotBeNull();
        final.QaChecks.Count.ShouldBe(2, "one failed attempt and one passing attempt");
        final.QaChecks.ShouldContain(x => x.Result == QaResult.Fail);
        final.QaChecks.ShouldContain(x => x.Result == QaResult.Pass);
    }

    [Fact]
    public async Task Production_and_qa_sign_off_can_each_be_recorded_once()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (qaClient, _) = await SignedInAsAsync(Permissions.Roles.Qa);
        var (productionManagerClient, _) = await SignedInAsAsync(Permissions.Roles.ProductionManager);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);
        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);
        await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/close", IntrusiveClosure());
        await qaClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/qa-result", new QaResultRequest(QaResult.Pass, null));

        var productionSignOff = await productionManagerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/sign-off/production", new SignOffRequest(true, true));
        productionSignOff.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var duplicateSignOff = await productionManagerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/sign-off/production", new SignOffRequest(true, true));
        duplicateSignOff.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var engineerAttemptsProductionSignOff = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/sign-off/production", new SignOffRequest(true, true));
        engineerAttemptsProductionSignOff.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var qaSignOff = await qaClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/sign-off/qa", new SignOffRequest(true, true));
        qaSignOff.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await qaClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.SignOffs.Count.ShouldBe(2);
        detail.SignOffs.ShouldContain(x => x.Kind == SignOffKind.Production);
        detail.SignOffs.ShouldContain(x => x.Kind == SignOffKind.Qa);
    }

    [Fact]
    public async Task The_reporter_can_see_their_own_report_detail_without_the_view_all_permission()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var response = await reporterClient.GetAsync($"/api/v1/workorders/{workOrderId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_unrelated_operator_cannot_open_someone_elses_report()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (otherOperatorClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var response = await otherOperatorClient.GetAsync($"/api/v1/workorders/{workOrderId}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Closing_a_job_that_is_still_new_and_unclaimed_is_rejected_as_an_invalid_transition()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var unitId = await SeededUnitIdAsync(reporterClient);

        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        // Never claimed, so AssignedEngineerId is null - this must fail on ownership,
        // not accidentally succeed because everyone "not the owner" is nobody.
        var response = await engineerClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/close", NonIntrusiveClosure());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_uploaded_photo_can_be_downloaded_back_with_its_original_bytes_and_content_type()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var mediaId = await UploadPhotoAsync(client);

        var response = await client.GetAsync($"/api/v1/media/{mediaId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/jpeg");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.ShouldBe([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F']);
    }

    [Fact]
    public async Task Downloading_an_unknown_media_id_is_a_404_not_an_error()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var response = await client.GetAsync("/api/v1/media/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The pool is the claim queue, so it belongs to the people who can claim from it.
    /// QA and the Production Manager can still open any individual job they need to act on.
    /// </summary>
    [Fact]
    public async Task The_pool_is_only_for_the_roles_that_can_claim_from_it()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        (await engineerClient.GetAsync("/api/v1/workorders/pool")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);
        (await managerClient.GetAsync("/api/v1/workorders/pool")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (qaClient, _) = await SignedInAsAsync(Permissions.Roles.Qa);
        (await qaClient.GetAsync("/api/v1/workorders/pool")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var (productionClient, _) = await SignedInAsAsync(Permissions.Roles.ProductionManager);
        (await productionClient.GetAsync("/api/v1/workorders/pool")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// History is the maintenance record: engineers and their manager search it, and the
    /// export is what goes to a BRC auditor. Production and QA do not get the archive.
    /// </summary>
    [Fact]
    public async Task History_and_its_export_are_only_for_maintenance()
    {
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        (await engineerClient.GetAsync("/api/v1/workorders/history")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await engineerClient.GetAsync("/api/v1/workorders/history/export")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var (productionClient, _) = await SignedInAsAsync(Permissions.Roles.ProductionManager);
        (await productionClient.GetAsync("/api/v1/workorders/history")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await productionClient.GetAsync("/api/v1/workorders/history/export")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var (qaClient, _) = await SignedInAsAsync(Permissions.Roles.Qa);
        (await qaClient.GetAsync("/api/v1/workorders/history")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The free-text box is one search over the whole record, not just the reference
    /// number - typing a phrase from the description has to find the job.
    /// </summary>
    [Fact]
    public async Task History_search_matches_a_phrase_from_the_description()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var marker = $"seal ring weeping {Guid.NewGuid():N}";
        var unitId = await SeededUnitIdAsync(reporterClient);

        var created = await reporterClient.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, "Filling head", Priority.Medium, marker, []));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var workOrderId = (await created.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!.Id;

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);
        (await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/close", NonIntrusiveClosure()))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var found = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>(
            $"/api/v1/workorders/history?search={Uri.EscapeDataString("seal ring weeping")}");

        found!.Items.Select(x => x.Id).ShouldContain(workOrderId);
    }

    /// <summary>
    /// The report form cascades its dropdowns, but the API is also what the phone app and
    /// an offline queue post to. A fault log that reads "Yard / Filling Line" is worse than
    /// a rejected report, because nobody can tell afterwards which half was wrong.
    /// </summary>
    [Fact]
    public async Task A_machine_cannot_be_reported_against_a_place_it_does_not_stand_in()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);

        var units = await client.GetFromJsonAsync<PagedResult<UnitDto>>("/api/v1/admin/units");
        var area1 = units!.Items.First(x => x.Code == "UNIT1").Id;
        var yard = units.Items.First(x => x.Code == "YARD").Id;

        var yardMachines = await client.GetFromJsonAsync<PagedResult<EquipmentDto>>(
            $"/api/v1/admin/equipment?unitId={yard}");
        var dafPlant = yardMachines!.Items.First(x => x.Name == "DAF Plant").Id;

        var wrongUnit = await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            area1, null, null, dafPlant, null, Priority.High, "Reported against the wrong unit.", []));

        wrongUnit.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A room from another unit is refused the same way.
        var unit2 = units.Items.First(x => x.Code == "UNIT2").Id;
        var unit2Rooms = await client.GetFromJsonAsync<PagedResult<AreaDto>>(
            $"/api/v1/admin/areas?unitId={unit2}");
        var blendingRoom = unit2Rooms!.Items.First(x => x.Code == "BLENDING").Id;

        var wrongRoom = await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            area1, blendingRoom, null, null, "Something", Priority.Low, "Room from another unit.", []));

        wrongRoom.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The same machine reported against its own unit is accepted.
        var correct = await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            yard, null, null, dafPlant, null, Priority.High, "DAF plant tripped out.", []));

        correct.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    /// <summary>
    /// Cancelling is how a duplicate leaves the pool. It is terminal, it needs a reason,
    /// and it is not a delete - the record and its reason stay for the audit.
    /// </summary>
    [Fact]
    public async Task A_duplicate_report_can_be_rejected_with_a_reason_and_then_nothing_more()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var unitId = await SeededUnitIdAsync(reporterClient);
        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        // An engineer holds workorder.claim but not workorder.cancel.
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        (await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/reject",
            new RejectWorkOrderRequest("Not mine to turn down")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A blank reason is refused: "why did this vanish" is the first audit question.
        (await managerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/reject",
            new RejectWorkOrderRequest("   ")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var rejected = await managerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/reject",
            new RejectWorkOrderRequest("Duplicate of WO-1041"));
        rejected.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await managerClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.Status.ShouldBe(WorkOrderStatus.Rejected);
        detail.Events.ShouldContain(x => x.Summary != null && x.Summary.Contains("Duplicate of WO-1041"));

        // Terminal: it cannot be claimed back out of the pool afterwards.
        (await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A report with nothing typed in the box is still a report. What broke and where is
    /// already on the form and a photograph usually says more than a line typed one-handed
    /// at the machine - and a fault nobody bothers to file is worse than a short one.
    /// </summary>
    [Fact]
    public async Task A_breakdown_can_be_reported_without_typing_a_description()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(client);

        var created = await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, "Sack tipper", Priority.Medium, "", []));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);

        var summary = (await created.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!;
        summary.EquipmentFreeText.ShouldBe("Sack tipper");

        // What broke is still required - a report naming neither a machine nor anything
        // else is not a report.
        (await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, null, Priority.Medium, "", [])))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// The notifications screen carries messages from the managers and nothing else. Every
    /// step of a repair is still written to the log - an auditor can see it, and turning it
    /// back on is one predicate - but it does not appear here: the job card already shows
    /// where a repair has got to, and repeating it buried the messages.
    /// </summary>
    [Fact]
    public async Task The_notifications_screen_carries_messages_and_leaves_job_progress_to_the_job()
    {
        var (reporterClient, reporterId) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);
        var (managerClient, _) = await SignedInAsAsync(Permissions.Roles.MaintenanceManager);

        var unitId = await SeededUnitIdAsync(reporterClient);
        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        var before = await reporterClient
            .GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?pageSize=100");
        var countBefore = before!.TotalCount;

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);

        (await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/notify",
            new NotifyReporterRequest(EngineerNotificationKind.OnMyWay)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/waiting-parts", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Both were recorded...
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
            var logged = await context.NotificationLogs
                .Where(x => x.UserId == reporterId)
                .Select(x => x.Type)
                .ToListAsync();

            logged.ShouldContain("workorder.onMyWay");
            logged.ShouldContain("workorder.waitingParts");
        }

        // ...and neither one reaches the screen.
        var afterJobEvents = await reporterClient
            .GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?pageSize=100");
        afterJobEvents!.TotalCount.ShouldBe(countBefore);

        // A message from a manager does, and it arrives readable: who sent it and what it
        // said, without opening a second screen.
        const string body = "Line 2 is running short-handed tonight.";
        (await managerClient.PostAsJsonAsync("/api/v1/messages",
            new SendMessageRequest(body, MessagePriority.Normal, null, [reporterId], [])))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var afterMessage = await reporterClient
            .GetFromJsonAsync<PagedResult<NotificationDto>>("/api/v1/notifications?pageSize=100");

        afterMessage!.TotalCount.ShouldBe(countBefore + 1);

        var arrived = afterMessage.Items.First();
        arrived.MessageId.ShouldNotBeNull();
        arrived.MessageBody.ShouldBe(body);
        arrived.SenderName.ShouldNotBeNullOrWhiteSpace();

        // Newest first, so the thing that just happened is the thing you see.
        afterMessage.Items.First().SentAt.ShouldBeGreaterThanOrEqualTo(afterMessage.Items.Last().SentAt);

        // The badge counts the same rows the screen lists - a badge promising something
        // that is not there when you tap it is worse than no badge.
        var unseen = await reporterClient
            .GetFromJsonAsync<UnseenNotificationsDto>("/api/v1/notifications/unseen-count");
        unseen!.Count.ShouldBe(afterMessage.TotalCount);
    }

    /// <summary>
    /// The last box on the paper form, and the only one filled in by somebody standing at
    /// the machine: the person who raised the fault accepting that it is fixed.
    ///
    /// They are told to sign when the job closes, only they can sign it, and there is
    /// nothing to sign before the repair is finished.
    /// </summary>
    [Fact]
    public async Task The_person_who_reported_a_fault_signs_to_accept_the_repair()
    {
        var (reporterClient, reporterId) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var unitId = await SeededUnitIdAsync(reporterClient);
        var workOrderId = await ReportBreakdownAsync(reporterClient, unitId);

        // Nothing to accept while the job is still open.
        (await reporterClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/sign-off/reporter",
            new SignOffRequest(true, true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await engineerClient.PostAsync($"/api/v1/workorders/{workOrderId}/claim", null);
        (await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/close", NonIntrusiveClosure()))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Closing asks them to sign. Recorded in the log rather than shown on the
        // notifications screen, which carries only messages.
        await using (var scope = factory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();

            (await context.NotificationLogs.AnyAsync(x =>
                x.UserId == reporterId && x.Type == "workorder.signatureRequested"))
                .ShouldBeTrue("closing the job should ask the reporter to confirm it is fixed");
        }

        // Somebody else cannot accept a repair on their behalf.
        (await engineerClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/sign-off/reporter",
            new SignOffRequest(true, true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var signed = await reporterClient.PostAsJsonAsync(
            $"/api/v1/workorders/{workOrderId}/sign-off/reporter", new SignOffRequest(true, true));
        signed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await reporterClient.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");
        detail!.SignOffs.ShouldContain(x => x.Kind == SignOffKind.Reporter);
        _ = reporterId;

        // Once, not once per visit.
        (await reporterClient.PostAsJsonAsync($"/api/v1/workorders/{workOrderId}/sign-off/reporter",
            new SignOffRequest(true, true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The name-and-position boxes at the top of the paper log. The reporter's position is
    /// stamped from their account; who they told is free text, because on a night shift it
    /// may be somebody with no account at all.
    /// </summary>
    [Fact]
    public async Task A_report_records_who_raised_it_and_who_they_told()
    {
        var (client, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var unitId = await SeededUnitIdAsync(client);

        var created = await client.PostAsJsonAsync("/api/v1/workorders", new CreateWorkOrderRequest(
            unitId, null, null, null, "Sack tipper", Priority.Medium,
            "Guard interlock keeps tripping.", [],
            ReportedToName: "Dave Wilkins", ReportedToPosition: "Shift Supervisor"));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var workOrderId = (await created.Content.ReadFromJsonAsync<WorkOrderSummaryDto>())!.Id;

        var detail = await client.GetFromJsonAsync<WorkOrderDetailDto>($"/api/v1/workorders/{workOrderId}");

        detail!.ReportedToName.ShouldBe("Dave Wilkins");
        detail.ReportedToPosition.ShouldBe("Shift Supervisor");
        detail.ReportedByPosition.ShouldBe(Permissions.Roles.Operator,
            "with no occupation set, the role is what the form's Position box says");
    }

    /// <summary>
    /// A part on order is a one-off notice to the reporter but a standing list for a
    /// manager (notes 03, §3) - they need to see everything still held up, not be told once.
    /// </summary>
    [Fact]
    public async Task Managers_can_list_everything_that_is_waiting_for_a_part()
    {
        var (reporterClient, _) = await SignedInAsAsync(Permissions.Roles.Operator);
        var (engineerClient, _) = await SignedInAsAsync(Permissions.Roles.Engineer);

        var unitId = await SeededUnitIdAsync(reporterClient);
        var held = await ReportBreakdownAsync(reporterClient, unitId);
        var running = await ReportBreakdownAsync(reporterClient, unitId);

        await engineerClient.PostAsync($"/api/v1/workorders/{held}/claim", null);
        (await engineerClient.PostAsync($"/api/v1/workorders/{held}/waiting-parts", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await engineerClient.PostAsync($"/api/v1/workorders/{running}/claim", null);

        var waiting = await engineerClient.GetFromJsonAsync<PagedResult<WorkOrderSummaryDto>>(
            "/api/v1/workorders?waitingParts=true&pageSize=100");

        waiting!.Items.ShouldContain(x => x.Id == held);
        waiting.Items.ShouldNotContain(x => x.Id == running);
    }
}
