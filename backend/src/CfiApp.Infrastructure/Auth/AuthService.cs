using CfiApp.Application.Abstractions;
using CfiApp.Application.Auth;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CfiApp.Infrastructure.Auth;

/// <summary>
/// Sign in, session renewal and sign out.
///
/// Three rules drive the design:
/// a failed sign in never reveals whether the account exists;
/// every renewal rotates the refresh token, and replaying an old one is treated as theft;
/// disabling an account takes effect on the next renewal at the latest, because the
/// security stamp stored in the token no longer matches.
/// </summary>
public sealed class AuthService(
    CfiAppDbContext context,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IClock clock,
    ILogger<AuthService> logger)
{
    private const string RotatedReason = "rotated";
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<User> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormaliseEmail(request.Email);

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            SecurityStamp = NewSecurityStamp(),
            PreferredLanguage = request.PreferredLanguage,
            // Inert until a manager approves it and assigns role, department and units.
            Status = UserStatus.PendingApproval
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registration received for user {UserId}", user.Id);
        return user;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken cancellationToken)
    {
        var email = NormaliseEmail(request.Email);

        var user = await context.Users
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            // Spend the same work as a real verification so timing does not leak whether
            // the address is registered.
            passwordHasher.Verify(DummyHash.Value, request.Password);
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        var now = clock.UtcNow;

        if (user.LockoutEndsAt is { } lockoutEnd && lockoutEnd > now)
        {
            return AuthResult.Fail(AuthFailure.AccountLocked);
        }

        var verification = passwordHasher.Verify(user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockoutEndsAt = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("Account {UserId} locked after repeated failed sign in attempts", user.Id);
            }

            await context.SaveChangesAsync(cancellationToken);
            return AuthResult.Fail(AuthFailure.InvalidCredentials);
        }

        if (user.Status == UserStatus.PendingApproval)
        {
            return AuthResult.Fail(AuthFailure.AccountNotApproved);
        }

        if (user.Status == UserStatus.Disabled)
        {
            return AuthResult.Fail(AuthFailure.AccountDisabled);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;

        // Correct password stored with older parameters: strengthen it now, silently.
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.Hash(request.Password);
        }

        var tokens = await IssueTokensAsync(user, request.DeviceId, ip, replacing: null, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens);
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, string? ip, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await context.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (stored?.User is null)
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        var now = clock.UtcNow;
        var user = stored.User;

        // Account state is checked before anything else. Disabling an account revokes its
        // tokens, and without this order that revocation would look like token theft: the
        // person would get a generic sign in failure instead of being told their account
        // was switched off, and the reuse alarm would fire on a perfectly normal event.
        if (user.Status != UserStatus.Active)
        {
            return AuthResult.Fail(user.Status == UserStatus.Disabled
                ? AuthFailure.AccountDisabled
                : AuthFailure.AccountNotApproved);
        }

        if (stored.RevokedAt is not null)
        {
            // Only a token that was rotated away should raise the alarm. A token revoked by
            // signing out or changing a password is simply spent - replaying it on a flaky
            // connection is ordinary, and must not sign every device out.
            if (stored.RevokedReason == RotatedReason)
            {
                await RevokeAllForUserAsync(stored.UserId, "refresh token reuse detected", cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                logger.LogWarning(
                    "Refresh token reuse detected for user {UserId}; all sessions revoked", stored.UserId);
            }

            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        if (stored.ExpiresAt <= now)
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        // The stamp changes whenever credentials or status change, which is what makes a
        // password change or a disabled account end existing sessions.
        if (!string.Equals(stored.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            return AuthResult.Fail(AuthFailure.InvalidRefreshToken);
        }

        var tokens = await IssueTokensAsync(user, request.DeviceId ?? stored.DeviceId, ip, stored, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return AuthResult.Success(tokens);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashRefreshToken(refreshToken);

        var stored = await context.RefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == hash && x.RevokedAt == null, cancellationToken);

        if (stored is null) return;

        stored.RevokedAt = clock.UtcNow;
        stored.RevokedReason = "signed out";
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ChangePasswordAsync(
        int userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return false;

        if (passwordHasher.Verify(user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // Changing the password signs every other device out.
        user.SecurityStamp = NewSecurityStamp();
        await RevokeAllForUserAsync(user.Id, "password changed", cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Ends every session immediately. Used when an account is disabled - someone who left
    /// the company must not keep a working phone.
    /// </summary>
    public async Task RevokeAllForUserAsync(int userId, string reason, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var active = await context.RefreshTokens
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }
    }

    public static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    private async Task<AuthTokens> IssueTokensAsync(
        User user,
        string? deviceId,
        string? ip,
        RefreshToken? replacing,
        CancellationToken cancellationToken)
    {
        var permissions = await LoadPermissionsAsync(user.RoleId, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(new AccessTokenSubject(
            user.Id,
            user.FullName,
            user.SecurityStamp,
            user.PreferredLanguage,
            permissions));

        var (raw, hash) = tokenService.CreateRefreshToken();
        var now = clock.UtcNow;

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            SecurityStamp = user.SecurityStamp,
            DeviceId = deviceId,
            CreatedByIp = ip,
            CreatedAt = now,
            ExpiresAt = now.Add(tokenService.RefreshTokenLifetime)
        };

        context.RefreshTokens.Add(refreshToken);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.RevokedReason = RotatedReason;
            replacing.ReplacedByToken = refreshToken;
        }

        return new AuthTokens(
            accessToken.Value,
            accessToken.ExpiresAt,
            raw,
            refreshToken.ExpiresAt);
    }

    public async Task<IReadOnlyCollection<string>> LoadPermissionsAsync(
        int? roleId,
        CancellationToken cancellationToken)
    {
        if (roleId is null) return [];

        return await context.RolePermissions
            .Where(x => x.RoleId == roleId)
            .Select(x => x.Permission!.Key)
            .ToListAsync(cancellationToken);
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// A real hash of a throwaway password, so verifying an unknown account costs the same
    /// as verifying a real one.
    /// </summary>
    private static class DummyHash
    {
        public static readonly string Value = new Security.PasswordHasher()
            .Hash("cfi-app-timing-equalisation-value");
    }
}
