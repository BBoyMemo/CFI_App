using CfiApp.Application.Abstractions;
using CfiApp.Application.Maintenance;
using CfiApp.Domain.Identity;
using CfiApp.Domain.Maintenance;
using CfiApp.Domain.Messaging;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Infrastructure.Maintenance;

/// <summary>
/// The breakdown lifecycle: report, pool, claim, work, close, the QA loop, sign-off.
///
/// Every state change goes through <see cref="WorkOrderStateMachine"/> and is written to
/// <see cref="WorkOrderEvent"/> - that table is what a BRC auditor is shown, so nothing
/// here changes status without logging why. Concurrency is not hand-rolled: WorkOrder
/// carries the same xmin token every audited entity gets, so two engineers claiming the
/// same job at once produces a DbUpdateConcurrencyException for the loser rather than a
/// silent double-claim.
/// </summary>
public sealed class WorkOrderService(CfiAppDbContext context, IClock clock, ICurrentUser currentUser)
{
    public async Task<WorkOrder> CreateAsync(CreateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (!await context.Units.AnyAsync(x => x.Id == request.UnitId, cancellationToken))
        {
            throw new UnknownReferenceException("Unknown unit.");
        }

        // Each level has to belong to the one above it. The web form cascades its dropdowns
        // so it cannot produce a mismatch, but the API is also what the phone app and any
        // offline queue post to, and "Yard / Filling Line" in the fault log is worse than
        // a rejected report - an auditor cannot tell which half was the typo.
        if (request.AreaId is not null &&
            !await context.Areas.AnyAsync(
                x => x.Id == request.AreaId && x.UnitId == request.UnitId, cancellationToken))
        {
            throw new UnknownReferenceException("That room is not in the selected unit.");
        }

        if (request.LineId is not null &&
            !await context.Lines.AnyAsync(
                x => x.Id == request.LineId && x.UnitId == request.UnitId, cancellationToken))
        {
            throw new UnknownReferenceException("That line is not in the selected unit.");
        }

        if (request.EquipmentId is not null &&
            !await context.Equipment.AnyAsync(
                x => x.Id == request.EquipmentId
                     && x.UnitId == request.UnitId
                     && (request.AreaId == null || x.AreaId == request.AreaId),
                cancellationToken))
        {
            throw new UnknownReferenceException("That machine is not in the selected place.");
        }

        await ValidatePhotoAssetsAsync(request.PhotoAssetIds, cancellationToken);

        var now = clock.UtcNow;
        var userId = currentUser.UserId!.Value;

        // A row needs a Number to satisfy the NOT NULL + unique constraint before its own
        // Id exists, so it is inserted with a collision-proof placeholder and renamed to
        // "WO-{id}" immediately after - the two-step avoids sequences or raw SQL while
        // guaranteeing no two work orders can ever race for the same number.
        var workOrder = new WorkOrder
        {
            Number = $"WO-PENDING-{Guid.NewGuid():N}"[..20],
            UnitId = request.UnitId,
            AreaId = request.AreaId,
            LineId = request.LineId,
            EquipmentId = request.EquipmentId,
            EquipmentFreeText = string.IsNullOrWhiteSpace(request.EquipmentFreeText)
                ? null
                : request.EquipmentFreeText.Trim(),
            ReportedByUserId = userId,
            // A snapshot, not a lookup: the log records the position somebody held on the
            // day, and a change of job next year must not rewrite last year's report.
            ReportedByPosition = await context.Users
                .Where(x => x.Id == userId)
                .Select(x => x.Occupation != null ? x.Occupation.Name : x.Role!.Name)
                .FirstOrDefaultAsync(cancellationToken),
            ReportedToName = string.IsNullOrWhiteSpace(request.ReportedToName)
                ? null
                : request.ReportedToName.Trim(),
            ReportedToPosition = string.IsNullOrWhiteSpace(request.ReportedToPosition)
                ? null
                : request.ReportedToPosition.Trim(),
            ReportedAt = now,
            Priority = request.Priority,
            Description = request.Description.Trim(),
            Status = WorkOrderStatus.New
        };

        AttachPhotos(workOrder, request.PhotoAssetIds, PhotoCategory.Report);

        context.WorkOrders.Add(workOrder);
        await context.SaveChangesAsync(cancellationToken);

        workOrder.Number = $"WO-{workOrder.Id}";
        AddEvent(workOrder, WorkOrderEventType.Created, userId, now, "Breakdown reported.", null, workOrder.Status);
        await context.SaveChangesAsync(cancellationToken);

        return workOrder;
    }

