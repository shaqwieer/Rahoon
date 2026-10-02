using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1.5 — team membership, roles, grants with resource scope and their enforcement. Every check runs as a
/// non-owner actor with genuinely different grants (two case managers, a document reviewer, a publisher, an operations
/// manager, an auditor, support), not only as the platform owner. Members that a test suspends, removes or re-roles are
/// always fresh members created through a real invitation, so the seeded team stays intact for other tests.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TeamAccessTests(ApiFixture api)
{
    private const string Password = "Member-Pass-2026";
    private static int _seq;

    private sealed record NewMember(TestClient Client, Guid MembershipId, Guid UserId, string Email);

    private static async Task<Dictionary<string, Guid>> RoleIdsAsync(TestClient admin)
    {
        var (_, roles) = await Ok(admin.GetAsync("/api/team/admin/roles"));
        return roles!["items"]!.AsArray().ToDictionary(r => r!["key"]!.GetValue<string>(), r => Guid.Parse(r!["id"]!.GetValue<string>()));
    }

    /// <summary>Invites (as the platform owner unless given), accepts with a password and signs in a fresh member.</summary>
    private async Task<NewMember> NewMemberAsync(params string[] roleKeys)
    {
        var n = Interlocked.Increment(ref _seq);
        var email = $"member{n}.{Guid.NewGuid().ToString("N")[..6]}@team.rahoon.example";
        var lead = await api.LoginAsync(Lead);
        var ids = await RoleIdsAsync(lead);
        var (_, inv) = await Ok(lead.PostAsync("/api/team/admin/invitations", new
        {
            email, fullName = $"عضو اختبار {n}", phone = $"0568{n:D6}", title = "اختبار", roleIds = roleKeys.Select(k => ids[k]).ToArray(),
        }));
        var token = TestClient.Str(inv, "link").Split('#')[1];
        await Ok(api.Client().PostAsync("/api/auth/invitations/accept", new { token, password = Password }));
        var client = api.Client();
        await client.LoginAsync(email, Password);
        var (userId, membershipId) = await api.WithDbAsync(async db =>
        {
            var u = await db.Users.FirstAsync(x => x.Email == email);
            var m = await db.Memberships.FirstAsync(x => x.UserId == u.Id);
            return (u.Id, m.Id);
        });
        return new NewMember(client, membershipId, userId, email);
    }

    private static string InviteToken(JsonNode? issued) => TestClient.Str(issued, "link").Split('#')[1];

    // ── 1. Assigned scope: a case manager can't reach another manager's file by changing an id ──

    [Fact]
    public async Task Case_manager_reviews_assigned_work_and_cannot_reach_another_managers_file()
    {
        var (owner, mine) = await SubmittedAsync(api);
        var (_, other) = await SubmittedAsync(api);
        var (_, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{mine}/documents", DocForm());
        await AssignAsync(api, $"sale-requests/{mine}", Coordinator);
        await AssignAsync(api, $"sale-requests/{other}", Coordinator2);

        var cm = await api.LoginAsync(Coordinator);
        await Ok(cm.GetAsync($"/api/team/market/sale-requests/{mine}"));
        await Ok(cm.PostAsync($"/api/team/market/sale-requests/{mine}/start-review"));
        Assert.Equal(HttpStatusCode.OK, (await cm.GetBytesAsync(TestClient.Str(doc, "url"))).Status);

        // The other manager's request: list, detail, contact, mutation — none of it is reachable.
        var (_, list) = await Ok(cm.GetAsync("/api/team/market/sale-requests?pageSize=100"));
        Assert.Contains(list!["items"]!.AsArray(), r => TestClient.Str(r, "reference") == mine);
        Assert.DoesNotContain(list["items"]!.AsArray(), r => TestClient.Str(r, "reference") == other);
        Assert.Equal(HttpStatusCode.NotFound, (await cm.GetAsync($"/api/team/market/sale-requests/{other}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await cm.GetAsync($"/api/team/market/sale-requests/{other}/contact")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await cm.PostAsync($"/api/team/market/sale-requests/{other}/start-review")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await cm.PostAsync($"/api/team/market/sale-requests/{other}/assign", new { })).Status);
        // ...and the second manager can't open the first one's private document.
        var cm2 = await api.LoginAsync(Coordinator2);
        Assert.Equal(HttpStatusCode.NotFound, (await cm2.GetBytesAsync(TestClient.Str(doc, "url"))).Status);

        // Counts come from the same scope: no unassigned request is counted for a case manager.
        var (_, overview) = await Ok(cm.GetAsync("/api/team/market/overview"));
        Assert.Equal(0, overview!["unassigned"]!["sale"]!.GetValue<int>());
        Assert.Equal("assigned", TestClient.Str(overview["scopes"], "market.view"));
    }

    private static MultipartFormDataContent DocForm()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n" + Guid.NewGuid()));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "statement.pdf");
        form.Add(new StringContent("payment_proof"), "kind");
        return form;
    }

    // ── 2. Separation of duties ──

    [Fact]
    public async Task Reviewer_verifies_evidence_but_cannot_decide_or_publish_and_publisher_cannot_manage_roles()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var (_, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", DocForm());
        await AssignAsync(api, $"sale-requests/{reference}", Reviewer);
        var reviewer = await api.LoginAsync(Reviewer);
        await Ok(reviewer.PostAsync($"/api/team/market/sale-requests/{reference}/start-review"));
        var (_, detail) = await Ok(reviewer.GetAsync($"/api/team/market/sale-requests/{reference}"));
        var obligationId = detail!["file"]!["obligations"]![0]!["id"]!.GetValue<string>();
        await Ok(reviewer.PostAsync($"/api/team/market/sale-requests/{reference}/verify-figure", new
        {
            fieldKey = $"o:{obligationId}:paid_approved", value = "300000", source = "developer_statement", sourceDate = "2026-09-01",
        }));
        await Ok(reviewer.PostAsync($"/api/team/market/sale-requests/{reference}/documents/{TestClient.Str(doc, "id")}/review", new { status = "accepted" }));
        Assert.False(detail["actions"]!["approve"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsync($"/api/team/market/sale-requests/{reference}/approve", new { reason = "" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsync($"/api/team/market/sale-requests/{reference}/reject", new { reason = "سبب كاف للرفض" })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.GetAsync($"/api/team/market/sale-requests/{reference}/contact")).Status);

        // Publishing is market.publish, which the reviewer doesn't hold for any opportunity.
        var (pubOwner, pubRef) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, pubOwner, pubRef);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsync($"/api/team/market/opportunities/{op}/pause", new { reason = "إيقاف للاختبار" })).Status);

        var publisher = await api.LoginAsync(Verifier);
        Assert.Equal(HttpStatusCode.Forbidden, (await publisher.GetAsync("/api/team/admin/members")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await publisher.PostAsync("/api/team/admin/roles", new { nameAr = "دور", grants = new[] { new { key = "market.view", scope = "all" } } })).Status);
        // A publisher sees the request but not its private document.
        Assert.Equal(HttpStatusCode.NotFound, (await publisher.GetBytesAsync(TestClient.Str(doc, "url"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await publisher.PostAsync($"/api/team/market/sale-requests/{reference}/approve", new { reason = "" })).Status);
    }

    // ── 3. Changes apply to an already-issued session ──

    [Fact]
    public async Task Role_change_and_suspension_apply_to_an_existing_session_on_mutations_and_downloads()
    {
        var member = await NewMemberAsync(SystemRoles.OperationsManager);
        var (owner, reference) = await SubmittedAsync(api);
        var (_, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", DocForm());
        var url = TestClient.Str(doc, "url");
        Assert.Equal(HttpStatusCode.OK, (await member.Client.GetBytesAsync(url)).Status);

        var lead = await api.LoginAsync(Lead);
        var ids = await RoleIdsAsync(lead);
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { ids[SystemRoles.Publisher] }, reason = "نقل إلى النشر" }));
        // Same cookie, no new sign-in: the document and the review action are gone at once.
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetBytesAsync(url)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PostAsync($"/api/team/market/sale-requests/{reference}/start-review")).Status);
        var (_, me) = await member.Client.GetAsync("/api/auth/me");
        Assert.DoesNotContain(me!["permissions"]!.AsArray(), p => p!.GetValue<string>() == P.DocumentsRead);

        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/suspend", new { reason = "إيقاف للاختبار" }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.Client.GetAsync("/api/team/market/overview")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetBytesAsync(url)).Status);
        var revoked = await api.WithDbAsync(db => db.Sessions.Where(s => s.MembershipId == member.MembershipId).AllAsync(s => s.RevokedAt != null));
        Assert.True(revoked);
        // Signing in again is refused while suspended.
        var (s, _) = await api.Client().PostAsync("/api/auth/login", new { email = member.Email, password = Password });
        Assert.Equal(HttpStatusCode.Forbidden, s);
    }

    // ── 4. Public accounts never become staff ──

    [Fact]
    public async Task A_public_user_cannot_gain_staff_access_by_forged_fields()
    {
        var person = await SellerAsync(api);
        var (_, me) = await person.GetAsync("/api/auth/me");
        Assert.Empty(me!["permissions"]!.AsArray());
        Assert.Equal("/account", TestClient.Str(me, "home"));
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/team/admin/members")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await person.PostAsync("/api/team/admin/invitations", new { email = "x@y.sa", fullName = "شخص", phone = "0567000001", roleIds = Array.Empty<Guid>() })).Status);
        // Extra fields on a customer endpoint are ignored, never bound to roles.
        await person.PostAsync("/api/market/buyer-requests", new
        {
            clientDraftId = Guid.NewGuid(), availableNow = 1000, purchaseMode = "cash", cities = new[] { "riyadh" }, propertyTypes = new[] { "apartment" },
            roles = new[] { "platform_owner" }, accountKind = "Staff", permissions = new[] { "team.manage" },
        });
        var (_, after) = await person.GetAsync("/api/auth/me");
        Assert.Empty(after!["permissions"]!.AsArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/team/market/sale-requests")).Status);
    }

    // ── 5. Invitations ──

    [Fact]
    public async Task Invitations_are_single_use_expire_can_be_revoked_and_never_duplicate()
    {
        var lead = await api.LoginAsync(Lead);
        var ids = await RoleIdsAsync(lead);
        var email = $"invitee.{Guid.NewGuid().ToString("N")[..8]}@team.rahoon.example";
        var body = new { email, fullName = "مدعو اختبار", phone = "0567000123", roleIds = new[] { ids[SystemRoles.CaseManager] } };
        var (_, issued) = await Ok(lead.PostAsync("/api/team/admin/invitations", body));
        Assert.False(issued!["delivered"]!.GetValue<bool>());
        var token = InviteToken(issued);
        // The token is never stored in clear (only its hash), not even in the idempotency records.
        Assert.False(await api.WithDbAsync(db => db.IdempotencyRecords.AnyAsync(r => r.ResponseJson != null && r.ResponseJson.Contains(token))));

        // Double submit → conflict, still one pending invitation.
        var (dup, dupBody) = await lead.PostAsync("/api/team/admin/invitations", body);
        Assert.Equal(HttpStatusCode.Conflict, dup);
        Assert.Equal("duplicate_invitation", TestClient.Str(dupBody, "code"));
        Assert.Equal(1, await api.WithDbAsync(db => db.StaffInvitations.CountAsync(i => i.Email == email)));

        // The invitee cannot choose roles: extra fields are ignored; the invitation's role is what they get.
        var (_, look) = await Ok(api.Client().PostAsync("/api/auth/invitations/lookup", new { token }));
        Assert.Equal(email, TestClient.Str(look, "email"));
        var (weak, _) = await api.Client().PostAsync("/api/auth/invitations/accept", new { token, password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, weak);
        await Ok(api.Client().PostAsync("/api/auth/invitations/accept", new { token, password = Password, roleIds = new[] { ids[SystemRoles.PlatformOwner] } }));
        var keys = await api.WithDbAsync(db => db.Memberships.Where(m => m.User!.Email == email).SelectMany(m => m.Roles.Select(r => r.Role!.Key)).ToListAsync());
        Assert.Equal([SystemRoles.CaseManager], keys);

        // Used → fails; a second accept never creates a second membership.
        var (used, usedBody) = await api.Client().PostAsync("/api/auth/invitations/accept", new { token, password = Password });
        Assert.Equal(HttpStatusCode.Gone, used);
        Assert.Equal("invitation_invalid", TestClient.Str(usedBody, "code"));
        Assert.Equal(1, await api.WithDbAsync(db => db.Memberships.CountAsync(m => m.User!.Email == email)));

        // Revoked → fails.
        var email2 = $"revoked.{Guid.NewGuid().ToString("N")[..8]}@team.rahoon.example";
        var (_, second) = await Ok(lead.PostAsync("/api/team/admin/invitations", body with { email = email2 }));
        await Ok(lead.PostAsync($"/api/team/admin/invitations/{TestClient.Str(second, "id")}/revoke"));
        Assert.Equal(HttpStatusCode.Gone, (await api.Client().PostAsync("/api/auth/invitations/accept", new { token = InviteToken(second), password = Password })).Status);

        // Expired → fails; renewing issues a new link and the old one stays dead.
        var email3 = $"expired.{Guid.NewGuid().ToString("N")[..8]}@team.rahoon.example";
        var (_, third) = await Ok(lead.PostAsync("/api/team/admin/invitations", body with { email = email3 }));
        var thirdId = Guid.Parse(TestClient.Str(third, "id"));
        await api.WithDbAsync(async db =>
        {
            await db.StaffInvitations.Where(i => i.Id == thirdId).ExecuteUpdateAsync(u => u.SetProperty(i => i.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            return 0;
        });
        Assert.Equal(HttpStatusCode.Gone, (await api.Client().PostAsync("/api/auth/invitations/lookup", new { token = InviteToken(third) })).Status);
        var (_, renewed) = await Ok(lead.PostAsync($"/api/team/admin/invitations/{thirdId}/renew"));
        Assert.Equal(HttpStatusCode.Gone, (await api.Client().PostAsync("/api/auth/invitations/lookup", new { token = InviteToken(third) })).Status);
        await Ok(api.Client().PostAsync("/api/auth/invitations/lookup", new { token = InviteToken(renewed) }));
    }

    [Fact]
    public async Task Invitation_roles_must_be_within_the_inviters_authority()
    {
        // An operations manager holding team.manage through a custom role still can't invite a platform owner.
        var lead = await api.LoginAsync(Lead);
        var ids = await RoleIdsAsync(lead);
        var (_, created) = await Ok(lead.PostAsync("/api/team/admin/roles", new
        {
            nameAr = $"مسؤول توظيف {Guid.NewGuid().ToString("N")[..4]}",
            grants = new[] { new { key = P.TeamRead, scope = "all" }, new { key = P.TeamManage, scope = "all" }, new { key = P.MarketView, scope = "assigned" } },
        }));
        var hr = await NewMemberAsync(SystemRoles.CaseManager);
        await Ok(lead.PostAsync($"/api/team/admin/members/{hr.MembershipId}/roles", new { roleIds = new[] { Guid.Parse(TestClient.Str(created, "id")), ids[SystemRoles.CaseManager] } }));

        var (s1, b1) = await hr.Client.PostAsync("/api/team/admin/invitations", new
        {
            email = $"x.{Guid.NewGuid().ToString("N")[..6]}@team.rahoon.example", fullName = "مدعو", phone = "0567000777", roleIds = new[] { ids[SystemRoles.PlatformOwner] },
        });
        Assert.Equal(HttpStatusCode.Forbidden, s1);
        Assert.Equal("grant_exceeds_authority", TestClient.Str(b1, "code"));
        // Within authority (a case manager: same grants and scopes it holds) is fine.
        await Ok(hr.Client.PostAsync("/api/team/admin/invitations", new
        {
            email = $"y.{Guid.NewGuid().ToString("N")[..6]}@team.rahoon.example", fullName = "مدعو آخر", phone = "0567000778", roleIds = new[] { ids[SystemRoles.CaseManager] },
        }));
        // ...but not to act on a member who holds more than they do, nor on themselves.
        var lamaMembership = await api.WithDbAsync(db => db.Memberships.Where(m => m.User!.Email == Lead).Select(m => m.Id).FirstAsync());
        var (s2, b2) = await hr.Client.PostAsync($"/api/team/admin/members/{lamaMembership}/suspend", new { reason = "محاولة غير مسموحة" });
        Assert.Equal(HttpStatusCode.Forbidden, s2);
        Assert.Equal("insufficient_authority", TestClient.Str(b2, "code"));
        var (s3, b3) = await hr.Client.PostAsync($"/api/team/admin/members/{hr.MembershipId}/roles", new { roleIds = new[] { ids[SystemRoles.PlatformOwner] } });
        Assert.Equal(HttpStatusCode.Forbidden, s3);
        Assert.Equal("self_change", TestClient.Str(b3, "code"));
    }

    // ── 6. Last owner and scope combination ──

    [Fact]
    public async Task The_last_active_owner_cannot_be_suspended_removed_or_lose_the_role()
    {
        // A separate operator team with exactly two owners makes "last owner" real without touching the seeded team.
        var (orgId, ownerA, ownerB) = await api.WithDbAsync(async db =>
        {
            var org = new Organization { ShortCode = "t-" + Guid.NewGuid().ToString("N")[..8], NameAr = "فريق اختبار", Initials = "فا", Kind = OrganizationKind.Operator };
            db.Organizations.Add(org);
            await db.SaveChangesAsync();
            await SystemRoleSync.SyncAsync(db);
            var owner = await db.Roles.FirstAsync(r => r.OrganizationId == org.Id && r.Key == SystemRoles.PlatformOwner);
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
            Guid Add(string tag)
            {
                var u = new User { Email = $"{tag}.{Guid.NewGuid().ToString("N")[..6]}@owners.rahoon.example", FullName = $"مالك {tag}", Phone = "0567001000" };
                u.PasswordHash = hasher.HashPassword(u, Password);
                db.Users.Add(u);
                var m = new Membership { OrganizationId = org.Id, UserId = u.Id };
                m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = owner.Id });
                db.Memberships.Add(m);
                return m.Id;
            }
            var a = Add("a");
            var b = Add("b");
            await db.SaveChangesAsync();
            return (org.Id, a, b);
        });
        var emails = await api.WithDbAsync(db => db.Memberships.Where(m => m.OrganizationId == orgId).OrderBy(m => m.Id == ownerA ? 0 : 1).Select(m => m.User!.Email).ToListAsync());
        var clientA = api.Client(); await clientA.LoginAsync(emails[0], Password);
        var clientB = api.Client(); await clientB.LoginAsync(emails[1], Password);

        // Concurrent: A suspends B while B suspends A. Exactly one wins; one active owner always remains.
        var r = await Task.WhenAll(
            clientA.PostAsync($"/api/team/admin/members/{ownerB}/suspend", new { reason = "تزامن اختبار" }),
            clientB.PostAsync($"/api/team/admin/members/{ownerA}/suspend", new { reason = "تزامن اختبار" }));
        Assert.Single(r, x => x.Status == HttpStatusCode.OK);
        var active = await api.WithDbAsync(db => db.Memberships.Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active).Select(m => m.Id).ToListAsync());
        Assert.Single(active);

        // The survivor is now the last owner. Give the other member every permission through a custom role (not the owner
        // role): even with full authority they can't suspend, remove or strip the last owner.
        var survivor = active[0];
        var winner = survivor == ownerA ? clientA : clientB;
        var other = survivor == ownerA ? ownerB : ownerA;
        await Ok(winner.PostAsync($"/api/team/admin/members/{other}/reactivate", new { reason = "إعادة للتجربة" }));
        var (_, everything) = await Ok(winner.PostAsync("/api/team/admin/roles", new
        {
            nameAr = "كل الصلاحيات", grants = P.Catalog.Where(p => !p.Reserved).Select(p => new { key = p.Key, scope = "all" }).ToArray(),
        }));
        var everythingId = Guid.Parse(TestClient.Str(everything, "id"));
        await Ok(winner.PostAsync($"/api/team/admin/members/{other}/roles", new { roleIds = new[] { everythingId } }));
        var full = api.Client();
        await full.LoginAsync(emails[survivor == ownerA ? 1 : 0], Password);

        var (s1, b1) = await full.PostAsync($"/api/team/admin/members/{survivor}/suspend", new { reason = "محاولة إيقاف" });
        Assert.Equal(HttpStatusCode.Conflict, s1);
        Assert.Equal("last_owner", TestClient.Str(b1, "code"));
        var (s2, b2) = await full.PostAsync($"/api/team/admin/members/{survivor}/remove", new { reason = "محاولة إزالة" });
        Assert.Equal(HttpStatusCode.Conflict, s2);
        Assert.Equal("last_owner", TestClient.Str(b2, "code"));
        var (s3, b3) = await full.PostAsync($"/api/team/admin/members/{survivor}/roles", new { roleIds = new[] { everythingId } });
        Assert.Equal(HttpStatusCode.Conflict, s3);
        Assert.Equal("last_owner", TestClient.Str(b3, "code"));
        // Nor can the owner change themself.
        var (s4, b4) = await winner.PostAsync($"/api/team/admin/members/{survivor}/remove", new { reason = "محاولة" });
        Assert.Equal(HttpStatusCode.Forbidden, s4);
        Assert.Equal("self_change", TestClient.Str(b4, "code"));
        var (_, mine) = await Ok(winner.GetAsync($"/api/team/admin/members/{survivor}"));
        Assert.True(mine!["lastOwner"]!.GetValue<bool>());
        Assert.False(mine["actions"]!["suspend"]!.GetValue<bool>());

        // With a second owner, the first may step down: the check counts active owners.
        var roles = await RoleIdsAsync(winner);
        await Ok(winner.PostAsync($"/api/team/admin/members/{other}/roles", new { roleIds = new[] { roles[SystemRoles.PlatformOwner] } }));
        await Ok(full.PostAsync($"/api/team/admin/members/{survivor}/roles", new { roleIds = new[] { roles[SystemRoles.Auditor] } }));
        Assert.Single(await api.WithDbAsync(db => db.Memberships.Where(m => m.OrganizationId == orgId && m.Status == MembershipStatus.Active
            && m.Roles.Any(r => r.Role!.Key == SystemRoles.PlatformOwner)).ToListAsync()));
    }

    [Fact]
    public async Task Combining_roles_never_widens_another_permissions_scope()
    {
        // Case manager (documents: assigned) + auditor (view: all, no documents) → sees every request, opens only assigned documents.
        var member = await NewMemberAsync(SystemRoles.CaseManager, SystemRoles.Auditor);
        var (owner, reference) = await SubmittedAsync(api);
        var (_, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", DocForm());
        await Ok(member.Client.GetAsync($"/api/team/market/sale-requests/{reference}"));
        Assert.Equal(HttpStatusCode.NotFound, (await member.Client.GetBytesAsync(TestClient.Str(doc, "url"))).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.PostAsync($"/api/team/market/sale-requests/{reference}/start-review")).Status);
        await AssignAsync(api, $"sale-requests/{reference}", member.Email);
        Assert.Equal(HttpStatusCode.OK, (await member.Client.GetBytesAsync(TestClient.Str(doc, "url"))).Status);

        var lead = await api.LoginAsync(Lead);
        var (_, detail) = await Ok(lead.GetAsync($"/api/team/admin/members/{member.MembershipId}"));
        var effective = detail!["effective"]!.AsArray();
        var view = effective.First(e => TestClient.Str(e, "key") == P.MarketView);
        Assert.Equal("all", TestClient.Str(view, "scope"));
        Assert.Equal(2, view!["from"]!.AsArray().Count);
        Assert.Equal("assigned", TestClient.Str(effective.First(e => TestClient.Str(e, "key") == P.DocumentsRead), "scope"));
    }

    // ── Deactivation with assigned work ──

    [Fact]
    public async Task Removing_a_member_with_open_work_requires_a_reassignment_and_keeps_the_history()
    {
        var member = await NewMemberAsync(SystemRoles.CaseManager);
        var (_, reference) = await SubmittedAsync(api);
        await AssignAsync(api, $"sale-requests/{reference}", member.Email);
        var lead = await api.LoginAsync(Lead);

        var (s, body) = await lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/remove", new { reason = "انتهاء العمل" });
        Assert.Equal(HttpStatusCode.Conflict, s);
        Assert.Equal("has_assigned_work", TestClient.Str(body, "code"));
        var coordinatorId = await UserIdAsync(api, Coordinator);
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/remove", new { reason = "انتهاء العمل", reassign = "member", reassignToUserId = coordinatorId }));

        var (_, file) = await Ok((await api.LoginAsync(Coordinator)).GetAsync($"/api/team/market/sale-requests/{reference}"));
        Assert.Equal(coordinatorId.ToString(), TestClient.Str(file!["assignedTo"], "id"));
        Assert.Contains(file["events"]!.AsArray(), e => TestClient.Str(e, "kind") == "reassigned");
        var m = await api.WithDbAsync(db => db.Memberships.Include(x => x.Roles).FirstAsync(x => x.Id == member.MembershipId));
        Assert.Equal(MembershipStatus.Revoked, m.Status);
        Assert.NotEmpty(m.Roles);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(a => a.Type == "team.member_removed" && a.SubjectReference == member.MembershipId.ToString())));
        Assert.Null(await api.WithDbAsync(db => Modules.Audit.AuditLog.VerifyChainAsync(db, m.OrganizationId)));
    }

    // ── Roles ──

    [Fact]
    public async Task System_roles_are_fixed_custom_roles_are_validated_and_in_use_roles_cannot_be_archived()
    {
        var lead = await api.LoginAsync(Lead);
        var ids = await RoleIdsAsync(lead);
        var (s1, b1) = await lead.PutAsync($"/api/team/admin/roles/{ids[SystemRoles.CaseManager]}", new { nameAr = "تعديل", grants = new[] { new { key = P.MarketView, scope = "all" } } });
        Assert.Equal(HttpStatusCode.Conflict, s1);
        Assert.Equal("system_role", TestClient.Str(b1, "code"));
        var (s2, _) = await lead.PostAsync("/api/team/admin/roles", new { nameAr = "دور مرحلة لاحقة", grants = new[] { new { key = P.OffersManage, scope = "all" } } });
        Assert.Equal(HttpStatusCode.BadRequest, s2);
        var (s3, _) = await lead.PostAsync("/api/team/admin/roles", new { nameAr = "دور مجهول", grants = new[] { new { key = "admin.everything", scope = "all" } } });
        Assert.Equal(HttpStatusCode.BadRequest, s3);

        var (_, created) = await Ok(lead.PostAsync("/api/team/admin/roles", new
        {
            nameAr = $"متابعة فقط {Guid.NewGuid().ToString("N")[..4]}",
            grants = new[] { new { key = P.MarketView, scope = "assigned" }, new { key = P.MarketFollow, scope = "assigned" }, new { key = P.DashboardView, scope = "assigned" } },
        }));
        var roleId = Guid.Parse(TestClient.Str(created, "id"));
        var (_, role) = await Ok(lead.GetAsync($"/api/team/admin/roles/{roleId}"));
        // Non-scopable grants are always "all".
        Assert.Equal("all", TestClient.Str(role!["role"]!["grants"]!.AsArray().First(g => TestClient.Str(g, "key") == P.DashboardView), "scope"));

        var member = await NewMemberAsync(SystemRoles.Auditor);
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { roleId } }));
        var (s4, b4) = await lead.PostAsync($"/api/team/admin/roles/{roleId}/archive", new { reason = "لم يعد مطلوبًا" });
        Assert.Equal(HttpStatusCode.Conflict, s4);
        Assert.Equal("role_in_use", TestClient.Str(b4, "code"));
        // Editing with a stale version is refused.
        var (s5, _) = await lead.PutAsync($"/api/team/admin/roles/{roleId}", new { nameAr = "اسم جديد", version = 1u, grants = new[] { new { key = P.MarketView, scope = "all" } } });
        Assert.Equal(HttpStatusCode.Conflict, s5);
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { ids[SystemRoles.Auditor] } }));
        await Ok(lead.PostAsync($"/api/team/admin/roles/{roleId}/archive", new { reason = "لم يعد مطلوبًا" }));
        var (s6, _) = await lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { roleId } });
        Assert.Equal(HttpStatusCode.BadRequest, s6);
    }

    // ── 7. Repeatable seed/sync and the pre-1.5 role keys ──

    [Fact]
    public async Task Role_sync_is_repeatable_and_renames_the_old_role_keys_in_place()
    {
        var (orgId, membershipId) = await api.WithDbAsync(async db =>
        {
            var org = new Organization { ShortCode = "legacy-" + Guid.NewGuid().ToString("N")[..8], NameAr = "فريق قديم", Initials = "فق", Kind = OrganizationKind.Operator };
            db.Organizations.Add(org);
            var lead = new Role { OrganizationId = org.Id, Key = "team_lead", NameAr = "قائد الفريق", NameEn = "Team lead" };
            lead.Permissions.Add(new RolePermission { RoleId = lead.Id, PermissionKey = P.MarketView });
            lead.Permissions.Add(new RolePermission { RoleId = lead.Id, PermissionKey = "legacy.removed_permission" });
            db.Roles.Add(lead);
            var u = new User { Email = $"legacy.{Guid.NewGuid().ToString("N")[..6]}@team.rahoon.example", FullName = "عضو قديم" };
            db.Users.Add(u);
            var m = new Membership { OrganizationId = org.Id, UserId = u.Id };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = lead.Id });
            db.Memberships.Add(m);
            await db.SaveChangesAsync();
            await SystemRoleSync.SyncAsync(db);
            await SystemRoleSync.SyncAsync(db);
            return (org.Id, m.Id);
        });
        var roles = await api.WithDbAsync(db => db.Roles.Include(r => r.Permissions).Where(r => r.OrganizationId == orgId).ToListAsync());
        Assert.Equal(SystemRoles.Templates.Select(t => t.Key).Order(), roles.Select(r => r.Key).Order());
        Assert.All(roles, r => Assert.True(r.IsSystem));
        var owner = roles.Single(r => r.Key == SystemRoles.PlatformOwner);
        Assert.Equal(P.GrantableKeys.Order(), owner.Permissions.Select(p => p.PermissionKey).Order());
        // The member who was team lead is now the platform owner (same role row).
        var held = await api.WithDbAsync(db => db.MembershipRoles.Where(x => x.MembershipId == membershipId).Select(x => x.RoleId).ToListAsync());
        Assert.Equal([owner.Id], held);
        var cm = roles.Single(r => r.Key == SystemRoles.CaseManager);
        Assert.Equal(GrantScope.Assigned, cm.Permissions.Single(p => p.PermissionKey == P.DocumentsRead).Scope);
    }

    [Fact]
    public async Task Each_member_lands_on_an_area_they_can_open_and_navigation_matches_grants()
    {
        var (_, auditorMe) = await (await api.LoginAsync(Auditor)).GetAsync("/api/auth/me");
        Assert.Equal("/team", TestClient.Str(auditorMe, "home"));
        var lead = await api.LoginAsync(Lead);
        var (_, created) = await Ok(lead.PostAsync("/api/team/admin/roles", new
        {
            nameAr = $"مدقق سجل {Guid.NewGuid().ToString("N")[..4]}", grants = new[] { new { key = P.AuditRead, scope = "all" } },
        }));
        var ids = await RoleIdsAsync(lead);
        var member = await NewMemberAsync(SystemRoles.Auditor);
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { Guid.Parse(TestClient.Str(created, "id")) } }));
        var (_, me) = await member.Client.GetAsync("/api/auth/me");
        Assert.Equal("/team/audit", TestClient.Str(me, "home"));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/team/market/overview")).Status);
        var (_, audit) = await Ok(member.Client.GetAsync("/api/team/admin/audit?group=team"));
        Assert.Contains(audit!["items"]!.AsArray(), e => TestClient.Str(e, "type") == "team.roles_changed");

        // Dashboard + team administration but no request access: the overview (which reads request queues) isn't
        // offered; the member lands on the team page, and the API enforces dashboard.view on the overview itself.
        var (_, adminRole) = await Ok(lead.PostAsync("/api/team/admin/roles", new
        {
            nameAr = $"إدارة الفريق فقط {Guid.NewGuid().ToString("N")[..4]}",
            grants = new[] { new { key = P.DashboardView, scope = "all" }, new { key = P.TeamRead, scope = "all" } },
        }));
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { Guid.Parse(TestClient.Str(adminRole, "id")) } }));
        var (_, me2) = await member.Client.GetAsync("/api/auth/me");
        Assert.Equal("/team/members", TestClient.Str(me2, "home"));
        await Ok(member.Client.GetAsync("/api/team/admin/members"));
        var (_, noDash) = await Ok(lead.PostAsync("/api/team/admin/roles", new
        {
            nameAr = $"طلبات بلا لوحة {Guid.NewGuid().ToString("N")[..4]}", grants = new[] { new { key = P.MarketView, scope = "all" } },
        }));
        await Ok(lead.PostAsync($"/api/team/admin/members/{member.MembershipId}/roles", new { roleIds = new[] { Guid.Parse(TestClient.Str(noDash, "id")) } }));
        Assert.Equal(HttpStatusCode.Forbidden, (await member.Client.GetAsync("/api/team/market/overview")).Status);
        var (_, me3) = await member.Client.GetAsync("/api/auth/me");
        Assert.Equal("/team/sale", TestClient.Str(me3, "home"));
        _ = ids;
    }

    [Fact]
    public async Task Support_sees_contact_messages_but_not_unassigned_requests_or_documents()
    {
        var support = await api.LoginAsync(Support);
        await Ok(support.GetAsync("/api/team/market/contact-messages"));
        var (_, list) = await Ok(support.GetAsync("/api/team/market/sale-requests"));
        Assert.Empty(list!["items"]!.AsArray());
        Assert.Equal(0, list["total"]!.GetValue<int>());
        // A case manager follows only assigned work, so the visitor inbox (no case) is not theirs.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.LoginAsync(Coordinator)).GetAsync("/api/team/market/contact-messages")).Status);
    }
}
