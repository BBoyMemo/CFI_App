using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CfiApp.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace CfiApp.Infrastructure.Security;

/// <summary>
/// Issues the short lived access token and the long lived refresh token.
///
/// The access token carries the permission list, so an endpoint check is a claim lookup
/// rather than a database round trip. It also carries the security stamp: when an account
/// is disabled the stamp changes, and any refresh attempt with an old stamp is rejected.
/// </summary>
public sealed class JwtTokenService(
    SigningCredentials signingCredentials,
    JwtOptions options,
    IClock clock) : ITokenService
{
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(options.RefreshTokenDays);

    public AccessToken CreateAccessToken(AccessTokenSubject subject)
    {
        var issuedAt = clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.Name, subject.FullName),
            new("cfi:stamp", subject.SecurityStamp),
            new("cfi:lang", subject.PreferredLanguage)
        };

        claims.AddRange(subject.Permissions.Select(permission => new Claim("cfi:perm", permission)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: signingCredentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public (string Raw, string Hash) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, HashRefreshToken(raw));
    }

    /// <summary>
    /// A plain SHA-256 is right here, unlike for passwords: the token is 64 random bytes,
    /// so there is nothing to brute force and a slow hash would only cost latency.
    /// </summary>
    public string HashRefreshToken(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
