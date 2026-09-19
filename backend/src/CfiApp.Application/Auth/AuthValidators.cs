using FluentValidation;

namespace CfiApp.Application.Auth;

public static class PasswordPolicy
{
    /// <summary>
    /// Length beats character-class rules: a long passphrase is both stronger and easier
    /// to type on a phone with gloves on. NCSC guidance points the same way.
    /// </summary>
    public const int MinimumLength = 10;
    public const int MaximumLength = 128;

    /// <summary>
    /// A short deny list of the passwords people actually pick. Not a substitute for
    /// hashing - just a way to stop the worst choices at the door.
    /// </summary>
    private static readonly HashSet<string> Obvious = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "password1", "password123", "passw0rd", "qwertyuiop", "1234567890",
        "letmein123", "welcome123", "admin12345", "changeme123", "iloveyou12",
        "countyfood", "cfiapp2026", "maintenance", "engineer123"
    };

    public static bool IsObvious(string password) => Obvious.Contains(password.Trim());

    /// <summary>
    /// Rejects the classic "my email is my password". Cheap, and it catches a real habit.
    /// </summary>
    public static bool ContainsEmailLocalPart(string password, string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var localPart = email.Split('@')[0];
        return localPart.Length >= 4
               && password.Contains(localPart, StringComparison.OrdinalIgnoreCase);
    }
}

public static class SupportedLanguage
{
    public static readonly string[] All = ["en", "pl", "bg", "es"];

    public static bool IsSupported(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Enter your full name.")
            .MaximumLength(150);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Enter your email address.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(32);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Choose a password.")
            .MinimumLength(PasswordPolicy.MinimumLength)
                .WithMessage($"Use at least {PasswordPolicy.MinimumLength} characters.")
            .MaximumLength(PasswordPolicy.MaximumLength)
            .Must(password => !PasswordPolicy.IsObvious(password))
                .WithMessage("This password is too easy to guess. Choose another one.");

        RuleFor(x => x)
            .Must(request => !PasswordPolicy.ContainsEmailLocalPart(request.Password, request.Email))
                .WithMessage("Your password must not contain your email address.")
            .OverridePropertyName(nameof(RegisterRequest.Password));

        RuleFor(x => x.PreferredLanguage)
            .Must(SupportedLanguage.IsSupported)
                .WithMessage($"Supported languages are {string.Join(", ", SupportedLanguage.All)}.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordPolicy.MaximumLength);
        RuleFor(x => x.DeviceId).MaximumLength(128);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512);
        RuleFor(x => x.DeviceId).MaximumLength(128);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(PasswordPolicy.MinimumLength)
                .WithMessage($"Use at least {PasswordPolicy.MinimumLength} characters.")
            .MaximumLength(PasswordPolicy.MaximumLength)
            .Must(password => !PasswordPolicy.IsObvious(password))
                .WithMessage("This password is too easy to guess. Choose another one.")
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("The new password must be different from the current one.");
    }
}

public sealed class ApproveUserRequestValidator : AbstractValidator<ApproveUserRequest>
{
    public ApproveUserRequestValidator()
    {
        RuleFor(x => x.RoleId).GreaterThan(0).WithMessage("Choose a role for this person.");
        RuleFor(x => x.AreaIds).NotNull();
        RuleForEach(x => x.AreaIds).GreaterThan(0);
    }
}

public sealed class RejectPendingUserRequestValidator : AbstractValidator<RejectPendingUserRequest>
{
    public RejectPendingUserRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Say why this registration is being turned down.")
            .MaximumLength(500);
    }
}
