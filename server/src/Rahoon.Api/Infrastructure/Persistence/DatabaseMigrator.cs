using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Infrastructure.Persistence;

/// <summary>
/// The one way the schema is brought up to date (CLI `migrate`, migrate-on-startup, tests). Applies migrations up to the
/// legacy-removal migration, copies files still kept on disk into the database (verifying size and checksum) — the next
/// migration refuses to run while any marketplace file is not copied — then applies the rest and syncs the team roles.
/// Caller supplies the system scope.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>Adds files.* and the nullable file ids; the next migration drops the disk-path columns.</summary>
    public const string FilesBridgeMigration = "RemoveLegacyMortgageModel";

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<RahoonDbContext>();
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("Rahoon.Migrate");
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        var bridge = pending.FirstOrDefault(m => m.EndsWith("_" + FilesBridgeMigration, StringComparison.Ordinal));
        if (bridge is not null)
        {
            log.LogInformation("Applying migrations up to {Migration}", bridge);
            await db.GetService<IMigrator>().MigrateAsync(bridge, ct);
        }
        await LegacyDiskFileMigrator.RunAsync(db, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>(), log, ct);
        await db.Database.MigrateAsync(ct);
        await SystemRoleSync.SyncAsync(db);
    }
}
