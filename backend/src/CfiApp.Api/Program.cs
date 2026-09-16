using System.Text.Json;
using System.Threading.RateLimiting;
using Asp.Versioning;
using CfiApp.Api.Filters;
using CfiApp.Api.Middleware;
using CfiApp.Api.Security;
using CfiApp.Application;
using CfiApp.Application.Abstractions;
using CfiApp.Infrastructure;
using CfiApp.Infrastructure.Persistence;
using CfiApp.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CfiApp.Infrastructure.Auth;
using CfiApp.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Bootstrap logger: without it, a failure during startup (bad connection string,
// missing configuration) would be swallowed before Serilog is configured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // ---------------------------------------------------------------- services

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Applied globally: an endpoint gets validation because its request type has a
    // validator, not because someone remembered to add an attribute.
    builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>());

    // Catches a database constraint violation (a race on a unique code, an invalid
    // reference) and turns it into 409/400 instead of a 500. Falls through to the
    // ProblemDetails writer below for everything else.
    // Order matters: handlers run in registration order, and the first one that
    // recognises the exception wins. Domain exceptions are named and specific, so they
    // are tried before the generic database-level handler.
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddExceptionHandler<ConflictExceptionHandler>();

    // Every error leaves the API in the same shape (RFC 7807), so the web and mobile
    // clients need one error handler rather than one per endpoint.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = context.HttpContext.Request.Path;
            context.ProblemDetails.Extensions["correlationId"] =
                context.HttpContext.Items[CorrelationIdMiddleware.HeaderName] as string
                ?? context.HttpContext.TraceIdentifier;
        };
    });

    builder.Services
        .AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new() { Title = "CFI App API", Version = "v1" });
    });

    var corsPolicyName = "CfiAppWebClients";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
        ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(corsPolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName));
    });

    // Rate limiting is on from day one: the login endpoint added in Phase 2 must never
    // ship without it, and adding it later is how it gets forgotten.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:Global:PermitLimit", 300),
                    Window = TimeSpan.FromMinutes(
                        builder.Configuration.GetValue("RateLimiting:Global:WindowMinutes", 1)),
                    QueueLimit = 0
                }));

        // Stricter bucket reserved for authentication endpoints (Phase 2).
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
            limiter.Window = TimeSpan.FromMinutes(
                builder.Configuration.GetValue("RateLimiting:Auth:WindowMinutes", 1));
            limiter.QueueLimit = 0;
        });
    });

    // ---------------------------------------------------------- authentication

    var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
        ?? new JwtOptions();

    var signingKey = new SymmetricSecurityKey(
        jwtOptions.ResolveKeyBytes(builder.Environment.IsDevelopment()));

    builder.Services.AddSingleton(jwtOptions);
    builder.Services.AddSingleton(new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));
    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<CfiApp.Infrastructure.Maintenance.WorkOrderService>();
    builder.Services.AddScoped<CfiApp.Infrastructure.Attendance.AttendanceService>();
    builder.Services.AddScoped<CfiApp.Infrastructure.Messaging.MessageService>();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = signingKey,
                // The default five minute grace period would keep a dismissed employee
                // signed in past the expiry the site was promised.
                ClockSkew = TimeSpan.Zero
            };
        });

    // Any [Authorize(Policy = "module.action")] resolves to a permission check without
    // needing to be registered here, so a new endpoint cannot be left unprotected by
    // forgetting a line in Program.cs.
    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
    builder.Services.AddAuthorization();

    var connectionString = builder.Configuration.GetConnectionString(
        CfiApp.Infrastructure.DependencyInjection.ConnectionStringName)!;

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);

    // ---------------------------------------------------------------- pipeline

    var app = builder.Build();

    // Migrations are a deployment step, not a startup side effect: applying schema changes
    // while instances are starting is how a factory gets a locked table mid-shift.
    // Both switches stay false in production and are turned on for local development only.
    if (builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CfiAppDbContext>();
        await context.Database.MigrateAsync();
    }

    if (builder.Configuration.GetValue("Database:RunSeedOnStartup", false))
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(options =>
    {
        // Health probes run every few seconds; logging them at Information level would
        // bury the requests that matter.
        options.GetLevel = (httpContext, _, exception) =>
            exception is not null ? Serilog.Events.LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
            : Serilog.Events.LogEventLevel.Information;
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CFI App API v1"));
    }
    else
    {
        app.UseHttpsRedirection();
    }

    app.UseCors(corsPolicyName);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    // Liveness answers "is the process up" and must not touch the database - otherwise
    // a database blip would make the orchestrator restart a healthy API.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    // Readiness answers "can this instance serve traffic", so it does check the database.
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponseAsync
    }).AllowAnonymous();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "CFI App API terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            // The exception message may contain the connection string, so only the
            // check name and status are exposed.
            error = entry.Value.Status == HealthStatus.Healthy ? null : "check failed"
        })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}

/// <summary>
/// Exposed so integration tests can host the real application through WebApplicationFactory.
/// </summary>
public partial class Program;
