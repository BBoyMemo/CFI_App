using Microsoft.AspNetCore.Authorization;

namespace CfiApp.Api.Security;

/// <summary>
/// Claim type carrying one permission key. The token lists what the user may do, so an
/// endpoint never has to look up a role name - and never has to guess what a role means.
/// </summary>
public static class CfiClaimTypes
{
    public const string Permission = "cfi:perm";
    public const string SecurityStamp = "cfi:stamp";
    public const string Language = "cfi:lang";
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var granted = context.User.FindAll(CfiClaimTypes.Permission)
            .Any(claim => string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (granted)
        {
            context.Succeed(requirement);
        }

        // Not calling Fail lets other handlers contribute; an unmet requirement still ends
        // as 403 rather than 401, which is the rule the frontend depends on.
        return Task.CompletedTask;
    }
}

/// <summary>
/// Builds an authorization policy on demand for any [Authorize(Policy = "workorder.claim")]
/// attribute, so adding a permission needs no registration in Program.cs - one less place
/// for a new endpoint to be quietly left unprotected.
/// </summary>
public sealed class PermissionPolicyProvider(
    Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var configured = await base.GetPolicyAsync(policyName);
        if (configured is not null) return configured;

        // Permission keys are always module.action; anything else is a real policy name.
        if (!policyName.Contains('.', StringComparison.Ordinal)) return null;

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
