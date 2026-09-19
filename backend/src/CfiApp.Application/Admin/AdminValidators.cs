using FluentValidation;

namespace CfiApp.Application.Admin;

public sealed class UpsertDepartmentRequestValidator : AbstractValidator<UpsertDepartmentRequest>
{
    public UpsertDepartmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpsertOccupationRequestValidator : AbstractValidator<UpsertOccupationRequest>
{
    public UpsertOccupationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpsertShiftTypeRequestValidator : AbstractValidator<UpsertShiftTypeRequest>
{
    public UpsertShiftTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);

        RuleFor(x => x.Weekdays)
            .NotEmpty().WithMessage("Choose at least one day of the week.")
            .Must(days => days.Distinct().Count() == days.Count)
            .WithMessage("The same day was listed twice.");
        // A night shift crossing midnight (22:00-06:00) is normal and deliberately allowed,
        // so start and end are only required to differ, not ordered.
        RuleFor(x => x)
            .Must(x => x.StartTime != x.EndTime)
            .WithMessage("Start time and end time must be different.")
            .OverridePropertyName(nameof(UpsertShiftTypeRequest.EndTime));
    }
}

public sealed class UpsertUnitRequestValidator : AbstractValidator<UpsertUnitRequest>
{
    public UpsertUnitRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertAreaRequestValidator : AbstractValidator<UpsertAreaRequest>
{
    public UpsertAreaRequestValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Code).MaximumLength(20);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertLineRequestValidator : AbstractValidator<UpsertLineRequest>
{
    public UpsertLineRequestValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertEquipmentRequestValidator : AbstractValidator<UpsertEquipmentRequest>
{
    public UpsertEquipmentRequestValidator()
    {
        RuleFor(x => x.UnitId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.IconKey).MaximumLength(60);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AssignManagerScopeRequestValidator : AbstractValidator<AssignManagerScopeRequest>
{
    public AssignManagerScopeRequestValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);

        RuleFor(x => x)
            .Must(x => x.DepartmentId.HasValue ^ x.UnitId.HasValue)
            .WithMessage("Set exactly one of DepartmentId or UnitId.")
            .OverridePropertyName(nameof(AssignManagerScopeRequest.DepartmentId));
    }
}
