using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>Creates fresh staff users so admin tests never suspend or re-role seeded people.</summary>
public static class AdminScenarios
{
    public const string StrongPassword = "Kestrel-Harbor-7741!";

    public static string UniqueEmail(string domain, string prefix = "t") => $"{prefix}.{Guid.NewGuid().ToString("N")[..10]}@{domain}";

    /// <summary>Invites through A02 as ليلى, accepts through S05 and completes the SMS step; returns the signed-in client.</summary>
    public static async Task<(TestClient Client, string Email)> InviteAndAcceptAsync(ApiFixture api, string roleKey = "case_officer", TestClient? admin = null)
    {
        admin ??= await api.LoginAsync("l.alghamdi@alufuq.example");
        var email = UniqueEmail("alufuq.example");
        var (s, inv) = await admin.PostAsync("/api/settings/invitations", new { email, fullName = "نواف سالم الرشيدي", phone = "0551239876", roleKey });
        Assert.True(s == HttpStatusCode.OK, inv?.ToJsonString());
        var token = TestClient.Str(inv, "sandboxToken");
        var c = api.Client();
        var (sa, acc) = await c.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "نواف سالم الرشيدي", password = StrongPassword, ackPolicy = true });
        Assert.True(sa == HttpStatusCode.OK, acc?.ToJsonString());
        var (sv, verify) = await c.PostAsync("/api/auth/mfa/verify", new { code = TestClient.Str(acc, "sandboxCode") });
        Assert.True(sv == HttpStatusCode.OK, verify?.ToJsonString());
        return (c, email);
    }

    public static async Task<Guid> MembershipIdAsync(ApiFixture api, string email) =>
        await api.WithDbAsync(db => db.Memberships.Where(m => m.User!.Email == email).Select(m => m.Id).FirstAsync());

    /// <summary>
    /// A user whose single (test-only) role holds every listed permission — used to prove that maker-checker guards,
    /// not missing permissions, refuse self-approval. Password is <see cref="ApiFixture.Password"/>.
    /// </summary>
    public static Task<string> UserWithPermissionsAsync(ApiFixture api, string orgShortCode, params string[] permissions) =>
        CreateUserAsync(api, orgShortCode, null, permissions);

    /// <summary>A user holding an existing role of the organization (e.g. org_admin of Sunbula, which has none seeded).</summary>
    public static Task<string> UserWithRoleAsync(ApiFixture api, string orgShortCode, string roleKey) => CreateUserAsync(api, orgShortCode, roleKey, []);

    private static async Task<string> CreateUserAsync(ApiFixture api, string orgShortCode, string? roleKey, string[] permissions)
    {
        var email = UniqueEmail(orgShortCode == "platform" ? "rahoon.example" : $"{orgShortCode}.example", "dual");
        var hasher = api.Services.GetRequiredService<IPasswordHasher<User>>();
        await api.WithDbAsync(async db =>
        {
            var org = await db.Organizations.FirstAsync(o => o.ShortCode == orgShortCode);
            var role = roleKey is null ? null : await db.Roles.FirstAsync(r => r.OrganizationId == org.Id && r.Key == roleKey);
            if (role is null)
            {
                role = new Role { OrganizationId = org.Id, Key = "test_" + Guid.NewGuid().ToString("N")[..8], NameAr = "دور اختبار مزدوج", NameEn = "Test dual role" };
                role.Permissions.AddRange(permissions.Select(p => new RolePermission { PermissionKey = p }));
                db.Roles.Add(role);
            }
            var user = new User { Email = email, FullName = "مستخدم اختبار مزدوج", Phone = "0551230000", MfaEnrolled = true };
            user.PasswordHash = hasher.HashPassword(user, ApiFixture.Password);
            db.Users.Add(user);
            var m = new Membership { OrganizationId = org.Id, UserId = user.Id, Title = "اختبار" };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = role.Id });
            db.Memberships.Add(m);
            return await db.SaveChangesAsync();
        });
        return email;
    }
}
