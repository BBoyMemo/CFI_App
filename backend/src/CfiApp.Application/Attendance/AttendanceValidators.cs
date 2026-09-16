using CfiApp.Domain.Attendance;
using FluentValidation;

namespace CfiApp.Application.Attendance;

public sealed class ClockRequestValidator : AbstractValidator<ClockRequest>
{
    public ClockRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.DeviceId).MaximumLength(128);

        // Mirrors CK_ClockEvent_Coordinates: half a coordinate is not a location.
        RuleFor(x => x)
            .Must(x => (x.Latitude is null) == (x.Longitude is null))
            .WithMessage("Latitude and longitude must be provided together.")
            .OverridePropertyName(nameof(ClockRequest.Longitude));

        RuleFor(x => x)
            .Must(x => x.Source != ClockSource.AutoGeofence || x.Latitude is not null)
            .WithMessage("A location is required for an automatic clock event.")
            .OverridePropertyName(nameof(ClockRequest.Latitude));

        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90).When(x => x.Latitude is not null);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180).When(x => x.Longitude is not null);
        RuleFor(x => x.AccuracyMeters).GreaterThanOrEqualTo(0).When(x => x.AccuracyMeters is not null);
    }
}

public sealed class ClockCorrectionRequestValidator : AbstractValidator<ClockCorrectionRequest>
{
    public ClockCorrectionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateOvertimeRequestValidator : AbstractValidator<CreateOvertimeRequest>
{
    public CreateOvertimeRequestValidator()
    {
        RuleFor(x => x.Minutes).GreaterThan(0).LessThanOrEqualTo(24 * 60);
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class DecideRequestValidator : AbstractValidator<DecideRequest>
{
    public DecideRequestValidator()
    {
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CreateHolidayRequestValidator : AbstractValidator<CreateHolidayRequest>
{
    public CreateHolidayRequestValidator()
    {
        RuleFor(x => x).Must(x => x.EndDate >= x.StartDate)
            .WithMessage("The end date must not be before the start date.")
            .OverridePropertyName(nameof(CreateHolidayRequest.EndDate));
    }
}

public sealed class UpsertGeofenceRequestValidator : AbstractValidator<UpsertGeofenceRequest>
{
    public UpsertGeofenceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Latitude).InclusiveBetween(-90, 90);
        RuleFor(x => x.Longitude).InclusiveBetween(-180, 180);
        RuleFor(x => x.RadiusMeters).GreaterThan(0);
        RuleFor(x => x.ReentryToleranceMinutes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RequiredAccuracyMeters).GreaterThan(0);
        RuleFor(x => x.MaxClockDriftMinutes).GreaterThan(0);
    }
}
