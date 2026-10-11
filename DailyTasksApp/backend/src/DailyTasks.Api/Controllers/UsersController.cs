using DailyTasks.Api.Auth;
using DailyTasks.Api.Common;
using DailyTasks.Api.Contracts;
using DailyTasks.Api.Data;
using DailyTasks.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DailyTasks.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Manager)]
public class UsersController(AppDbContext db, AppClock clock) : ControllerBase
{
    // Everyone, deactivated users included (the Team page lists them as such).
    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> List(CancellationToken ct) =>
        (await db.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync(ct))
            .Select(u => u.ToDto())
            .ToList();

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var validation = new Validation()
            .Text("name", request.Name, required: true, max: Limits.NameMax, min: Limits.NameMin);
        if (string.IsNullOrEmpty(request.Password)) validation.Add("password", "required");
        else if (request.Password.Length < Limits.PasswordMin) validation.Add("password", "tooShort");
        else if (request.Password.Length > Limits.PasswordMax) validation.Add("password", "tooLong");
        if (request.Role is null || !Enum.IsDefined(request.Role.Value)) validation.Add("role", "required");
        if (!validation.IsValid) return validation.ToResult();

        var name = request.Name!.Trim();
        var normalized = Domain.User.Normalize(name);
        if (await db.Users.AnyAsync(u => u.NormalizedName == normalized, ct))
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = normalized,
            PasswordHash = PasswordHasher.Hash(request.Password!),
            Role = request.Role!.Value,
            CreatedAt = clock.UtcNow,
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two managers adding the same name at once: the unique index decides.
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");
        }

        return StatusCode(StatusCodes.Status201Created, user.ToDto());
    }

    // Name, role and, only for a user who asked for it ("Forgot password?"), a new password. That
    // password is temporary: the user must change it at next login. Managers change their own details on the account page, not here, so nobody
    // can take away their own Manager role.
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        if (id == User.UserId())
            return ApiErrors.Conflict("user.self", "Use the account page to change your own details.");

        var validation = new Validation()
            .Text("name", request.Name, required: true, max: Limits.NameMax, min: Limits.NameMin);
        if (!string.IsNullOrEmpty(request.Password))
        {
            if (request.Password.Length < Limits.PasswordMin) validation.Add("password", "tooShort");
            else if (request.Password.Length > Limits.PasswordMax) validation.Add("password", "tooLong");
        }
        if (request.Role is null || !Enum.IsDefined(request.Role.Value)) validation.Add("role", "required");
        if (!validation.IsValid) return validation.ToResult();

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ApiErrors.NotFound();
        if (!user.IsActive)
            return ApiErrors.Conflict("user.deactivated", "A deactivated user cannot be changed.");
        // Managers do not change passwords at will: only for someone who asked ("Forgot password?").
        if (!string.IsNullOrEmpty(request.Password) && user.PasswordResetRequestedAt is null)
            return ApiErrors.Conflict("user.noResetRequest", "A new password can only be set after the user asks for one.");

        var name = request.Name!.Trim();
        var normalized = Domain.User.Normalize(name);
        if (normalized != user.NormalizedName && await db.Users.AnyAsync(u => u.NormalizedName == normalized, ct))
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");

        user.Name = name;
        user.NormalizedName = normalized;
        user.Role = request.Role!.Value;
        if (!string.IsNullOrEmpty(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
            user.MustChangePassword = true;
            user.PasswordResetRequestedAt = null;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return ApiErrors.Conflict("user.nameTaken", "A user with this name already exists.");
        }
        return user.ToDto();
    }

    // "Delete" on the Team page. The user stays in the database so history keeps their name; they
    // just cannot log in any more (their open session ends on its next request). Their open tasks
    // must be given to someone else first, so no task is left with nobody to do it.
    [HttpPost("{id:guid}/deactivate")]
    public async Task<ActionResult<UserDto>> Deactivate(Guid id, CancellationToken ct)
    {
        if (id == User.UserId())
            return ApiErrors.Conflict("user.self", "You cannot deactivate yourself.");

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ApiErrors.NotFound();
        if (!user.IsActive) return user.ToDto();

        var hasOpenTasks = await db.TaskAssignees.AnyAsync(
            a => a.UserId == id && a.Task.Status != WorkStatus.Completed, ct);
        if (hasOpenTasks)
            return ApiErrors.Conflict("user.hasOpenTasks", "Give this user's open tasks to someone else first.");

        user.DeactivatedAt = clock.UtcNow;
        user.PasswordResetRequestedAt = null;
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }

    // Undoes a deactivation: the user can log in again with their old password.
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<UserDto>> Activate(Guid id, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return ApiErrors.NotFound();
        if (user.IsActive) return user.ToDto();

        user.DeactivatedAt = null;
        await db.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
