using FluentValidation;

namespace CfiApp.Application.Work;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum().When(x => x.Priority is not null);
        RuleFor(x => x.AssignedUserIds).NotEmpty().WithMessage("Assign at least one engineer.");
        RuleForEach(x => x.AssignedUserIds).GreaterThan(0);
    }
}

public sealed class ReassignTaskRequestValidator : AbstractValidator<ReassignTaskRequest>
{
    public ReassignTaskRequestValidator()
    {
        RuleFor(x => x.AssignedUserIds).NotEmpty().WithMessage("Assign at least one engineer.");
        RuleForEach(x => x.AssignedUserIds).GreaterThan(0);
    }
}

public sealed class CompleteTaskRequestValidator : AbstractValidator<CompleteTaskRequest>
{
    public CompleteTaskRequestValidator()
    {
        RuleFor(x => x.Note).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.PhotoAssetId).GreaterThan(0).When(x => x.PhotoAssetId is not null);
    }
}

public sealed class CreatePartOrderRequestValidator : AbstractValidator<CreatePartOrderRequest>
{
    public CreatePartOrderRequestValidator()
    {
        RuleFor(x => x.PartName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0);
    }
}
