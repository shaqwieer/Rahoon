using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Seed;
using Testcontainers.PostgreSql;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL container, applies the EF migrations
/// (not EnsureCreated — migrations are part of what we test) and seeds the fictional demo
/// data without the bulk synthetic portfolio.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test-Password-2026!";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("rahoon_test")
        .WithUsername("rahoon")
        .WithPassword("test-only-password")
        .Build();

    private readonly string _storage = Path.Combine(Path.GetTempPath(), "rahoon-tests-" + Guid.NewGuid().ToString("N"));

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        using (db.Request.BeginSystemScope())
        {
            // Parallel feature branches may add entities before the consolidated migration exists;
            // RAHOON_TEST_ENSURE_CREATED=1 lets them test against the model. CI always uses migrations.
            if (Environment.GetEnvironmentVariable("RAHOON_TEST_ENSURE_CREATED") == "1") await db.Database.EnsureCreatedAsync();
            else await db.Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<DevSeeder>().SeedAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Rahoon"] = _pg.GetConnectionString(),
            ["Web:AllowedOrigins:0"] = TestClient.Origin,
            ["Auth:SecureCookies"] = "false",
            ["Auth:ExposeSandboxOtp"] = "true",
            ["Seed:DemoPassword"] = Password,
            ["Seed:Bulk"] = "false",
            ["Storage:Root"] = Path.Combine(_storage, "documents"),
            ["DataProtection:KeysPath"] = Path.Combine(_storage, "keys"),
            ["Security:PiiLookupKey"] = "test-lookup-key",
            ["Database:MigrateOnStartup"] = "false",
            ["Auth:RateLimitPerMinute"] = "10000",
            ["Jobs:BreachMonitor"] = "false",
            ["Jobs:TempAccessExpiry"] = "false",
        }));
    }

    public TestClient Client() => new(CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    public async Task<TestClient> LoginAsync(string email, string? orgName = null)
    {
        var c = Client();
        await c.LoginAsync(email, Password, orgName);
        return c;
    }

    /// <summary>Direct database access for assertions (system scope, bypasses tenant filters).</summary>
    public async Task<T> WithDbAsync<T>(Func<RahoonDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        using (db.Request.BeginSystemScope()) return await action(db);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _pg.DisposeAsync();
        try { Directory.Delete(_storage, true); } catch { /* best effort */ }
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "api";
}
