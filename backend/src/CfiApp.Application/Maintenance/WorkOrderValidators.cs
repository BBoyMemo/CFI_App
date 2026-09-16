using FluentValidation;

namespace CfiApp.Application.Maintenance;

public sealed class CreateWorkOrderRequestValidator : AbstractValidator<CreateWorkOrderRequest>
{
    public CreateWorkOrderRequestValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);

        // Not required. What broke and where is already on the form, and a photograph says
        // more than a line typed one-handed at the machine - a report nobody bothers to
        // file is worse than a short one. The column stays non-null, so this arrives empty.
        RuleFor(x => x.Description).NotNull().MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.PhotoAssetIds).NotNull();

        // Mirrors the CK_WorkOrder_EquipmentIdentified database constraint: a report that
        // names neither a known machine nor a free text description is not a usable report.
        RuleFor(x => x)
            .Must(x => x.EquipmentId is not null || !string.IsNullOrWhiteSpace(x.EquipmentFreeText))
            .WithMessage("Pick a machine from the list, or describe it if it is not listed.")
            .OverridePropertyName(nameof(CreateWorkOrderRequest.EquipmentFreeText));

        RuleFor(x => x.EquipmentFreeText).MaximumLength(200);
    }
}

public sealed class RejectWorkOrderRequestValidator : AbstractValidator<RejectWorkOrderRequest>
{
    public RejectWorkOrderRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why this report is being turned down.")
            .MaximumLength(500);
    }
}

public sealed class AssignWorkOrderRequestValidator : AbstractValidator<AssignWorkOrderRequest>
{
    public AssignWorkOrderRequestValidator()
    {
        RuleFor(x => x.EngineerUserId).GreaterThan(0);
    }
}

public sealed class CloseWorkOrderRequestValidator : AbstractValidator<CloseWorkOrderRequest>
{
    public CloseWorkOrderRequestValidator()
    {
        RuleFor(x => x.RootCause).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.CorrectiveAction).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.UnableToRepairReason).MaximumLength(2000);
        RuleFor(x => x.ContractorUsed).MaximumLength(200);
        RuleFor(x => x.MissingItemsNote).MaximumLength(2000);
        RuleFor(x => x.PoNumber).MaximumLength(60);
        RuleFor(x => x.PartsRequired).MaximumLength(2000);
        RuleFor(x => x.DowntimeMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PartsPrice).GreaterThanOrEqualTo(0).When(x => x.PartsPrice is not null);
        RuleFor(x => x.LabourCostPerHour).GreaterThanOrEqualTo(0).When(x => x.LabourCostPerHour is not null);

        RuleFor(x => x)
            .Must(x => x.AbleToRepair || !string.IsNullOrWhiteSpace(x.UnableToRepairReason))
            .WithMessage("Explain why the job could not be repaired.")
            .OverridePropertyName(nameof(CloseWorkOrderRequest.UnableToRepairReason));

        RuleFor(x => x)
            .Must(x => !x.ContractorRequired || !string.IsNullOrWhiteSpace(x.ContractorUsed))
            .WithMessage("Name the contractor that was used.")
            .OverridePropertyName(nameof(CloseWorkOrderRequest.ContractorUsed));

        RuleFor(x => x)
            .Must(x => x.ToolsAndPartsAccounted || !string.IsNullOrWhiteSpace(x.MissingItemsNote))
            .WithMessage("Describe what is missing.")
            .OverridePropertyName(nameof(CloseWorkOrderRequest.MissingItemsNote));
    }
}

public sealed class QaResultRequestValidator : AbstractValidator<QaResultRequest>
{
    public QaResultRequestValidator()
    {
        RuleFor(x => x.Result).IsInEnum().NotEqual(Domain.Maintenance.QaResult.Pending);
        RuleFor(x => x.Note).MaximumLength(2000);

        // Mirrors CK_QaCheck_FailNeedsNote: an engineer cannot be sent back with nothing
        // to act on.
        RuleFor(x => x)
            .Must(x => x.Result != Domain.Maintenance.QaResult.Fail || !string.IsNullOrWhiteSpace(x.Note))
            .WithMessage("A failed check must include a note explaining why.")
            .OverridePropertyName(nameof(QaResultRequest.Note));
    }
}
