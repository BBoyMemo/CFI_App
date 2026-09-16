using System.Reflection;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace CfiApp.Api.Controllers.V1;

/// <summary>
/// Non-business endpoints used by clients and by operations to confirm which build is
/// running. Kept versioned like every other endpoint so the mobile app can rely on it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system")]
public sealed class SystemController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("info")]
    [ProducesResponseType(typeof(SystemInfoResponse), StatusCodes.Status200OK)]
    public ActionResult<SystemInfoResponse> GetInfo()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        return Ok(new SystemInfoResponse(
            Application: "CFI App API",
            Version: version,
            Environment: environment.EnvironmentName,
            ServerTimeUtc: DateTimeOffset.UtcNow));
    }
}

/// <param name="ServerTimeUtc">
/// Always UTC. Clients convert to Europe/London for display; the server never formats
/// local time, because attendance records must not depend on a machine time zone.
/// </param>
public sealed record SystemInfoResponse(
    string Application,
    string Version,
    string Environment,
    DateTimeOffset ServerTimeUtc);
