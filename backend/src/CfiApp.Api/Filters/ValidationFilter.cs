using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CfiApp.Api.Filters;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, and turns
/// failures into the same RFC 7807 shape every other error uses.
///
/// Applied globally rather than per action: a new endpoint gets validation because its
/// request type has a validator, not because someone remembered an attribute.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, string[]>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (services.GetService(validatorType) is not IValidator validator) continue;

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid) continue;

            foreach (var group in result.Errors.GroupBy(error => error.PropertyName))
            {
                errors[group.Key] = [.. group.Select(error => error.ErrorMessage)];
            }
        }

        if (errors.Count > 0)
        {
            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Title = "One or more fields need attention",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            });

            return;
        }

        await next();
    }
}
