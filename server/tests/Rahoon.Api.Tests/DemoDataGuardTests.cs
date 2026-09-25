using System.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Rahoon.Api.Seed;

namespace Rahoon.Api.Tests;

/// <summary>
/// Demo-data commands must be refused outside Development/Testing before any database access
/// (a `reset-demo` used to drop the database before the seeder's environment check).
/// </summary>
public sealed class DemoDataGuardTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Rahoon.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    [InlineData("Staging", false)]
    public void Demo_data_is_allowed_only_in_development_and_testing(string environment, bool allowed)
    {
        Assert.Equal(allowed, DemoDataGuard.IsAllowed(new Env(environment)));
        if (allowed) DemoDataGuard.EnsureAllowed(new Env(environment), "seed");
        else Assert.Throws<InvalidOperationException>(() => DemoDataGuard.EnsureAllowed(new Env(environment), "seed"));
    }

    [Theory]
    [InlineData("reset-demo")]
    [InlineData("seed")]
    public async Task Cli_refuses_demo_commands_in_production_before_touching_the_database(string command)
    {
        // Points at a closed port: if the command reached the database it would fail with a connection error instead.
        var dll = Path.Combine(AppContext.BaseDirectory, "Rahoon.Api.dll");
        var psi = new ProcessStartInfo("dotnet", $"\"{dll}\" {command}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["DOTNET_ENVIRONMENT"] = "Production";
        psi.Environment["ConnectionStrings__Rahoon"] = "Host=127.0.0.1;Port=1;Database=must_not_be_touched;Username=x;Password=x;Timeout=2";

        using var p = Process.Start(psi)!;
        var stderr = p.StandardError.ReadToEndAsync();
        var stdout = p.StandardOutput.ReadToEndAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await p.WaitForExitAsync(cts.Token);

        var output = await stderr + await stdout;
        Assert.Equal(1, p.ExitCode);
        Assert.Contains("refused", output);
        Assert.Contains("No data was changed", output);
        Assert.DoesNotContain("Npgsql", output);
    }
}
