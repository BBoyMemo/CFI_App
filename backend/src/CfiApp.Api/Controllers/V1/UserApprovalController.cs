using Asp.Versioning;
using CfiApp.Application.Abstractions;
using CfiApp.Application.Auth;
using CfiApp.Domain.Identity;
using CfiApp.Infrastructure.Auth;
using CfiApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// The approval side of onboarding. A new starter registers themselves; a manager decides
/// what they are and switches the account on.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public sealed class UserApprovalController(
    CfiAppDbContext context,
    AuthService authService,
    IClock clock,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Everyone waiting for approval. Paged, because "show me everything" is how a list
    /// endpoint becomes a problem two years in.
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = Permissions.UserApprove)]
    [ProducesResponseType(typeof(PagedResult<PendingUserResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PendingUserResponse>>> Pending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = context.Users
            .AsNoTracking()
            .Where(x => x.Status == UserStatus.PendingApproval)
            .OrderBy(x => x.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((PagedResult.NormalisePage(page) - 1) * PagedResult.NormalisePageSize(pageSize))
            .Take(PagedResult.NormalisePageSize(pageSize))
            .Select(x => new PendingUserResponse(x.Id, x.FullName, x.Email, x.PhoneNumber, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<PendingUserResponse>(items, total, page, pageSize));
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = Permissions.UserApprove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        int id,
        ApproveUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await context.Users
            .Include(x => x.Areas)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user is null) return NotFound();

        if (user.Status != UserStatus.PendingApproval)
        {
            return Problem(
                title: "Account is not awaiting approval",
                detail: $"This account is already {user.Status}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var roleName = await context.Roles
            .Where(x => x.Id == request.RoleId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleName is null)
        {
            return Problem(
                title: "Unknown role",
                detail: "The selected role does not exist.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Holding user.approve is not the same as being allowed to hand out any role:
        // maintenance takes on engineers and QA, production takes on operators.
        var grantable = await RolesController.GrantableRoleNamesAsync(
            context, currentUser.UserId, cancellationToken);

        if (!grantable.Contains(roleName))
        {
            return Problem(
                title: "That role is not yours to assign",
                detail: $"Someone with {roleName} is approved by a different manager.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        user.RoleId = request.RoleId;
        user.OccupationId = request.OccupationId;

        // The department follows the role rather than being picked separately - an
        // operator is Production and an engineer is Maintenance by definition, and a form
        // that asks twice is a form somebody can answer inconsistently.
        user.DepartmentId = await DepartmentIdForRoleAsync(roleName, cancellationToken);

        // Areas only: a person is posted to a room, never to a machine.
        var areaIds = request.AreaIds.Distinct().ToArray();

        if (areaIds.Length > 0 &&
            await context.Areas.CountAsync(x => areaIds.Contains(x.Id), cancellationToken) != areaIds.Length)
        {
            return Problem(
                title: "Unknown area",
                detail: "One of the selected areas does not exist.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        user.Areas.Clear();
        foreach (var areaId in areaIds)
        {
            user.Areas.Add(new UserArea { AreaId = areaId });
        }

        user.Status = UserStatus.Active;
        user.ApprovedAt = clock.UtcNow;
        user.ApprovedByUserId = currentUser.UserId;

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Turning a registration down - not a real starter, a duplicate, or not this
    /// manager's to approve. Unlike <see cref="Disable"/> the account was never active,
    /// so nothing needs revoking; it simply stops showing up on the pending list, with
    /// the reason kept for whoever asks later why it never went through.
    /// </summary>
    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = Permissions.UserApprove)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(int id, RejectPendingUserRequest request, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound();

        if (user.Status != UserStatus.PendingApproval)
        {
            return Problem(
                title: "Account is not awaiting approval",
                detail: $"This account is already {user.Status}.",
                statusCode: StatusCodes.Status409Conflict);
        }

        user.Status = UserStatus.Rejected;
        user.RejectedAt = clock.UtcNow;
        user.RejectedByUserId = currentUser.UserId;
        user.RejectionReason = request.Reason.Trim();

        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<int?> DepartmentIdForRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!Permissions.RoleDepartments.TryGetValue(roleName, out var departmentName)) return null;

        return await context.Departments
            .Where(x => x.Name == departmentName)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Disabling an account ends every session it has, immediately - a refresh with the
    /// old security stamp is refused, so a phone that left with the person stops working.
    /// </summary>
    [HttpPost("{id:int}/disable")]
    [Authorize(Policy = Permissions.UserManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disable(int id, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return NotFound();

        if (user.Id == currentUser.UserId)
        {
            return Problem(
                title: "Cannot disable your own account",
                detail: "Ask another manager to do this.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        user.Status = UserStatus.Disabled;
        user.DisabledAt = clock.UtcNow;
        user.DisabledByUserId = currentUser.UserId;
        user.SecurityStamp = AuthService.NewSecurityStamp();

        await authService.RevokeAllForUserAsync(user.Id, "account disabled", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}

public sealed record PagedResult<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);

public static class PagedResult
{
    public const int MaxPageSize = 100;

    public static int NormalisePage(int page) => page < 1 ? 1 : page;

    public static int NormalisePageSize(int pageSize) =>
        pageSize switch { < 1 => 25, > MaxPageSize => MaxPageSize, _ => pageSize };
}
