using CfiApp.Api.Security;
using CfiApp.Application.Maintenance;
using CfiApp.Domain.Maintenance;
using CfiApp.Infrastructure.Attendance;
using CfiApp.Infrastructure.Messaging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CfiApp.Api.Middleware;

/// <summary>
/// Maps the application's own typed exceptions to the right HTTP status, so a service
/// method can just throw a meaningful exception instead of every caller re-deriving the
/// same if/else. Runs before <see cref="ConflictExceptionHandler"/>; anything it does not
/// recognise falls through to that handler and, after it, to the generic ProblemDetails
/// writer.
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            WorkOrderNotFoundException => (StatusCodes.Status404NotFound, exception.Message),
            UnknownReferenceException => (StatusCodes.Status400BadRequest, exception.Message),
            WorkOrderForbiddenException => (StatusCodes.Status403Forbidden, exception.Message),
            InvalidWorkOrderTransitionException => (StatusCodes.Status409Conflict, exception.Message),
            ClockRejectedException => (StatusCodes.Status409Conflict, exception.Message),
            UnknownMessageTargetException => (StatusCodes.Status400BadRequest, exception.Message),
            _ => (0, null)
        };

        if (status == 0) return false;

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["correlationId"] =
            httpContext.Items[CorrelationIdMiddleware.HeaderName] as string ?? httpContext.TraceIdentifier;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
