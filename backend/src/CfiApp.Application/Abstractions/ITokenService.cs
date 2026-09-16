namespace CfiApp.Application.Abstractions;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record AccessTokenSubject(
    int UserId,
    string FullName,
    string SecurityStamp,
    string PreferredLanguage,
    IReadOnlyCollection<string> Permissions);

public interface ITokenService
{
    AccessToken CreateAccessToken(AccessTokenSubject subject);

    /// <summary>
    /// Returns the raw refresh token to hand to the client and the hash to store.
    /// The raw value is never persisted, so a database leak does not hand over sessions.
    /// </summary>
    (string Raw, string Hash) CreateRefreshToken();

    string HashRefreshToken(string raw);

    TimeSpan RefreshTokenLifetime { get; }
}
