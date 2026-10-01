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
        await SeedStaffAsync();
        await SeedMarketAsync();
        log.LogInformation("Seed complete.");
    }

    /// <summary>«فريق رهون» (its system roles come from SystemRoleSync).</summary>
    private async Task SeedCoreAsync()
    {
        var team = new Organization
        {
            ShortCode = "rahoon-team", NameAr = "فريق رهون", NameEn = "Rahoon Team", Initials = "فر", Kind = OrganizationKind.Operator,
            CreatedAt = DemoToday.AddYears(-1),
        };
        db.Organizations.Add(team);
        await db.SaveChangesAsync();
    }

    /// <summary>Demo members, one per default role (two case managers). Each is added only if its e-mail is missing.</summary>
    private async Task SeedStaffAsync()
    {
        var team = await db.Organizations.FirstAsync(o => o.ShortCode == "rahoon-team");
        _orgs[team.ShortCode] = team;
        foreach (var r in await db.Roles.Where(r => r.OrganizationId == team.Id).ToListAsync()) _roles[(team.Id, r.Key)] = r;
        var existing = (await db.Users.Select(u => u.Email).ToListAsync()).ToHashSet();

        void Add(string key, string email, string name, string phone, string role, string title)
        {
            if (existing.Contains(email)) return;
            User(key, email, name, phone, (team, role, title));
        }
        Add("lama", "l.alharbi@team.rahoon.example", "لمى الحربي", "0550000601", SystemRoles.PlatformOwner, "قائدة الفريق");
        Add("nayef", "n.alyami@team.rahoon.example", "نايف اليامي", "0550000602", SystemRoles.CaseManager, "مسؤول ملفات");
        Add("turki", "t.alshehri@team.rahoon.example", "تركي الشهري", "0550000603", SystemRoles.CaseManager, "مسؤول ملفات");
        Add("abeer", "a.alqahtani@team.rahoon.example", "عبير القحطاني", "0550000604", SystemRoles.Publisher, "مسؤولة النشر");
        Add("fahad", "f.alotaibi@team.rahoon.example", "فهد العتيبي", "0550000605", SystemRoles.OperationsManager, "مدير العمليات");
        Add("huda", "h.alzahrani@team.rahoon.example", "هدى الزهراني", "0550000606", SystemRoles.DocumentReviewer, "مراجِعة مستندات");
        Add("majed", "m.alghamdi@team.rahoon.example", "ماجد الغامدي", "0550000607", SystemRoles.FinanceOfficer, "مسؤول مالي");
        Add("reem", "r.almutairi@team.rahoon.example", "ريم المطيري", "0550000608", SystemRoles.SupportAgent, "دعم العملاء");
        Add("sara", "s.aldosari@team.rahoon.example", "سارة الدوسري", "0550000609", SystemRoles.Auditor, "مدققة");
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
