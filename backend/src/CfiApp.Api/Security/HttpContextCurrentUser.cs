using System.Security.Claims;
using CfiApp.Application.Abstractions;

namespace CfiApp.Api.Security;

/// <summary>
/// Reads the acting user from the request principal. Returns null for anonymous
/// requests and background work, which is why audit columns are nullable.
/// The claim is populated by JWT authentication in Phase 2.
/// </summary>
public sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var id) ? id : null;
        }
    }
}
