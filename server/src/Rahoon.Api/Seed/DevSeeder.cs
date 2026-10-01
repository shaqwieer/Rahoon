using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Seed;

/// <summary>
/// Fictional demo data for development and staging: «فريق رهون» and its members, plus the marketplace demo
/// (DevSeeder.Market.cs). Names, numbers and amounts are invented. Refuses to run outside Development/Staging/Testing.
/// Idempotent: each part skips when already present. The organization directory is never seeded with demo data — it is
/// filled only by the importer from real sources (`dotnet run -- import-directory`).
/// </summary>
public sealed partial class DevSeeder(
    RahoonDbContext db,
    IPasswordHasher<User> hasher,
    FileStore files,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<DevSeeder> log,
    Infrastructure.Security.PiiProtector pii)
{
    private readonly Dictionary<string, Organization> _orgs = new();
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<(Guid Org, string Role), Role> _roles = new();

    public static readonly DateTimeOffset DemoToday = new(2026, 9, 23, 7, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync()
    {
        DemoDataGuard.EnsureAllowed(env, "seed");
        using var _ = db.Request.BeginSystemScope();
        if (!await db.Organizations.AnyAsync()) await SeedCoreAsync();
        else log.LogInformation("Core seed skipped: the Rahoon team is already present.");

        await SystemRoleSync.SyncAsync(db);
        await SeedMarketAsync();
        log.LogInformation("Seed complete.");
    }

    /// <summary>«فريق رهون» and its members.</summary>
    private async Task SeedCoreAsync()
    {
        var team = new Organization
        {
            ShortCode = "rahoon-team", NameAr = "فريق رهون", NameEn = "Rahoon Team", Initials = "فر", Kind = OrganizationKind.Operator,
            CreatedAt = DemoToday.AddYears(-1),
        };
        db.Organizations.Add(team);
        _orgs[team.ShortCode] = team;
        foreach (var t in SystemRoles.Templates)
        {
            var role = new Role { OrganizationId = team.Id, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
            role.Permissions.AddRange(t.Permissions.Distinct().Select(p => new RolePermission { PermissionKey = p }));
            db.Roles.Add(role);
            _roles[(team.Id, t.Key)] = role;
        }
        await db.SaveChangesAsync();

        User("lama", "l.alharbi@team.rahoon.example", "لمى الحربي", "0550000601", (team, SystemRoles.TeamLead, "قائدة الفريق"));
        User("nayef", "n.alyami@team.rahoon.example", "نايف اليامي", "0550000602", (team, SystemRoles.TeamCoordinator, "منسق طلبات"));
        User("turki", "t.alshehri@team.rahoon.example", "تركي الشهري", "0550000603", (team, SystemRoles.TeamCoordinator, "منسق طلبات"));
        User("abeer", "a.alqahtani@team.rahoon.example", "عبير القحطاني", "0550000604", (team, SystemRoles.TeamVerifier, "مراجِعة النشر"));
        await db.SaveChangesAsync();
    }

    private string DemoPassword => config["Seed:DemoPassword"] is { Length: >= 10 } p ? p : "Rahoon-Demo-2026!";

    private void User(string key, string email, string name, string phone, params (Organization Org, string Role, string Title)[] memberships)
    {
        var u = new User { Email = email, FullName = name, Phone = phone, MfaEnrolled = true, CreatedAt = DemoToday.AddMonths(-6) };
        u.PasswordHash = hasher.HashPassword(u, DemoPassword);
        db.Users.Add(u);
        _users[key] = u;
        foreach (var (org, role, title) in memberships)
        {
            var m = new Membership { OrganizationId = org.Id, UserId = u.Id, Title = title, CreatedAt = DemoToday.AddMonths(-6) };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = _roles[(org.Id, role)].Id });
            db.Memberships.Add(m);
        }
    }
}
