using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Tests.Infrastructure;
using Testcontainers.PostgreSql;

namespace Rahoon.Api.Tests;

/// <summary>
/// Upgrading a database that still holds the withdrawn model (as staging did): legacy schemas, organizations, staff and
/// grants are removed; the marketplace, the Rahoon team and individuals stay; files on disk move into the database with
/// their references, size and checksum intact, and the disk copies are deleted only after that.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MigrationTests(ApiFixture api) : IAsyncLifetime
{
    private const string BeforeRemoval = "20261001084006_MarketExitPlatform";
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").WithDatabase("rahoon_upgrade")
        .WithUsername("rahoon").WithPassword("test-only-password").Build();
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rahoon-upgrade-" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => _pg.StartAsync();

    public async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Upgrading_a_database_with_legacy_data_removes_it_and_moves_files_into_the_database()
    {
        await using var host = api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Rahoon"] = _pg.GetConnectionString(),
            ["Storage:Root"] = _root,
        })));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        using var _ = db.Request.BeginSystemScope();

        await db.GetService<IMigrator>().MigrateAsync(BeforeRemoval);

        // ── A database as the withdrawn model left it ──
        Guid team = Guid.NewGuid(), lender = Guid.NewGuid(), teamUser = Guid.NewGuid(), lenderUser = Guid.NewGuid(), person = Guid.NewGuid();
        Guid teamRole = Guid.NewGuid(), lenderRole = Guid.NewGuid(), party = Guid.NewGuid(), request = Guid.NewGuid(), obligation = Guid.NewGuid(), doc = Guid.NewGuid();
        var content = System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n% contract kept on disk\n%%EOF\n");
        var sha = Convert.ToHexStringLower(SHA256.HashData(content));
        const string key = "market-docs/0190aaaa/2026/09/contract";
        Directory.CreateDirectory(Path.Combine(_root, "market-docs", "0190aaaa", "2026", "09"));
        await File.WriteAllBytesAsync(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)), content);
        var legacyFolder = Path.Combine(_root, lender.ToString("N"), "2026", "09");
        Directory.CreateDirectory(legacyFolder);
        await File.WriteAllTextAsync(Path.Combine(legacyFolder, "deed"), "legacy case document");

        await db.Database.ExecuteSqlAsync($$"""
            INSERT INTO identity.organizations (id, name_ar, short_code, initials, kind, status, default_owner_language, idle_timeout_minutes, mfa_required, allowed_email_domains, created_at, updated_at)
            VALUES ({{team}}, 'فريق رهون', 'rahoon-team', 'فر', 'Operator', 'Active', 'ar', 30, true, '{}', now(), now()),
                   ({{lender}}, 'مصرف قديم', 'old-bank', 'مق', 'Lender', 'Active', 'ar', 30, true, '{}', now(), now());
            INSERT INTO identity.users (id, email, full_name, account_kind, status, mfa_enrolled, mfa_method, preferred_locale, numeral_style, failed_login_count, created_at, updated_at)
            VALUES ({{teamUser}}, 'team@team.rahoon.example', 'عضو الفريق', 'Staff', 'Active', true, 'Sms', 'ar', 'latn', 0, now(), now()),
                   ({{lenderUser}}, 'officer@bank.example', 'موظف المصرف', 'Staff', 'Active', true, 'Sms', 'ar', 'latn', 0, now(), now()),
                   ({{person}}, 'individual@individuals.rahoon.local', 'مالك', 'Individual', 'Active', true, 'Sms', 'ar', 'latn', 0, now(), now());
            INSERT INTO identity.roles (id, organization_id, key, name_ar, name_en)
            VALUES ({{teamRole}}, {{team}}, 'team_lead', 'قائد', 'Lead'), ({{lenderRole}}, {{lender}}, 'case_manager', 'مدير', 'Manager');
            INSERT INTO identity.role_permissions (role_id, permission_key, "grant")
            VALUES ({{teamRole}}, 'market.view', 'Allow'), ({{teamRole}}, 'request.review', 'Allow'), ({{lenderRole}}, 'case.view', 'Allow');
            INSERT INTO identity.memberships (id, organization_id, user_id, status, created_at, updated_at)
            VALUES ({{Guid.NewGuid()}}, {{team}}, {{teamUser}}, 'Active', now(), now()), ({{Guid.NewGuid()}}, {{lender}}, {{lenderUser}}, 'Active', now(), now());
            INSERT INTO cases.reference_counters (key, value) VALUES ('market:SR:2026', 7), ('case:2026', 5200);
            INSERT INTO admin.idempotency_records (key, user_id, endpoint, request_hash, status_code, created_at)
            VALUES ('k1', {{lenderUser}}, 'POST /api/cases', 'h', 200, now()), ('k2', {{person}}, 'POST /api/market/sale-requests', 'h', 200, now());
            INSERT INTO market.obligation_parties (id, kind, name_ar, active, sort_order, is_demo) VALUES ({{party}}, 'developer', 'مطور تجريبي (تجريبي)', true, 0, true);
            INSERT INTO market.sale_requests (id, reference, applicant_user_id, status, status_changed_at, answers, is_demo, organization_id, created_at, updated_at)
            VALUES ({{request}}, 'SR-2026-00007', {{person}}, 'Submitted', now(), '{}', false, {{team}}, now(), now());
            INSERT INTO market.sale_obligations (id, sale_request_id, applicant_user_id, sort_order, kind, party_id, answers, organization_id, created_at, updated_at)
            VALUES ({{obligation}}, {{request}}, {{person}}, 0, 'developer', {{party}}, '{}', {{team}}, now(), now());
            INSERT INTO market.private_documents (id, sale_request_id, applicant_user_id, kind, file_name, content_type, size_bytes, sha256, storage_key, source, uploaded_by_user_id, review_status, organization_id, created_at, updated_at)
            VALUES ({{doc}}, {{request}}, {{person}}, 'developer_contract', 'عقد.pdf', 'application/pdf', {{content.LongLength}}, {{sha}}, {{key}}, 'applicant', {{person}}, 'Pending', {{team}}, now(), now());
            """);

        // Plain EF migration would skip the file copy: the second migration refuses to drop the disk columns.
        var plain = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await db.GetService<IMigrator>().MigrateAsync("20261001140631_RemoveLegacyMortgageModel");
            await db.Database.MigrateAsync();
        });
        Assert.Contains("not yet copied", plain.Message + plain.InnerException?.Message);
        Assert.True(File.Exists(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))));

        await DatabaseMigrator.MigrateAsync(scope.ServiceProvider);
        db.ChangeTracker.Clear();

        var schemas = await db.Database.SqlQueryRaw<string>(
            "SELECT nspname AS \"Value\" FROM pg_namespace WHERE nspname NOT LIKE 'pg_%' AND nspname <> 'information_schema'").ToListAsync();
        Assert.Equal(["app", "audit", "directory", "files", "identity", "market", "public"], schemas.Order().ToList());

        Assert.Equal([team], await db.Organizations.Select(o => o.Id).ToListAsync());
        var users = await db.Users.Select(u => u.Id).ToListAsync();
        Assert.Contains(teamUser, users);
        Assert.Contains(person, users);
        Assert.DoesNotContain(lenderUser, users);
        // The team keeps its roles (the role sync adds the missing templates); the old organization's are gone.
        var roles = await db.Roles.Select(r => new { r.Id, r.OrganizationId }).ToListAsync();
        Assert.Contains(roles, r => r.Id == teamRole);
        Assert.All(roles, r => Assert.Equal(team, r.OrganizationId));
        Assert.All(await db.RolePermissions.Select(p => p.PermissionKey).ToListAsync(), k => Assert.Contains(k, Modules.Identity.P.AllKeys));
        Assert.Equal(["market:SR:2026"], await db.ReferenceCounters.Select(c => c.Key).ToListAsync());
        Assert.Equal(["k2"], await db.IdempotencyRecords.Select(r => r.Key).ToListAsync());

        // The request and its obligation survive; the fictional party is kept as typed text.
        var o = await db.SaleObligations.SingleAsync(x => x.Id == obligation);
        Assert.Null(o.PartyId);
        Assert.Equal("مطور تجريبي (تجريبي)", o.PartyOtherName);

        // The file now lives in the database, same reference, same bytes; the disk copy and the legacy folder are gone.
        var d = await db.PrivateDocuments.Include(x => x.File).SingleAsync(x => x.Id == doc);
        Assert.Equal("عقد.pdf", d.File!.FileName);
        Assert.Equal(sha, d.File.Sha256);
        Assert.Equal(content.LongLength, d.File.SizeBytes);
        var blob = await db.FileBlobs.SingleAsync(b => b.Id == Guid.Parse(d.File.StorageRef));
        Assert.Equal(content, blob.Content);
        Assert.False(File.Exists(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(Directory.Exists(Path.Combine(_root, lender.ToString("N"))));

        // Running it again is harmless.
        await DatabaseMigrator.MigrateAsync(scope.ServiceProvider);
    }
}
