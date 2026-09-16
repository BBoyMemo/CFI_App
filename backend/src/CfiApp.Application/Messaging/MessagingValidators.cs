using FluentValidation;

namespace CfiApp.Application.Messaging;

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Priority).IsInEnum();

        RuleFor(x => x)
            .Must(x => x.RecipientUserIds.Count > 0 || x.RecipientDepartmentIds.Count > 0)
            .WithMessage("Address the message to at least one person or department.")
            .OverridePropertyName(nameof(SendMessageRequest.RecipientUserIds));

        RuleForEach(x => x.RecipientUserIds).GreaterThan(0);
        RuleForEach(x => x.RecipientDepartmentIds).GreaterThan(0);
    }
}

public sealed class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Platform).IsInEnum();
    }
}
