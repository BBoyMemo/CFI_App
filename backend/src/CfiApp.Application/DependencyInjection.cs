using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CfiApp.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Validators are discovered by convention, so adding a new request DTO with a
        // validator next to it is enough - no registration list to keep in sync.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        return services;
    }
}
