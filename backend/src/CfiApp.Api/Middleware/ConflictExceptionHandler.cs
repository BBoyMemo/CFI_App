using CfiApp.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CfiApp.Api.Middleware;

/// <summary>
/// Turns a database constraint violation into the same ProblemDetails shape every other
/// error uses, instead of a 500.
///
/// This is a global backstop, not a substitute for checking things up front: controllers
/// still validate what they can before saving. But a race - two engineers claiming the
/// same job, two requests creating the same unit code at once - is exactly the case an
/// upfront check cannot catch, and it must not surface as an unhandled exception.
/// </summary>
public sealed class ConflictExceptionHandler : IExceptionHandler
{
    private const string UniqueViolation = "23505";
    private const string ForeignKeyViolation = "23503";
    private const string CheckViolation = "23514";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Two people acting on the same row at once - a claim race is the clearest example.
        // The xmin concurrency token on every audited entity is what makes this exception
        // possible in the first place, rather than a silent second write.
        if (exception is DbUpdateConcurrencyException)
        {
            await WriteAsync(
                httpContext,
                StatusCodes.Status409Conflict,
                "This record was changed by someone else just now. Reload and try again.",
                cancellationToken);
            return true;
        }

        if (exception is not DbUpdateException { InnerException: PostgresException postgres })
        {
            return false;
        }

        var (status, title) = postgres.SqlState switch
        {
            UniqueViolation => (StatusCodes.Status409Conflict, "This value is already in use."),
            ForeignKeyViolation => (StatusCodes.Status409Conflict,
                "A related record could not be found, or another record still depends on it."),
            CheckViolation => (StatusCodes.Status400BadRequest,
                "The submitted data does not satisfy a required rule."),
            _ => (0, null)
        };

        if (status == 0) return false;

        await WriteAsync(httpContext, status, title!, cancellationToken);
        return true;
    }

    private static async Task WriteAsync(
        HttpContext httpContext, int status, string title, CancellationToken cancellationToken)
    {
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
    }
}
