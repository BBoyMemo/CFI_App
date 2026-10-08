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
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
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
        if (user is null || !valid)
            return ApiErrors.Problem(StatusCodes.Status401Unauthorized, "invalidCredentials", "Invalid name or password.");

        var (token, expiresAt) = tokens.Create(user);
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
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public static class RateLimits
{
    public const string Login = "login";
}

public static class UserMapping
{
    public static UserDto ToDto(this Domain.User u) => new(u.Id, u.Name, u.Role, u.CreatedAt);
}
