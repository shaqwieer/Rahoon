namespace Rahoon.Api.Seed;

/// <summary>
/// Single rule for every command that creates or destroys demo data (`seed`, `reset-demo`, startup seeding):
/// allowed only in Development or Testing. Checked before any database access, so a mistaken
/// `reset-demo` against another environment can never drop its database.
/// </summary>
public static class DemoDataGuard
{
    /// <summary>Staging is the client demo server (deploy/README.md): fictional data only, so it may be seeded and reset.</summary>
    public static bool IsAllowed(IHostEnvironment env) => env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Testing");

    public static void EnsureAllowed(IHostEnvironment env, string operation)
    {
        if (!IsAllowed(env))
            throw new InvalidOperationException(
                $"'{operation}' is only allowed in Development/Staging/Testing (current environment: {env.EnvironmentName}). No data was changed.");
    }
}
