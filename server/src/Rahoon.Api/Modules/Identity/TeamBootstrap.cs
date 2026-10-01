using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Audit;

namespace Rahoon.Api.Modules.Identity;

/// <summary>
/// Controlled setup of the first platform owner, from the server shell only:
/// <c>dotnet Rahoon.Api.dll bootstrap-owner --email a@b.sa --name "الاسم" --phone 05xxxxxxxx</c>.
/// Creates «فريق رهون» and its system roles if missing, then a platform-owner invitation, and prints its one-time link.
/// Refuses while the team has an active platform owner (they invite from the console). There is no public endpoint, no
/// default password and no automatic privilege for existing users.
/// </summary>
public static class TeamBootstrap
{
    public static async Task<bool> HasActiveOwnerAsync(RahoonDbContext db, Guid orgId) =>
        await db.Memberships.AnyAsync(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active
                                           && m.Roles.Any(r => r.Role!.Key == SystemRoles.PlatformOwner && r.Role.ArchivedAt == null));

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        var email = (Arg("--email") ?? "").Trim().ToLowerInvariant();
        var name = PhoneAuthEndpoints.CleanName(Arg("--name"));
        var phone = PhoneAuthEndpoints.NormalizeMobile(Arg("--phone"));
        if (!email.Contains('@') || name is null || phone is null)
        {
            Console.Error.WriteLine("bootstrap-owner: usage: bootstrap-owner --email <work e-mail> --name \"<full name>\" --phone <05xxxxxxxx>. No data was changed.");
            return 2;
        }

        var db = services.GetRequiredService<RahoonDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        using var _ = db.Request.BeginSystemScope();
        var team = await db.Organizations.FirstOrDefaultAsync(o => o.Kind == OrganizationKind.Operator);
        if (team is null)
        {
            team = new Organization { ShortCode = "rahoon-team", NameAr = "فريق رهون", NameEn = "Rahoon Team", Initials = "فر", Kind = OrganizationKind.Operator };
            db.Organizations.Add(team);
            await db.SaveChangesAsync();
        }
        await SystemRoleSync.SyncAsync(db);

        await using var tx = await db.Database.BeginTransactionAsync();
        var key = "team-admin:" + team.Id;
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))");
        if (await HasActiveOwnerAsync(db, team.Id))
        {
            Console.Error.WriteLine("bootstrap-owner: refused — the team already has an active platform owner. Invite new members from the console (/team/members). No data was changed.");
            return 3;
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var old in await db.StaffInvitations.Where(i => i.OrganizationId == team.Id && i.Email == email && i.Status == InvitationStatus.Pending).ToListAsync())
        {
            old.Status = InvitationStatus.Revoked;
            old.RevokedAt = now;
        }
        await db.SaveChangesAsync();
        var owner = await db.Roles.FirstAsync(r => r.OrganizationId == team.Id && r.Key == SystemRoles.PlatformOwner);
        var token = Tokens.NewToken();
        var inv = new StaffInvitation
        {
            OrganizationId = team.Id, Email = email, FullName = name, Phone = phone, Title = "مالك المنصة", RoleIds = [owner.Id],
            TokenHash = Tokens.Sha256(token), InvitedByUserId = Guid.Empty, InvitedByLabel = "تهيئة النظام", CreatedAt = now,
            ExpiresAt = now.AddHours(TeamAdminEndpoints.InvitationHours(config)),
        };
        db.StaffInvitations.Add(inv);
        await services.GetRequiredService<AuditLog>().RecordAsync(new AuditEntry("team.bootstrap_invitation", $"دعوة تهيئة لمالك المنصة {name}",
            OrganizationId: team.Id, Detail: Mask.Email(email), SubjectType: "team_invitation", SubjectReference: inv.Id.ToString()));
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        Console.WriteLine($"bootstrap-owner: invitation created for {Mask.Email(email)}, valid until {inv.ExpiresAt:u}.");
        Console.WriteLine("Open this link on the site (prefix it with the site's address, e.g. https://rahoon.example) and set a password:");
        Console.WriteLine("  " + TeamAdminEndpoints.JoinLink(token));
        Console.WriteLine("The link is shown once and was not sent anywhere. Sign-in then needs the password and an SMS code to the given mobile.");
        return 0;
    }
}
