using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DailyTasks.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "daily-tasks";
    public string Audience { get; set; } = "daily-tasks";
    public int AccessTokenHours { get; set; } = 12;
}

public static class Policies
{
    public const string Manager = "Manager";
    public const string Engineer = "Engineer";
}

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAt) Create(User user)
    {
        // The JWT handler checks token times against the real clock, so they are issued from it too
        // (not from the app's TimeProvider, which tests move to other days).
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(_options.AccessTokenHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Name),
            new Claim("role", user.Role.ToString()),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer, _options.Audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

// PBKDF2-SHA256. Stored as "v1.{iterations}.{salt}.{hash}" so the cost can be raised later.
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, Iterations);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" || !int.TryParse(parts[1], out var iterations))
            return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        KeyDerivation.Pbkdf2(password, salt, KeyDerivationPrf.HMACSHA256, iterations, HashBytes);
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? throw new InvalidOperationException("Authenticated user has no subject claim."));

    public static bool IsManager(this ClaimsPrincipal user) =>
        user.FindFirstValue("role") == nameof(Role.Manager);
}

// A token is valid for hours, but the user behind it can change meanwhile. Every request re-checks
// the user: deactivated or given another role = the session ends (401, the app logs out). A user
// whose password was set by a manager is marked, and only /api/auth answers them until they change it.
public static class SessionCheck
{
    public const string MustChangePasswordClaim = "must_change_password";

    public static async Task OnTokenValidated(TokenValidatedContext ctx)
    {
        var principal = ctx.Principal!;
        if (!Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id))
        {
            ctx.Fail("Token has no valid subject.");
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.DeactivatedAt, u.Role, u.MustChangePassword })
            .SingleOrDefaultAsync(ctx.HttpContext.RequestAborted);

        if (user is null || user.DeactivatedAt != null || user.Role.ToString() != principal.FindFirstValue("role"))
        {
            ctx.Fail("The user is no longer active in this role.");
            return;
        }
        if (user.MustChangePassword)
            ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(MustChangePasswordClaim, "true"));
    }

    public static async Task BlockUntilPasswordChanged(HttpContext http, Func<Task> next)
    {
        if (http.User.HasClaim(MustChangePasswordClaim, "true") && !http.Request.Path.StartsWithSegments("/api/auth"))
        {
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            await http.Response.WriteAsJsonAsync(
                new { status = 403, title = "Choose a new password first.", code = "auth.mustChangePassword" },
                (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
            return;
        }
        await next();
    }
}
