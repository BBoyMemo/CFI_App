using Serilog.Context;

namespace CfiApp.Api.Middleware;

/// <summary>
/// Gives every request a correlation id so a single user action can be traced across
/// all log lines - including the ones written after the response was sent.
/// The id is echoed back so a mobile client can quote it when reporting a problem.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming))
        {
            var candidate = incoming.ToString();

            // Client supplied values are never trusted verbatim: they end up in log
            // files, so length and character set are constrained here.
            if (candidate.Length is > 0 and <= 64 &&
                candidate.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            {
                return candidate;
            }
        }

        return context.TraceIdentifier;
    }
}
