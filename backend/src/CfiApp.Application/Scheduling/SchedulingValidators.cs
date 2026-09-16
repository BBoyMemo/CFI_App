using FluentValidation;

namespace CfiApp.Application.Scheduling;

public sealed class CreateShiftAssignmentRequestValidator : AbstractValidator<CreateShiftAssignmentRequest>
{
    public CreateShiftAssignmentRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.ShiftTypeId).GreaterThan(0);
    }
}
