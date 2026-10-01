using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>Sessions, CSRF/Origin, lockout, server-side permissions and the append-only audit log.</summary>
[Collection(ApiCollection.Name)]
public sealed class SecurityTests(ApiFixture api)
{
    [Fact]
    public async Task Anonymous_requests_to_private_areas_are_rejected()
    {
        var c = api.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/team/market/overview")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/team/directory")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/account")).Status);
    }

    [Fact]
    public async Task Team_endpoints_check_permissions_on_the_server()
    {
        // A case manager works on requests and reads the directory, but does not manage it.
        var coordinator = await api.LoginAsync(Coordinator);
        Assert.Equal(HttpStatusCode.OK, (await coordinator.GetAsync("/api/team/market/overview")).Status);
        Assert.Equal(HttpStatusCode.OK, (await coordinator.GetAsync("/api/team/directory")).Status);
        var (s, _) = await coordinator.PostAsync("/api/team/directory", new { nameAr = "جهة", types = new[] { "bank" } });
        Assert.Equal(HttpStatusCode.Forbidden, s);
        // A document reviewer has no directory grant at all.
        Assert.Equal(HttpStatusCode.Forbidden, (await (await api.LoginAsync(Reviewer)).GetAsync("/api/team/directory")).Status);
    }

    [Fact]
    public async Task Owners_and_buyers_never_reach_team_endpoints()
    {
        var person = await SellerAsync(api);
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/team/market/sale-requests")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync("/api/team/directory")).Status);
    }

    [Fact]
    public async Task Mutations_require_csrf_token_and_allowed_origin()
    {
        var lead = await api.LoginAsync(Lead);
        var noCsrf = await lead.SendAsync(HttpMethod.Post, "/api/team/directory", new { nameAr = "جهة", types = new[] { "bank" } }, includeCsrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, noCsrf.Status);
        Assert.Equal("csrf", TestClient.Str(noCsrf.Body, "code"));

        var evil = await lead.SendAsync(HttpMethod.Post, "/api/team/directory", new { nameAr = "جهة", types = new[] { "bank" } }, origin: "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, evil.Status);
        Assert.Equal("origin", TestClient.Str(evil.Body, "code"));
    }

    [Fact]
    public async Task Failed_logins_lock_the_account_after_five_attempts()
    {
        var c = api.Client();
        // The demo finance officer: no other test signs in as this member (the lockout lasts 15 minutes).
        const string email = "m.alghamdi@team.rahoon.example";
        for (var i = 0; i < 4; i++)
        {
            var (s, b) = await c.PostAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, s);
            Assert.Equal(4 - i, b!["remainingAttempts"]!.GetValue<int>());
        }
        var (locked, _) = await c.PostAsync("/api/auth/login", new { email, password = "wrong-password" });
        Assert.Equal((HttpStatusCode)423, locked);
        // Even the right password is refused while locked.
        var (still, _) = await c.PostAsync("/api/auth/login", new { email, password = ApiFixture.Password });
        Assert.Equal((HttpStatusCode)423, still);
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_response_shape()
    {
        var (s, b) = await api.Client().PostAsync("/api/auth/login", new { email = "nobody@team.rahoon.example", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, s);
        Assert.Equal("invalid_credentials", TestClient.Str(b, "code"));
    }

    [Fact]
    public async Task Database_rejects_audit_tampering()
    {
        // Make sure there is at least one event (a sign-in records one).
        await api.LoginAsync(Verifier);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => api.WithDbAsync(db =>
            db.Database.ExecuteSqlRawAsync("UPDATE audit.audit_events SET title = 'x' WHERE seq = (SELECT min(seq) FROM audit.audit_events)")));
        Assert.Contains("append-only", ex.Message + ex.InnerException?.Message);
    }

    [Fact]
    public async Task Audit_chain_verifies()
    {
        await api.LoginAsync(Lead);
        var orgs = await api.WithDbAsync(db => db.AuditEvents.Select(e => e.OrganizationId).Distinct().ToListAsync());
        foreach (var org in orgs)
        {
            var broken = await api.WithDbAsync(db => Modules.Audit.AuditLog.VerifyChainAsync(db, org));
            if (broken is null) continue;
            var around = await api.WithDbAsync(db => db.AuditEvents.Where(e => e.OrganizationId == org && e.Seq >= broken - 2 && e.Seq <= broken)
                .OrderBy(e => e.Seq).ToListAsync());
            Assert.Fail($"chain {org} broken at {broken}: " +
                string.Join(" | ", around.Select(e => $"{e.Seq} {e.Type} prev={e.PrevHash} hash={e.Hash} actor={e.ActorType} at={e.OccurredAt:O}")));
        }
    }
}
