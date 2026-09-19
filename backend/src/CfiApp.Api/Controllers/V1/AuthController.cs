using Asp.Versioning;
using CfiApp.Api.Security;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Auth;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Auth;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
// Sign in and registration are the endpoints worth hammering, so they get the stricter bucket.
[EnableRateLimiting("auth")]
public sealed class AuthController(
    AuthService authService,
    CfiAppDbContext context,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Self service registration. The account is created but inert: a manager has to
    /// approve it and assign a role, a department and work units before it can sign in.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await context.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            return Problem(
                title: "Email already registered",
                detail: "An account with this email address already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        await authService.RegisterAsync(request, cancellationToken);

        // 202: accepted, but nothing is usable until a manager approves it.
        return Accepted(new { message = "Registration received. A manager will approve your account." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, RemoteIp(), cancellationToken);

        return result.Succeeded
            ? Ok(result.Tokens)
            : FailureResponse(result.Failure);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokens), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, RemoteIp(), cancellationToken);

        return result.Succeeded
            ? Ok(result.Tokens)
            : FailureResponse(result.Failure);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;
        var changed = await authService.ChangePasswordAsync(userId, request, cancellationToken);

        if (!changed)
        {
            return Problem(
                title: "Current password is incorrect",
                detail: "Enter your current password to set a new one.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId!.Value;

        var user = await context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .Include(x => x.Occupation)
            .Include(x => x.Department)
            .Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null) return NotFound();

        var permissions = User.FindAll(CfiClaimTypes.Permission).Select(x => x.Value).ToArray();

        return Ok(new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email,
            user.Status.ToString(),
            user.Role?.Name,
            user.Occupation?.Name,
            user.Department?.Name,
            user.PreferredLanguage,
            user.Areas.Select(x => x.AreaId).ToArray(),
            permissions));
    }

    /// <summary>
    /// Language is a per person setting, available to every role, and it follows the
    /// account rather than the device.
    /// </summary>
    [HttpPut("me/language")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetLanguage(
        [FromBody] string language,
        CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.Contains(language))
        {
            return Problem(
                title: "Unsupported language",
                detail: $"Supported languages are {string.Join(", ", SupportedLanguages)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var userId = currentUser.UserId!.Value;
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null) return NotFound();

        user.PreferredLanguage = language;
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static readonly string[] SupportedLanguages = ["en", "pl", "bg", "es"];

    private string? RemoteIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>
    /// 401 means "you are not signed in"; 403 means "you are, but this account cannot be
    /// used". Mixing them up is what made the old system log people out at random.
    /// </summary>
    private IActionResult FailureResponse(AuthFailure failure) => failure switch
    {
        AuthFailure.AccountNotApproved => Problem(
            title: "Account awaiting approval",
            detail: "A manager has not approved this account yet.",
            statusCode: StatusCodes.Status403Forbidden),

        AuthFailure.AccountDisabled => Problem(
            title: "Account disabled",
            detail: "This account has been disabled. Contact your manager.",
            statusCode: StatusCodes.Status403Forbidden),

        AuthFailure.AccountRejected => Problem(
            title: "Registration not approved",
            detail: "This registration was not approved. Contact your manager.",
            statusCode: StatusCodes.Status403Forbidden),

        AuthFailure.AccountLocked => Problem(
            title: "Too many attempts",
            detail: "Too many failed sign in attempts. Try again in a few minutes.",
            statusCode: StatusCodes.Status429TooManyRequests),

        // Deliberately identical for a wrong password and an unknown address.
        _ => Problem(
            title: "Sign in failed",
            detail: "Email or password is incorrect.",
            statusCode: StatusCodes.Status401Unauthorized)
    };
}
