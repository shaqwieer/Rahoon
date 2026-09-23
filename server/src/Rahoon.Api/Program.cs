using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Seed;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ── Persistence ──
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<TenantWriteGuardInterceptor>();
builder.Services.AddDbContext<RahoonDbContext>((sp, o) =>
{
    var cs = config.GetConnectionString("Rahoon") ?? throw new InvalidOperationException("ConnectionStrings:Rahoon is not configured.");
    o.UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations", "public"))
        .UseSnakeCaseNamingConvention()
        .AddInterceptors(sp.GetRequiredService<TenantWriteGuardInterceptor>());
});

// ── Security ──
var keysDir = config["DataProtection:KeysPath"] is { Length: > 0 } k
    ? (Path.IsPathRooted(k) ? k : Path.Combine(builder.Environment.ContentRootPath, k))
    : Path.Combine(builder.Environment.ContentRootPath, ".data", "keys");
builder.Services.AddDataProtection().SetApplicationName("rahoon").PersistKeysToFileSystem(new DirectoryInfo(keysDir));
builder.Services.AddSingleton<PiiProtector>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
// Resolved lazily so test hosts and environment overrides apply.
builder.Services.AddSingleton(sp => sp.GetRequiredService<IConfiguration>().GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions());
builder.Services.AddScoped<SessionService>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = config.GetValue("Auth:RateLimitPerMinute", 20), Window = TimeSpan.FromMinutes(1) }));
});

// ── Infrastructure services ──
builder.Services.AddScoped<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddScoped<IFileScanner, BasicSignatureScanner>();
builder.Services.AddScoped<IIntegrationRegistry, IntegrationRegistry>();
builder.Services.AddScoped<ISmsGateway, SandboxSmsGateway>();
builder.Services.AddSingleton<ILicensedSigningProvider, UnavailableSigningProvider>();
builder.Services.AddSingleton<ILicensedPaymentProvider, UnavailablePaymentProvider>();
builder.Services.AddSingleton<INationalIdentityProvider, UnavailableIdentityProvider>();
builder.Services.AddSingleton<IJudicialChannel, UnavailableJudicialChannel>();
builder.Services.AddScoped<AuditLog>();
builder.Services.AddScoped<CaseWorkflow>();
builder.Services.AddRahoonModules();
builder.Services.AddScoped<DevSeeder>();

builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── CLI: `dotnet run -- migrate` / `seed` / `reset-demo` ──
if (args.Length > 0 && args[0] is "migrate" or "seed" or "reset-demo")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
    using (db.Request.BeginSystemScope())
    {
        if (args[0] == "reset-demo") await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        if (args[0] is "seed" or "reset-demo") await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
    }
    Console.WriteLine($"{args[0]}: done");
    return;
}

if (app.Environment.IsDevelopment() && config.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
    using (db.Request.BeginSystemScope())
    {
        await db.Database.MigrateAsync();
        if (config.GetValue("Database:SeedOnStartup", false)) await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
    }
}

app.UseExceptionHandler();
app.Use(async (http, next) =>
{
    // App data is private: no caching, no indexing, no framing.
    var h = http.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "same-origin";
    h["Cache-Control"] = "no-store";
    h["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});
app.UseRateLimiter();
app.UseMiddleware<RequestContextMiddleware>();

app.MapHealthChecks("/api/health");
app.MapRahoonEndpoints();

app.Run();

public partial class Program;
