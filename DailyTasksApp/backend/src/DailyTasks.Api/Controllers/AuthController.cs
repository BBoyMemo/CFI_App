using DailyTasks.Api.Auth;
using DailyTasks.Api.Common;
using DailyTasks.Api.Contracts;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, AppClock clock) : ControllerBase
{
    // Compared against when the name is unknown, so both failure paths cost the same time.
    private static readonly string DummyHash = PasswordHasher.Hash(Guid.NewGuid().ToString());

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrEmpty(request.Password))
            return new Validation()
                .Text("name", request.Name, required: true, max: Limits.NameMax)
                .Text("password", request.Password, required: true, max: Limits.PasswordMax)
                .ToResult();

        var normalized = Domain.User.Normalize(request.Name);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedName == normalized, ct);
        var valid = PasswordHasher.Verify(request.Password, user?.PasswordHash ?? DummyHash);
        // A deactivated user gets the same answer as a wrong password.
        if (user is null || !valid || !user.IsActive)
            return ApiErrors.Problem(StatusCodes.Status401Unauthorized, "invalidCredentials", "Invalid name or password.");

        var (token, expiresAt) = tokens.Create(user, request.KeepSignedIn);
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    // A fresh token of the same kind for a still valid session. The phone app calls it on every
    // start, so a kept session only runs out after a long time without opening the app.
    [HttpPost("renew")]
    [Authorize]
    public async Task<ActionResult<LoginResponse>> Renew(CancellationToken ct)
    {
        var id = User.UserId();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Unauthorized();

        var (token, expiresAt) = tokens.Create(user, User.KeepsSignedIn());
        return new LoginResponse(token, expiresAt, user.ToDto());
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var id = User.UserId();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, ct);
        return user is null ? Unauthorized() : user.ToDto();
    }

    // Every user changes their own password. The current one is required, so a phone left
    // logged in cannot be used to lock its owner out. Rate limited like login.
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var validation = new Validation();
        if (string.IsNullOrEmpty(request.CurrentPassword)) validation.Add("currentPassword", "required");
        if (string.IsNullOrEmpty(request.NewPassword)) validation.Add("newPassword", "required");
        else if (request.NewPassword.Length < Limits.PasswordMin) validation.Add("newPassword", "tooShort");
        else if (request.NewPassword.Length > Limits.PasswordMax) validation.Add("newPassword", "tooLong");
        if (!validation.IsValid) return validation.ToResult();

        var id = User.UserId();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Unauthorized();
        if (!PasswordHasher.Verify(request.CurrentPassword!, user.PasswordHash))
            return new Validation().Add("currentPassword", "wrongPassword").ToResult();

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword!);
        user.MustChangePassword = false;
        user.PasswordResetRequestedAt = null;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // "Forgot password?" on the login screen. Only flags the user for the managers, who set a new
    // password from the Team page; nothing changes for the user here. Always 204, so the answer
    // does not reveal which names exist.
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        var validation = new Validation().Text("name", request.Name, required: true, max: Limits.NameMax);
        if (!validation.IsValid) return validation.ToResult();

        var normalized = Domain.User.Normalize(request.Name!);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedName == normalized, ct);
        if (user is { IsActive: true, PasswordResetRequestedAt: null })
        {
            user.PasswordResetRequestedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return NoContent();
    }

    // Everyone may rename themselves. The name is also the login name, so it stays unique, and the
    // current password is required as for a password change. Returns a fresh session with the new name.
    [HttpPost("change-name")]
    [Authorize]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<LoginResponse>> ChangeName(ChangeNameRequest request, CancellationToken ct)
    {
        var validation = new Validation()
            .Text("newName", request.NewName, required: true, max: Limits.NameMax, min: Limits.NameMin);
        if (string.IsNullOrEmpty(request.CurrentPassword)) validation.Add("currentPassword", "required");
        if (!validation.IsValid) return validation.ToResult();

        var id = User.UserId();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Unauthorized();
        if (!PasswordHasher.Verify(request.CurrentPassword!, user.PasswordHash))
            return new Validation().Add("currentPassword", "wrongPassword").ToResult();

        var name = request.NewName!.Trim();
        var normalized = Domain.User.Normalize(name);
        if (normalized != user.NormalizedName && await db.Users.AnyAsync(u => u.NormalizedName == normalized, ct))
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");

        user.Name = name;
        user.NormalizedName = normalized;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");
        }

        var (token, expiresAt) = tokens.Create(user, User.KeepsSignedIn());
        return new LoginResponse(token, expiresAt, user.ToDto());
    }
}

public static class RateLimits
{
    public const string Login = "login";
}

public static class UserMapping
{
    public static UserDto ToDto(this Domain.User u) => new(
        u.Id, u.Name, u.Role, u.CreatedAt, u.IsActive, u.PasswordResetRequestedAt != null, u.MustChangePassword);
}
