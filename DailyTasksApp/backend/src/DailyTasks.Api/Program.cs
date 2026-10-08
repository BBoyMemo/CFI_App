using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DailyTasks.Api.Auth;
using DailyTasks.Api.Common;
using DailyTasks.Api.Controllers;
using DailyTasks.Api.Data;
using DailyTasks.Api.Storage;
using DailyTasks.Api.Localization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
// Machine-local secrets (e.g. the DeepL key) live here, outside git. Re-read when changed, so a
// key can be added without restarting.
config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
builder.Services.Configure<TranslationOptions>(config.GetSection(TranslationOptions.Section));
builder.Services.Configure<SiteOptions>(config.GetSection(SiteOptions.Section));
builder.Services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));

var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be set and at least 32 bytes long.");

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not set.")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AppClock>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<PhotoService>();

builder.Services.AddHttpClient<ITranslator, DeepLTranslator>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddScoped<TranslationQueue>();
builder.Services.AddScoped<TranslationProcessor>();
builder.Services.AddHostedService<TranslationWorker>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.Manager, p => p.RequireClaim("role", nameof(DailyTasks.Api.Domain.Role.Manager)))
    .AddPolicy(Policies.Engineer, p => p.RequireClaim("role", nameof(DailyTasks.Api.Domain.Role.Engineer)));

var loginAttemptsPerMinute = config.GetValue("RateLimit:LoginPerMinute", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimits.Login, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginAttemptsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddProblemDetails();
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.UseStatusCodePages();

// In a single-server deployment the built web app is copied to wwwroot and served from here.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapFallback("/api/{**rest}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();

public partial class Program;
