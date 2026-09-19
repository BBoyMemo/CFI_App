using FluentValidation;

namespace CfiApp.Application.Scheduling;

public sealed class SetRosterRequestValidator : AbstractValidator<SetRosterRequest>
{
    public SetRosterRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.ActiveShiftId).GreaterThan(0);

        // Whether the date may be in the past is a business rule that needs the clock, so
        // it lives in the service rather than here.
    }
}

public sealed class AddShiftToPoolRequestValidator : AbstractValidator<AddShiftToPoolRequest>
{
    public AddShiftToPoolRequestValidator() => RuleFor(x => x.ShiftTypeId).GreaterThan(0);
}

public sealed class CreateCoverRequestValidator : AbstractValidator<CreateCoverRequest>
{
    public CreateCoverRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.ActiveShiftId).GreaterThan(0).When(x => x.ActiveShiftId is not null);
        RuleFor(x => x.ToDate).GreaterThanOrEqualTo(x => x.FromDate);
        RuleFor(x => x.Note).MaximumLength(200);
    }
}
