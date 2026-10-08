using Microsoft.AspNetCore.Mvc;

namespace DailyTasks.Api.Common;

// Errors carry a stable "code" the web client translates; the title is for humans reading logs.
public static class ApiErrors
{
    public static ObjectResult Problem(int status, string code, string title)
    {
        var problem = new ProblemDetails { Status = status, Title = title };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status, ContentTypes = { "application/problem+json" } };
    }

    public static ObjectResult NotFound(string code = "notFound") =>
        Problem(StatusCodes.Status404NotFound, code, "Not found.");

    public static ObjectResult Forbidden(string code = "forbidden") =>
        Problem(StatusCodes.Status403Forbidden, code, "Not allowed.");

    public static ObjectResult Conflict(string code, string title) =>
        Problem(StatusCodes.Status409Conflict, code, title);
}

// Collects field errors as codes ("required", "tooLong", ...) and renders a 400 ValidationProblem.
public class Validation
{
    private readonly Dictionary<string, string[]> _errors = new();

    public bool IsValid => _errors.Count == 0;

    public Validation Add(string field, string code)
    {
        _errors[field] = _errors.TryGetValue(field, out var existing) ? [.. existing, code] : [code];
        return this;
    }

    public Validation Text(string field, string? value, bool required, int max, int min = 0)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            if (required) Add(field, "required");
        }
        else if (trimmed.Length > max) Add(field, "tooLong");
        else if (trimmed.Length < min) Add(field, "tooShort");
        return this;
    }

    public ActionResult ToResult() =>
        new BadRequestObjectResult(new ValidationProblemDetails(_errors) { Status = 400, Title = "Validation failed." })
        {
            ContentTypes = { "application/problem+json" },
        };
}
