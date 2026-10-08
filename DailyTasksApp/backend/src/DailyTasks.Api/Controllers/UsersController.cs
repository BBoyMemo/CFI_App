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
    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> List(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Name)
            .Select(u => new UserDto(u.Id, u.Name, u.Role, u.CreatedAt))
            .ToListAsync(ct);

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
}