    /// <summary>
    /// Taken from the pool. Even the reporter's own manager or the reporting engineer
    /// cannot skip this by design - every report lands in the pool, no exceptions.
    /// </summary>
    public async Task ClaimAsync(int workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        WorkOrderStateMachine.EnsureCanTransition(workOrder.Status, WorkOrderStatus.Accepted);

        var from = workOrder.Status;
        workOrder.AssignedEngineerId = userId;
        workOrder.ClaimedAt = now;
        workOrder.Status = WorkOrderStatus.Accepted;

        AddEvent(workOrder, WorkOrderEventType.Claimed, userId, now, "Claimed from the pool.", from, workOrder.Status);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// A manager assigning directly bypasses the pool for a fresh job, or hands an
    /// already-claimed job to a different engineer - the status does not change either
    /// way, only who is responsible for it.
    /// </summary>
    public async Task AssignAsync(int workOrderId, int engineerUserId, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);

        if (!await context.Users.AnyAsync(x => x.Id == engineerUserId, cancellationToken))
        {
            throw new UnknownReferenceException("Unknown engineer.");
        }

        var now = clock.UtcNow;
        var wasUnclaimed = workOrder.Status == WorkOrderStatus.New;

        if (wasUnclaimed)
        {
            WorkOrderStateMachine.EnsureCanTransition(workOrder.Status, WorkOrderStatus.Accepted);
        }
        else if (WorkOrderStateMachine.IsTerminal(workOrder.Status))
        {
            throw new InvalidWorkOrderTransitionException(workOrder.Status, WorkOrderStatus.Accepted);
        }

        var previousEngineerId = workOrder.AssignedEngineerId;
        var from = workOrder.Status;

        workOrder.AssignedEngineerId = engineerUserId;
        workOrder.ClaimedAt ??= now;

        if (wasUnclaimed)
        {
            workOrder.Status = WorkOrderStatus.Accepted;
        }

        var eventType = previousEngineerId is null ? WorkOrderEventType.Assigned : WorkOrderEventType.Reassigned;
        var summary = previousEngineerId is null
            ? "Assigned to an engineer by a manager."
            : "Reassigned to a different engineer by a manager.";

        AddEvent(workOrder, eventType, currentUser.UserId, now, summary, from, workOrder.Status);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>"I'm busy" or "on my way" - one line, sent to whoever reported the job.</summary>
    public async Task NotifyReporterAsync(
        int workOrderId, EngineerNotificationKind kind, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);
        var userId = currentUser.UserId!.Value;

        if (workOrder.AssignedEngineerId != userId)
        {
            throw new WorkOrderForbiddenException("Only the engineer assigned to this job can send this update.");
        }

        var now = clock.UtcNow;
        var message = kind == EngineerNotificationKind.Busy
            ? "The engineer is busy and will get to your report as soon as possible."
            : "The engineer is on their way to your report.";

        AddEvent(workOrder, WorkOrderEventType.ReporterNotified, userId, now, message, workOrder.Status, workOrder.Status);

        context.NotificationLogs.Add(new NotificationLog
        {
            UserId = workOrder.ReportedByUserId,
            Type = kind == EngineerNotificationKind.Busy ? "workorder.busy" : "workorder.onMyWay",
            SentAt = now,
            Delivery = NotificationDelivery.Sent
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task StartWorkAsync(int workOrderId, CancellationToken cancellationToken) =>
        await TransitionAsAssignedEngineerAsync(
            workOrderId, WorkOrderStatus.InProgress, "Work started.", cancellationToken);

    public async Task MarkWaitingPartsAsync(int workOrderId, CancellationToken cancellationToken)
    {
        var workOrder = await TransitionAsAssignedEngineerAsync(
            workOrderId, WorkOrderStatus.WaitingParts, "Waiting for parts.", cancellationToken);

        // A one-off notice, not a running status the reporter has to keep checking -
        // managers track the waiting-parts list separately and continuously instead.
        context.NotificationLogs.Add(new NotificationLog
        {
            UserId = workOrder.ReportedByUserId,
            Type = "workorder.waitingParts",
            SentAt = clock.UtcNow,
            Delivery = NotificationDelivery.Sent
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeFromWaitingPartsAsync(int workOrderId, CancellationToken cancellationToken) =>
        await TransitionAsAssignedEngineerAsync(
            workOrderId, WorkOrderStatus.InProgress, "Parts arrived, work resumed.", cancellationToken);

    /// <summary>
    /// Turns down a report that should not have been raised - a duplicate of one already
    /// in the pool, not a fault at all, or not maintenance's to fix.
    ///
    /// Rejecting is terminal and the reason is required, because "why did this disappear"
    /// is the first question an auditor asks about a job that never got fixed. Nothing is
    /// deleted: the record stays, marked rejected, with the reason in its own event.
    /// </summary>
    public async Task RejectAsync(int workOrderId, string reason, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);

        WorkOrderStateMachine.EnsureCanTransition(workOrder.Status, WorkOrderStatus.Rejected);

        var from = workOrder.Status;
        workOrder.Status = WorkOrderStatus.Rejected;

        AddEvent(
            workOrder, WorkOrderEventType.Rejected, currentUser.UserId, clock.UtcNow,
            $"Rejected: {reason.Trim()}", from, WorkOrderStatus.Rejected);

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<WorkOrder> TransitionAsAssignedEngineerAsync(
        int workOrderId, WorkOrderStatus target, string summary, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);
        var userId = currentUser.UserId!.Value;

        if (workOrder.AssignedEngineerId != userId)
        {
            throw new WorkOrderForbiddenException("Only the engineer assigned to this job can do this.");
        }

        WorkOrderStateMachine.EnsureCanTransition(workOrder.Status, target);

        var from = workOrder.Status;
        workOrder.Status = target;

        AddEvent(workOrder, WorkOrderEventType.StatusChanged, userId, clock.UtcNow, summary, from, target);
        await context.SaveChangesAsync(cancellationToken);

        return workOrder;
    }

    /// <summary>
    /// Submits (or, after a QA failure, resubmits) the closure form. Intrusive work goes
    /// to QA; everything else closes immediately. Labour time and the closed-at stamp are
    /// only ever set the moment the job actually reaches Closed, not on an interim
    /// submission that still has to pass QA.
    /// </summary>
    public async Task CloseAsync(int workOrderId, CloseWorkOrderRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);
        var userId = currentUser.UserId!.Value;

        if (workOrder.AssignedEngineerId != userId)
        {
            throw new WorkOrderForbiddenException("Only the engineer assigned to this job can close it.");
        }

        var target = request.PostDeodorisationIntervention ? WorkOrderStatus.AwaitingQa : WorkOrderStatus.Completed;
        WorkOrderStateMachine.EnsureCanTransition(workOrder.Status, target);

        await ValidatePhotoAssetsAsync(request.PhotoAssetIds, cancellationToken);

        var now = clock.UtcNow;
        var from = workOrder.Status;

        var nextVersion = await context.WorkOrderClosures
            .Where(x => x.WorkOrderId == workOrderId)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) is { } maxVersion ? maxVersion + 1 : 1;

        var closure = new WorkOrderClosure
        {
            WorkOrderId = workOrderId,
            Version = nextVersion,
            RootCause = request.RootCause.Trim(),
            CorrectiveAction = request.CorrectiveAction.Trim(),
            AbleToRepair = request.AbleToRepair,
            UnableToRepairReason = Trimmed(request.UnableToRepairReason),
            ContractorRequired = request.ContractorRequired,
            ContractorUsed = Trimmed(request.ContractorUsed),
            // Production downtime, entered by the engineer - a distinct figure from
            // WorkOrder.LabourMinutes below, which is the automatic claim-to-close time.
            DowntimeMinutes = request.DowntimeMinutes,
            ToolsAndPartsAccounted = request.ToolsAndPartsAccounted,
            MissingItemsNote = Trimmed(request.MissingItemsNote),
            PostDeodorisationIntervention = request.PostDeodorisationIntervention,
            SubmittedByUserId = userId,
            SubmittedAt = now
        };

        context.WorkOrderClosures.Add(closure);

        var closingPhotos = request.PhotoAssetIds
            .Select(assetId => new WorkOrderPhoto
            {
                WorkOrderId = workOrderId,
                MediaAssetId = assetId,
                Category = PhotoCategory.Closing
            });

        context.WorkOrderPhotos.AddRange(closingPhotos);

        await UpsertCostAsync(workOrderId, request, cancellationToken);

        workOrder.Status = target;

        if (target == WorkOrderStatus.AwaitingQa)
        {
            var nextAttempt = await context.QaChecks
                .Where(x => x.WorkOrderId == workOrderId)
                .Select(x => (int?)x.Attempt)
                .MaxAsync(cancellationToken) is { } maxAttempt ? maxAttempt + 1 : 1;

            context.QaChecks.Add(new QaCheck
            {
                WorkOrderId = workOrderId,
                Attempt = nextAttempt,
                RequestedAt = now,
                Result = QaResult.Pending
            });

            AddEvent(workOrder, WorkOrderEventType.QaRequested, userId, now,
                "Closure submitted for intrusive work; sent to QA for a swab test.", from, target);
        }
        else
        {
            FinaliseClosure(workOrder, userId, now);
            AddEvent(workOrder, WorkOrderEventType.ClosureSubmitted, userId, now, "Closure submitted.", from, target);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Pass closes the job; fail sends it back to the engineer to fix and resubmit.</summary>
    public async Task RecordQaResultAsync(int workOrderId, QaResultRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);

        if (workOrder.Status != WorkOrderStatus.AwaitingQa)
        {
            throw new InvalidWorkOrderTransitionException(workOrder.Status, WorkOrderStatus.Completed);
        }

        var qaCheck = await context.QaChecks
            .Where(x => x.WorkOrderId == workOrderId && x.Result == QaResult.Pending)
            .OrderByDescending(x => x.Attempt)
            .FirstOrDefaultAsync(cancellationToken);

        if (qaCheck is null)
        {
            throw new InvalidWorkOrderTransitionException(workOrder.Status, WorkOrderStatus.Completed);
        }

        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;
        var from = workOrder.Status;

        qaCheck.Result = request.Result;
        qaCheck.ResultedAt = now;
        qaCheck.ResultByUserId = userId;
        qaCheck.Note = Trimmed(request.Note);

        if (request.Result == QaResult.Pass)
        {
            workOrder.Status = WorkOrderStatus.Completed;
            FinaliseClosure(workOrder, workOrder.AssignedEngineerId ?? userId, now);
            AddEvent(workOrder, WorkOrderEventType.QaPassed, userId, now, "QA passed. Job closed.", from, workOrder.Status);
        }
        else
        {
            workOrder.Status = WorkOrderStatus.QaFailed;
            AddEvent(workOrder, WorkOrderEventType.QaFailed, userId, now,
                $"QA failed: {qaCheck.Note}", from, workOrder.Status);

            context.NotificationLogs.Add(new NotificationLog
            {
                UserId = workOrder.AssignedEngineerId ?? userId,
                Type = "workorder.qaFailed",
                SentAt = now,
                Delivery = NotificationDelivery.Sent
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SignOffAsync(
        int workOrderId, SignOffKind kind, SignOffRequest request, CancellationToken cancellationToken)
    {
        var workOrder = await LoadAsync(workOrderId, cancellationToken);
        var userId = currentUser.UserId!.Value;
        var now = clock.UtcNow;

        if (kind == SignOffKind.Reporter)
        {
            if (workOrder.ReportedByUserId != userId)
            {
                throw new WorkOrderForbiddenException(
                    "Only the person who reported this fault can accept the repair.");
            }

            if (workOrder.Status != WorkOrderStatus.Completed)
            {
                throw new WorkOrderForbiddenException(
                    "There is nothing to accept yet - the repair is not finished.");
            }
        }

        var alreadySigned = await context.SignOffs
            .AnyAsync(x => x.WorkOrderId == workOrderId && x.Kind == kind, cancellationToken);

        if (alreadySigned)
        {
            throw new WorkOrderForbiddenException($"This job already has a {kind} sign-off.");
        }

        var signatureAssetId = await context.UserSignatures
            .Where(x => x.UserId == userId && x.ReplacedAt == null)
            .Select(x => (int?)x.MediaAssetId)
            .FirstOrDefaultAsync(cancellationToken);

        context.SignOffs.Add(new SignOff
        {
            WorkOrderId = workOrderId,
            Kind = kind,
            UserId = userId,
            SignedAt = now,
            SignatureAssetId = signatureAssetId,
            AreaCleanAndTidy = request.AreaCleanAndTidy,
            ReleasedBackIntoService = request.ReleasedBackIntoService
        });

        AddEvent(workOrder, WorkOrderEventType.SignedOff, userId, now, $"{kind} sign-off recorded.",
            workOrder.Status, workOrder.Status);

        await context.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<WorkOrder> LoadAsync(int workOrderId, CancellationToken cancellationToken) =>
        await context.WorkOrders.FirstOrDefaultAsync(x => x.Id == workOrderId, cancellationToken)
            ?? throw new WorkOrderNotFoundException(workOrderId);

    /// <summary>
    /// Closing the job is the engineering side finishing. The person who raised it is then
    /// asked to sign that the machine is actually working again - the last box on the paper
    /// form, and the only one filled in by somebody standing at the machine.
    ///
    /// The signature does not gate the Closed status. If it did, an engineer's labour clock
    /// would keep running while they waited for an operator to pick up their phone, and
    /// that number goes to a BRC auditor.
    /// </summary>
    private void FinaliseClosure(WorkOrder workOrder, int closedByUserId, DateTimeOffset now)
    {
        workOrder.ClosedAt = now;
        workOrder.ClosedByUserId = closedByUserId;
        workOrder.LabourMinutes = ClaimedMinutesElapsed(workOrder, now);

        context.NotificationLogs.Add(new NotificationLog
        {
            UserId = workOrder.ReportedByUserId,
            Type = "workorder.signatureRequested",
            SentAt = now,
            Delivery = NotificationDelivery.Sent
        });
    }

    private static int ClaimedMinutesElapsed(WorkOrder workOrder, DateTimeOffset now) =>
        workOrder.ClaimedAt is { } claimedAt ? (int)Math.Max(0, (now - claimedAt).TotalMinutes) : 0;

    private async Task UpsertCostAsync(int workOrderId, CloseWorkOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.PartsRequired is null && request.PartsPrice is null &&
            request.LabourCostPerHour is null && request.PoNumber is null)
        {
            return;
        }

        var cost = await context.WorkOrderCosts.FirstOrDefaultAsync(x => x.WorkOrderId == workOrderId, cancellationToken);

        if (cost is null)
        {
            cost = new WorkOrderCost { WorkOrderId = workOrderId };
            context.WorkOrderCosts.Add(cost);
        }

        cost.PartsRequired = Trimmed(request.PartsRequired);
        cost.PartsPrice = request.PartsPrice;
        cost.LabourCostPerHour = request.LabourCostPerHour;
        cost.PoNumber = Trimmed(request.PoNumber);
    }

    private async Task ValidatePhotoAssetsAsync(IReadOnlyCollection<int> photoAssetIds, CancellationToken cancellationToken)
    {
        if (photoAssetIds.Count == 0) return;

        if (photoAssetIds.Count > CfiApp.Application.Abstractions.UploadPolicy.MaxPhotosPerWorkOrder)
        {
            throw new UnknownReferenceException(
                $"No more than {CfiApp.Application.Abstractions.UploadPolicy.MaxPhotosPerWorkOrder} photos per submission.");
        }

        var distinctIds = photoAssetIds.Distinct().ToArray();
        var existingCount = await context.MediaAssets.CountAsync(x => distinctIds.Contains(x.Id), cancellationToken);

        if (existingCount != distinctIds.Length)
        {
            throw new UnknownReferenceException("One or more photos were not found. Upload them again.");
        }
    }

    private void AttachPhotos(WorkOrder workOrder, IReadOnlyCollection<int> photoAssetIds, PhotoCategory category)
    {
        foreach (var assetId in photoAssetIds.Distinct())
        {
            workOrder.Photos.Add(new WorkOrderPhoto { MediaAssetId = assetId, Category = category });
        }
    }

    private void AddEvent(
        WorkOrder workOrder,
        WorkOrderEventType type,
        int? actorUserId,
        DateTimeOffset occurredAt,
        string summary,
        WorkOrderStatus? from,
        WorkOrderStatus? to)
    {
        context.WorkOrderEvents.Add(new WorkOrderEvent
        {
            WorkOrderId = workOrder.Id,
            Type = type,
            ActorUserId = actorUserId,
            OccurredAt = occurredAt,
            Summary = summary,
            FromStatus = from,
            ToStatus = to
        });
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
