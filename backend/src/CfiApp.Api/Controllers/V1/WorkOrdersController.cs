using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Maintenance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Infrastructure.Maintenance;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Breakdown reporting, the pool, claiming, working a job, closing it, the QA loop and
/// sign-off. Business rules live in <see cref="WorkOrderService"/>; this controller only
/// translates HTTP in and out and enforces who is allowed to call what.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workorders")]
[Authorize]
public sealed class WorkOrdersController(
    CfiAppDbContext context,
    WorkOrderService service,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Permissions.WorkOrderCreate)]
    [ProducesResponseType(typeof(WorkOrderSummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<WorkOrderSummaryDto>> Create(
        CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await service.CreateAsync(request, cancellationToken);
        var dto = await LoadSummaryAsync(workOrder.Id, cancellationToken);
        return CreatedAtAction(nameof(Detail), new { id = workOrder.Id }, dto);
    }

    /// <summary>
    /// Every breakdown still live on the floor, free ones first. A job stays here from the
    /// moment it is reported until it is genuinely finished, carrying its own status and
    /// the name of whoever took it, so the pool answers "what is broken right now, and who
    /// has it" instead of hiding a job the moment somebody picks it up - work in hand is
    /// still work the shift needs to see.
    ///
    /// unclaimed=true narrows it to the jobs nobody has taken. That is a different number
    /// and the dashboard counts it separately: "waiting for someone" is not "open".
    /// </summary>
    [HttpGet("pool")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(typeof(PagedResult<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> Pool(
        [FromQuery] bool? unclaimed = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = unclaimed == true
            ? BaseQuery().Where(x => x.Status == WorkOrderStatus.New)
            : BaseQuery().Where(x => !FinishedStatuses.Contains(x.Status));

        return ListAsync(query, page, pageSize, cancellationToken, freeFirst: true);
    }

    /// <summary>
    /// Everything still on the signed-in engineer's plate. A job stays here until it is
    /// genuinely finished, which includes the two QA states: one waiting on a swab test,
    /// and one QA sent back to be fixed. Those are not a separate screen to remember to
    /// check - the card carries its own status, and a sent-back job sorts to the top
    /// because it is the one thing here that is waiting on the engineer.
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(typeof(PagedResult<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> Mine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;

        var openStatuses = new[]
        {
            WorkOrderStatus.Accepted, WorkOrderStatus.InProgress, WorkOrderStatus.WaitingParts,
            WorkOrderStatus.AwaitingQa, WorkOrderStatus.QaFailed
        };

        return ListAsync(
            BaseQuery().Where(x => x.AssignedEngineerId == userId && openStatuses.Contains(x.Status)),
            page, pageSize, cancellationToken,
            needsMeFirst: true);
    }

    /// <summary>What the signed-in user has personally reported, any status.</summary>
    [HttpGet("reported-by-me")]
    [ProducesResponseType(typeof(PagedResult<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> ReportedByMe(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId!.Value;
        return ListAsync(BaseQuery().Where(x => x.ReportedByUserId == userId), page, pageSize, cancellationToken);
    }

    /// <summary>
    /// The full list with filters - managers and QA see everything, not just their own.
    /// "open" is what a Production Manager watches: every breakdown still costing the site
    /// time, from the moment it is reported until it is completed or turned down.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Permissions.WorkOrderViewAll)]
    [ProducesResponseType(typeof(PagedResult<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> List(
        [FromQuery] WorkOrderStatus? status = null,
        [FromQuery] bool? open = null,
        [FromQuery] bool? waitingParts = null,
        [FromQuery] int? unitId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = BaseQuery();

        if (status is not null) query = query.Where(x => x.Status == status);
        if (unitId is not null) query = query.Where(x => x.UnitId == unitId);

        if (open == true)
        {
            query = query.Where(x => !FinishedStatuses.Contains(x.Status));
        }

        if (waitingParts == true)
        {
            query = query.Where(x => x.Status == WorkOrderStatus.WaitingParts);
        }

        return ListAsync(query, page, pageSize, cancellationToken);
    }

    /// <summary>
    /// Everything past active work: closed jobs, and the ones sitting in the QA cycle -
    /// both the engineer's own and everyone else's. Search is one free-text box over the
    /// whole record - reference number, unit, room, machine, what was reported, who
    /// reported it, who fixed it, and the root cause and corrective action written at
    /// closing time - so a half-remembered phrase still finds the record at audit time.
    /// </summary>
    [HttpGet("history")]
    [Authorize(Policy = Permissions.WorkOrderHistory)]
    [ProducesResponseType(typeof(PagedResult<WorkOrderSummaryDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> History(
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        return ListAsync(HistoryQuery(search, from, to), page, pageSize, cancellationToken);
    }

    /// <summary>
    /// The same history list as a CSV download - what a manager hands a BRC auditor who
    /// wants their own copy, or attaches to an internal report. Streamed rather than
    /// buffered: a full year of history should not have to fit in memory to be exported.
    /// </summary>
    [HttpGet("history/export")]
    [Authorize(Policy = Permissions.WorkOrderHistory)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task ExportHistory(
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/csv";
        Response.Headers.ContentDisposition = $"attachment; filename=\"work-order-history-{DateOnly.FromDateTime(DateTime.UtcNow):yyyy-MM-dd}.csv\"";

        await using var writer = new StreamWriter(Response.Body, leaveOpen: true);

        await writer.WriteLineAsync(string.Join(',', [
            "Number", "Status", "Priority", "Unit", "Area", "Equipment", "ReportedBy", "ReportedAtUtc",
            "AssignedEngineer", "ClaimedAtUtc", "ClosedAtUtc", "LabourMinutes", "RootCause", "CorrectiveAction",
            "DowntimeMinutes"
        ]));

        var rows = HistoryQuery(search, from, to)
            .OrderByDescending(x => x.ReportedAt)
            .Select(x => new
            {
                x.Number,
                x.Status,
                x.Priority,
                UnitName = x.Unit!.Name,
                AreaName = x.Area != null ? x.Area.Name : null,
                EquipmentName = x.Equipment != null ? x.Equipment.Name : x.EquipmentFreeText,
                ReportedByName = x.ReportedBy!.FullName,
                x.ReportedAt,
                AssignedEngineerName = x.AssignedEngineer != null ? x.AssignedEngineer.FullName : null,
                x.ClaimedAt,
                x.ClosedAt,
                x.LabourMinutes,
                LatestClosure = x.Closures.OrderByDescending(c => c.Version).FirstOrDefault()
            });

        await foreach (var row in rows.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(string.Join(',', [
                CsvField(row.Number), CsvField(row.Status.ToString()), CsvField(row.Priority.ToString()),
                CsvField(row.UnitName), CsvField(row.AreaName), CsvField(row.EquipmentName),
                CsvField(row.ReportedByName), CsvField(row.ReportedAt.ToString("O")),
                CsvField(row.AssignedEngineerName), CsvField(row.ClaimedAt?.ToString("O")),
                CsvField(row.ClosedAt?.ToString("O")), CsvField(row.LabourMinutes?.ToString()),
                CsvField(row.LatestClosure?.RootCause), CsvField(row.LatestClosure?.CorrectiveAction),
                CsvField(row.LatestClosure?.DowntimeMinutes.ToString())
            ]));
        }
    }

    private IQueryable<WorkOrder> HistoryQuery(string? search, DateTimeOffset? from, DateTimeOffset? to)
    {
        var historyStatuses = new[]
        {
            WorkOrderStatus.Completed, WorkOrderStatus.AwaitingQa, WorkOrderStatus.QaFailed, WorkOrderStatus.Rejected
        };

        var query = BaseQuery().Where(x => historyStatuses.Contains(x.Status));

        if (from is not null) query = query.Where(x => x.ReportedAt >= from);
        if (to is not null) query = query.Where(x => x.ReportedAt <= to);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Plain ToLower/Contains rather than a provider-specific ILIKE: this must keep
            // working if the database ever moves from PostgreSQL to SQL Server.
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.Number.ToLower().Contains(term) ||
                x.Unit!.Name.ToLower().Contains(term) ||
                (x.Area != null && x.Area.Name.ToLower().Contains(term)) ||
                (x.Equipment != null && x.Equipment.Name.ToLower().Contains(term)) ||
                (x.EquipmentFreeText != null && x.EquipmentFreeText.ToLower().Contains(term)) ||
                x.Description.ToLower().Contains(term) ||
                x.ReportedBy!.FullName.ToLower().Contains(term) ||
                (x.AssignedEngineer != null && x.AssignedEngineer.FullName.ToLower().Contains(term)) ||
                x.Closures.Any(c =>
                    c.RootCause.ToLower().Contains(term) ||
                    c.CorrectiveAction.ToLower().Contains(term)));
        }

        return query;
    }

    /// <summary>Quotes a field only when it needs it, and escapes embedded quotes - a root
    /// cause note written in free text can easily contain a comma or a newline.</summary>
    private static string CsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var needsQuoting = value.IndexOfAny([',', '"', '\n', '\r']) >= 0;
        if (!needsQuoting) return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(WorkOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<WorkOrderDetailDto>> Detail(int id, CancellationToken cancellationToken)
    {
        var workOrder = await context.WorkOrders.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (workOrder is null) return NotFound();

        var userId = currentUser.UserId!.Value;
        var canViewAll = User.HasClaim(CfiApp.Api.Security.CfiClaimTypes.Permission, Permissions.WorkOrderViewAll);
        var isOwner = workOrder.ReportedByUserId == userId || workOrder.AssignedEngineerId == userId;

        if (!canViewAll && !isOwner)
        {
            return Problem(
                title: "You do not have access to this work order",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Ok(await LoadDetailAsync(id, cancellationToken));
    }

    [HttpPost("{id:int}/claim")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Claim(int id, CancellationToken cancellationToken)
    {
        await service.ClaimAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Policy = Permissions.WorkOrderAssign)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Assign(int id, AssignWorkOrderRequest request, CancellationToken cancellationToken)
    {
        await service.AssignAsync(id, request.EngineerUserId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/notify")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Notify(int id, NotifyReporterRequest request, CancellationToken cancellationToken)
    {
        await service.NotifyReporterAsync(id, request.Kind, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/start")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Start(int id, CancellationToken cancellationToken)
    {
        await service.StartWorkAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/waiting-parts")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> WaitingParts(int id, CancellationToken cancellationToken)
    {
        await service.MarkWaitingPartsAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/resume")]
    [Authorize(Policy = Permissions.WorkOrderClaim)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Resume(int id, CancellationToken cancellationToken)
    {
        await service.ResumeFromWaitingPartsAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = Permissions.WorkOrderClose)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Close(int id, CloseWorkOrderRequest request, CancellationToken cancellationToken)
    {
        await service.CloseAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Permissions.WorkOrderReject)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(int id, RejectWorkOrderRequest request, CancellationToken cancellationToken)
    {
        await service.RejectAsync(id, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/qa-result")]
    [Authorize(Policy = Permissions.QaCheck)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> QaResult(int id, QaResultRequest request, CancellationToken cancellationToken)
    {
        await service.RecordQaResultAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// The reporter accepting the repair: the machine is running and the area is fit to
    /// use. Anyone can call this, but only for a job they raised themselves.
    /// </summary>
    [HttpPost("{id:int}/sign-off/reporter")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SignOffReporter(
        int id, SignOffRequest request, CancellationToken cancellationToken)
    {
        await service.SignOffAsync(id, SignOffKind.Reporter, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/sign-off/production")]
    [Authorize(Policy = Permissions.ProductionSignOff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOffProduction(
        int id, SignOffRequest request, CancellationToken cancellationToken)
    {
        await service.SignOffAsync(id, SignOffKind.Production, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/sign-off/qa")]
    [Authorize(Policy = Permissions.QaSignOff)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOffQa(int id, SignOffRequest request, CancellationToken cancellationToken)
    {
        await service.SignOffAsync(id, SignOffKind.Qa, request, cancellationToken);
        return NoContent();
    }

    // ---------------------------------------------------------------- queries

    /// <summary>The two ways a job stops being live work: it was done, or it was turned down.</summary>
    private static readonly WorkOrderStatus[] FinishedStatuses =
        [WorkOrderStatus.Completed, WorkOrderStatus.Rejected];

    private IQueryable<WorkOrder> BaseQuery() => context.WorkOrders.AsNoTracking();

    private static IQueryable<WorkOrderSummaryDto> ToSummaries(IQueryable<WorkOrder> query) =>
        query.Select(x => new WorkOrderSummaryDto(
            x.Id, x.Number, x.JobType, x.Unit!.Name, x.Area != null ? x.Area.Name : null,
            x.Line != null ? x.Line.Name : null, x.Equipment != null ? x.Equipment.Name : null,
            x.Equipment != null && x.Equipment.ParentEquipment != null
                ? x.Equipment.ParentEquipment.Name
                : null,
            x.EquipmentFreeText, x.Priority, x.Status, x.ReportedBy!.FullName, x.ReportedAt,
            x.AssignedEngineer != null ? x.AssignedEngineer.FullName : null, x.ClaimedAt,
            x.Photos.Count));

    private async Task<WorkOrderSummaryDto> LoadSummaryAsync(int id, CancellationToken cancellationToken) =>
        await ToSummaries(BaseQuery().Where(x => x.Id == id)).FirstAsync(cancellationToken);

    private async Task<WorkOrderDetailDto> LoadDetailAsync(int id, CancellationToken cancellationToken)
    {
        var workOrder = await context.WorkOrders.AsNoTracking()
            .Include(x => x.Unit)
            .Include(x => x.Area)
            .Include(x => x.Line)
            .Include(x => x.Equipment).ThenInclude(x => x!.ParentEquipment)
            .Include(x => x.ReportedBy)
            .Include(x => x.AssignedEngineer)
            .Include(x => x.Photos)
            .Include(x => x.Events).ThenInclude(x => x.Actor)
            .Include(x => x.Closures).ThenInclude(x => x.SubmittedBy)
            .Include(x => x.QaChecks).ThenInclude(x => x.ResultBy)
            .Include(x => x.SignOffs).ThenInclude(x => x.User)
            .FirstAsync(x => x.Id == id, cancellationToken);

        return new WorkOrderDetailDto(
            workOrder.Id, workOrder.Number, workOrder.JobType, workOrder.Unit!.Name, workOrder.Area?.Name,
            workOrder.Line?.Name,
            workOrder.Equipment?.Name, workOrder.Equipment?.ParentEquipment?.Name,
            workOrder.EquipmentFreeText, workOrder.Priority, workOrder.Description,
            workOrder.Status, workOrder.ReportedByUserId, workOrder.ReportedBy!.FullName,
            workOrder.ReportedByPosition, workOrder.ReportedToName, workOrder.ReportedToPosition,
            workOrder.ReportedAt,
            workOrder.AssignedEngineerId, workOrder.AssignedEngineer?.FullName, workOrder.ClaimedAt,
            workOrder.ClosedAt, workOrder.LabourMinutes,
            [.. workOrder.Photos.Select(p => new WorkOrderPhotoDto(p.Id, p.MediaAssetId, p.Category, p.CreatedAt))],
            [.. workOrder.Events.OrderBy(e => e.OccurredAt)
                .Select(e => new WorkOrderEventDto(e.Id, e.Type, e.Actor?.FullName, e.OccurredAt, e.Summary))],
            [.. workOrder.Closures.OrderBy(c => c.Version).Select(c => new WorkOrderClosureDto(
                c.Version, c.RootCause, c.CorrectiveAction, c.AbleToRepair, c.UnableToRepairReason,
                c.ContractorRequired, c.ContractorUsed, c.DowntimeMinutes, c.ToolsAndPartsAccounted,
                c.MissingItemsNote, c.PostDeodorisationIntervention, c.SubmittedBy!.FullName, c.SubmittedAt))],
            [.. workOrder.QaChecks.OrderBy(q => q.Attempt).Select(q => new QaCheckDto(
                q.Attempt, q.Result, q.RequestedAt, q.ResultedAt, q.ResultBy?.FullName, q.Note))],
            [.. workOrder.SignOffs.Select(s => new SignOffDto(
                s.Kind, s.User!.FullName, s.SignedAt, s.AreaCleanAndTidy, s.ReleasedBackIntoService,
                s.SignatureAssetId is not null))]);
    }

    private async Task<ActionResult<PagedResult<WorkOrderSummaryDto>>> ListAsync(
        IQueryable<WorkOrder> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        bool needsMeFirst = false,
        bool freeFirst = false)
    {
        // Newest first everywhere, with two exceptions. On a personal list a job QA has
        // sent back outranks a newer one: it is already late and somebody is waiting on it.
        // In the pool a job nobody has taken outranks one that already has an engineer -
        // an engineer scanning for work should not have to read past jobs in hand.
        if (needsMeFirst)
        {
            query = query
                .OrderByDescending(x => x.Status == WorkOrderStatus.QaFailed)
                .ThenByDescending(x => x.ReportedAt);
        }
        else if (freeFirst)
        {
            query = query
                .OrderByDescending(x => x.Status == WorkOrderStatus.New)
                .ThenByDescending(x => x.ReportedAt);
        }
        else
        {
            query = query.OrderByDescending(x => x.ReportedAt);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await ToSummaries(query
                .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
                .Take(PagedResult.NormalisePageSize(pageSize)))
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkOrderSummaryDto>(items, total, page, pageSize);
    }
}
