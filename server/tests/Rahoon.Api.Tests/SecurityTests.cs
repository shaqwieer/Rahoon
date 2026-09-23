using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SecurityTests(ApiFixture api)
{
    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var c = api.Client();
        var (status, _) = await c.GetAsync("/api/cases?view=all");
        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Lender_cannot_read_or_list_another_tenants_case()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (status, body) = await sara.GetAsync("/api/cases/RH-2026-005101");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("case_forbidden", TestClient.Str(body, "code"));

        var (_, list) = await sara.GetAsync("/api/cases?view=all&q=RH-2026-0051&pageSize=50");
        Assert.DoesNotContain(list!["items"]!.AsArray(), i => TestClient.Str(i, "reference").StartsWith("RH-2026-0051"));
    }

    [Fact]
    public async Task Unknown_and_foreign_cases_get_the_same_refusal()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var foreign = await sara.GetAsync("/api/cases/RH-2026-005101");
        var unknown = await sara.GetAsync("/api/cases/RH-2099-000001");
        Assert.Equal(foreign.Status, unknown.Status);
        Assert.Equal(TestClient.Str(foreign.Body, "title"), TestClient.Str(unknown.Body, "title"));
    }

    [Fact]
    public async Task Switching_organization_changes_the_visible_tenant()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "شركة السنبلة للتمويل");
        // In Sunbula she is a credit analyst: sees her tenant only, never Alufuq's cases.
        var (status, _) = await sara.GetAsync("/api/cases/RH-2026-004172");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Other_tenant_user_cannot_reach_alufuq_data()
    {
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        var (status, _) = await maha.GetAsync("/api/cases/RH-2026-004172");
        Assert.Equal(HttpStatusCode.Forbidden, status);
        var (s2, list) = await maha.GetAsync("/api/cases?view=all&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.All(list!["items"]!.AsArray(), i => Assert.StartsWith("RH-2026-0051", TestClient.Str(i, "reference")));
    }

    [Fact]
    public async Task Provider_and_platform_users_cannot_use_lender_endpoints()
    {
        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync("/api/cases?view=all")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync("/api/portfolio")).Status);

        var rana = await api.LoginAsync("r.alsubaie@rahoon.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await rana.GetAsync("/api/cases/RH-2026-004172")).Status);
    }

    [Fact]
    public async Task Role_without_permission_is_refused_server_side()
    {
        var khaled = await api.LoginAsync("k.alzahrani@alufuq.example"); // case officer: no case.create
        var (status, _) = await khaled.PostAsync("/api/cases/drafts");
        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task Mutations_require_csrf_token_and_allowed_origin()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var noCsrf = await sara.SendAsync(HttpMethod.Post, "/api/cases/drafts", new { }, includeCsrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, noCsrf.Status);
        Assert.Equal("csrf", TestClient.Str(noCsrf.Body, "code"));

        var evil = await sara.SendAsync(HttpMethod.Post, "/api/cases/drafts", new { }, origin: "https://evil.example");
        Assert.Equal(HttpStatusCode.Forbidden, evil.Status);
        Assert.Equal("origin", TestClient.Str(evil.Body, "code"));
    }

    [Fact]
    public async Task Failed_logins_lock_the_account_after_five_attempts()
    {
        var c = api.Client();
        for (var i = 0; i < 4; i++)
        {
            var (s, b) = await c.PostAsync("/api/auth/login", new { email = "m.alqarni@alufuq.example", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, s);
            Assert.Equal(4 - i, b!["remainingAttempts"]!.GetValue<int>());
        }
        var (locked, _) = await c.PostAsync("/api/auth/login", new { email = "m.alqarni@alufuq.example", password = "wrong-password" });
        Assert.Equal((HttpStatusCode)423, locked);
        // Even the right password is refused while locked.
        var (still, _) = await c.PostAsync("/api/auth/login", new { email = "m.alqarni@alufuq.example", password = ApiFixture.Password });
        Assert.Equal((HttpStatusCode)423, still);
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_response_shape()
    {
        var c = api.Client();
        var (s, b) = await c.PostAsync("/api/auth/login", new { email = "nobody@alufuq.example", password = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, s);
        Assert.Equal("invalid_credentials", TestClient.Str(b, "code"));
    }

    [Fact]
    public async Task Pii_is_masked_and_reveal_is_audited()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (_, ws) = await sara.GetAsync("/api/cases/RH-2026-004172");
        var primary = ws!["parties"]!["primary"]!;
        Assert.Equal("1•••••••42", TestClient.Str(primary, "nationalIdMasked"));

        var partyId = TestClient.Str(primary, "id");
        var (noReason, _) = await sara.PostAsync($"/api/cases/RH-2026-004172/parties/{partyId}/reveal", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason);

        var (ok, revealed) = await sara.PostAsync($"/api/cases/RH-2026-004172/parties/{partyId}/reveal", new { reason = "مطابقة مع الصك" });
        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.Equal("1098734542", TestClient.Str(revealed, "nationalId"));
        var logged = await api.WithDbAsync(db => db.AuditEvents.CountAsync(e => e.Type == "pii.reveal" && e.Reason == "مطابقة مع الصك"));
        Assert.True(logged >= 1);
    }

    [Fact]
    public async Task Database_rejects_audit_tampering()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => api.WithDbAsync(db =>
            db.Database.ExecuteSqlRawAsync("UPDATE audit.audit_events SET title = 'x' WHERE seq = (SELECT min(seq) FROM audit.audit_events)")));
        Assert.Contains("append-only", ex.Message + ex.InnerException?.Message);
    }
}
