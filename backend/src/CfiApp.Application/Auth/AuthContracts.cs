namespace CfiApp.Application.Auth;

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string? PhoneNumber,
    string Password,
    string PreferredLanguage);

public sealed record LoginRequest(string Email, string Password, string? DeviceId);

public sealed record RefreshRequest(string RefreshToken, string? DeviceId);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// What the manager decides when approving a registration: what the person does, which
/// department they belong to and which units they work in.
/// </summary>
public sealed record ApproveUserRequest(
    int RoleId,
    int? OccupationId,
    IReadOnlyCollection<int> AreaIds);

/// <summary>Turning a registration down - not a real starter, a duplicate, or not theirs to approve.</summary>
public sealed record RejectPendingUserRequest(string Reason);

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record CurrentUserResponse(
    int Id,
    string FullName,
    string Email,
    string Status,
    string? Role,
    string? Occupation,
    string? Department,
    string PreferredLanguage,
    IReadOnlyCollection<int> AreaIds,
    IReadOnlyCollection<string> Permissions);

public sealed record PendingUserResponse(
    int Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateTimeOffset RegisteredAt);

/// <summary>
/// Why a sign in attempt did not produce tokens. Kept separate from the message shown to
/// the user: the API says "email or password is incorrect" for all of the first three, so
/// an attacker cannot discover which accounts exist.
/// </summary>
public enum AuthFailure
{
    None = 0,
    InvalidCredentials = 1,
    AccountNotApproved = 2,
    AccountDisabled = 3,
    AccountLocked = 4,
    InvalidRefreshToken = 5,
    AccountRejected = 6
}

public sealed record AuthResult(AuthTokens? Tokens, AuthFailure Failure)
{
    public bool Succeeded => Tokens is not null && Failure == AuthFailure.None;

    public static AuthResult Success(AuthTokens tokens) => new(tokens, AuthFailure.None);
    public static AuthResult Fail(AuthFailure failure) => new(null, failure);
}
