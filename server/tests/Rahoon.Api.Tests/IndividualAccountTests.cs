using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A step 4 — self-registered individuals (ADR 0001 §4.1): registration and sign-in by national ID/iqama +
/// mobile code, terms evidence, anti-enumeration, lockout, and isolation from staff and owner data.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class IndividualAccountTests(ApiFixture api)
{
    private static readonly Random Rng = new();
    private static string NewId(char first = '1') => first + string.Concat(Enumerable.Range(0, 9).Select(_ => Rng.Next(10)));
    private static string NewPhone() => "05" + string.Concat(Enumerable.Range(0, 8).Select(_ => Rng.Next(10)));

    private static async Task<(HttpStatusCode Status, System.Text.Json.Nodes.JsonNode? Body)> StartAsync(TestClient c, string id, string phone) =>
        await c.PostAsync("/api/auth/individual/start", new { nationalId = id, phone });

    private async Task<TestClient> RegisterAsync(string id, string phone)
    {
        var c = api.Client();
        var (s, start) = await StartAsync(c, id, phone);
        Assert.Equal(HttpStatusCode.OK, s);
        var (v, body) = await c.PostAsync("/api/auth/individual/verify", new { code = TestClient.Str(start, "sandboxCode"), acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.OK, v);
        Assert.Equal("/my", TestClient.Str(body, "next"));
        return c;
    }

    [Fact]
    public async Task New_individual_registers_and_gets_a_session_not_bound_to_any_case()
    {
        var id = NewId('2');
        var phone = NewPhone();
        var c = await RegisterAsync(id, phone);

        var (_, me) = await c.GetAsync("/api/auth/me");
        Assert.Equal("individual", TestClient.Str(me, "scope"));
        Assert.Equal("/my", TestClient.Str(me, "home"));
        Assert.Null(me!["owner"]);
        Assert.Null(me["organization"]);
        Assert.Empty(me["memberships"]!.AsArray());
        Assert.Equal("", TestClient.Str(me["user"], "email")); // placeholder e-mail never exposed
        Assert.Equal($"{id[0]}•••••••{id[^2..]}", TestClient.Str(me["individual"], "idMasked"));
        Assert.Equal("self_declared", TestClient.Str(me["individual"], "identityAssurance"));

        var (sa, account) = await c.GetAsync("/api/individual/account");
        Assert.Equal(HttpStatusCode.OK, sa);
        Assert.Equal("resident", TestClient.Str(account, "idType"));
        Assert.Equal(IndividualAuthEndpoints.TermsVersion, TestClient.Str(account, "termsVersion"));
        Assert.Equal("unavailable", TestClient.Str(account, "nationalIdProvider"));

        await api.WithDbAsync(async db =>
        {
            var profile = await db.IndividualProfiles.IgnoreQueryFilters().Include(p => p.User).SingleAsync(p => p.NationalIdMasked == TestClient.Str(account, "idMasked") && p.PhoneMasked == TestClient.Str(account, "phoneMasked"));
            Assert.NotEqual(id, profile.NationalIdEnc);          // encrypted at rest
            Assert.DoesNotContain(id, profile.User!.Email);
            Assert.Equal(AccountKind.Individual, profile.User.AccountKind);
            Assert.Equal(UserStatus.Active, profile.User.Status);
            Assert.True(await db.TermsAcceptances.AnyAsync(t => t.UserId == profile.UserId && t.Version == IndividualAuthEndpoints.TermsVersion));
            Assert.True(await db.AuditEvents.AnyAsync(e => e.Type == "individual.registered" && e.ActorUserId == profile.UserId && e.ActorType == "individual"));
            return 0;
        });
    }

    [Fact]
    public async Task Terms_are_required_and_a_missing_tick_does_not_cost_an_attempt()
    {
        var c = api.Client();
        var (_, start) = await StartAsync(c, NewId(), NewPhone());
        var code = TestClient.Str(start, "sandboxCode");
        for (var i = 0; i < 4; i++)
        {
            var (s, body) = await c.PostAsync("/api/auth/individual/verify", new { code, acceptTerms = false, awarenessOptIn = false });
            Assert.Equal(HttpStatusCode.BadRequest, s);
            Assert.NotNull(body!["errors"]!["acceptTerms"]);
        }
        var (ok, _) = await c.PostAsync("/api/auth/individual/verify", new { code, acceptTerms = true, awarenessOptIn = true });
        Assert.Equal(HttpStatusCode.OK, ok);
    }

    [Fact]
    public async Task Invalid_id_or_mobile_is_a_field_error()
    {
        var c = api.Client();
        var (s, body) = await StartAsync(c, "3123456789", "0412345678");
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.NotNull(body!["errors"]!["nationalId"]);
        Assert.NotNull(body["errors"]!["phone"]);
    }

    [Fact]
    public async Task Registered_individual_signs_in_again_with_the_same_mobile()
    {
        var id = NewId();
        var phone = NewPhone();
        var first = await RegisterAsync(id, phone);
        var (_, me1) = await first.GetAsync("/api/auth/me");

        var again = api.Client();
        var (_, start) = await StartAsync(again, id, phone);
        var (s, body) = await again.PostAsync("/api/auth/individual/verify", new { code = TestClient.Str(start, "sandboxCode"), acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.False(body!["firstTime"]!.GetValue<bool>());
        var (_, me2) = await again.GetAsync("/api/auth/me");
        Assert.Equal(TestClient.Str(me1!["user"], "id"), TestClient.Str(me2!["user"], "id")); // one account per ID
    }

    [Fact]
    public async Task Registered_id_with_another_mobile_gets_the_same_answer_but_no_code_and_no_lock()
    {
        var id = NewId();
        var phone = NewPhone();
        await RegisterAsync(id, phone);

        var attacker = api.Client();
        var otherPhone = NewPhone();
        var (s, start) = await StartAsync(attacker, id, otherPhone);
        Assert.Equal(HttpStatusCode.OK, s);                                   // same status as a fresh registration
        Assert.Equal("+966 5• ••• ••" + otherPhone[^2..], TestClient.Str(start, "destination")); // echoes the typed number only
        Assert.Null(start!["sandboxCode"]?.GetValue<string?>());              // nothing was sent to anyone

        (HttpStatusCode, System.Text.Json.Nodes.JsonNode?) last = default;
        for (var i = 0; i < 3; i++)
            last = await attacker.PostAsync("/api/auth/individual/verify", new { code = "000000", acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.Locked, last.Item1);

        // The real owner of the account is not locked out by someone else's attempts.
        var owner = api.Client();
        var (_, again) = await StartAsync(owner, id, phone);
        Assert.False(string.IsNullOrEmpty(TestClient.Str(again, "sandboxCode")));
        var (ok, _) = await owner.PostAsync("/api/auth/individual/verify", new { code = TestClient.Str(again, "sandboxCode"), acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.OK, ok);
    }

    [Fact]
    public async Task Wrong_codes_on_the_real_mobile_lock_the_account_temporarily()
    {
        var id = NewId();
        var phone = NewPhone();
        await RegisterAsync(id, phone);

        var c = api.Client();
        await StartAsync(c, id, phone);
        var (s1, b1) = await c.PostAsync("/api/auth/individual/verify", new { code = "000000", acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.Unauthorized, s1);
        Assert.Equal("otp_invalid", TestClient.Str(b1, "code"));
        await c.PostAsync("/api/auth/individual/verify", new { code = "000000", acceptTerms = true, awarenessOptIn = false });
        var (s3, _) = await c.PostAsync("/api/auth/individual/verify", new { code = "000000", acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.Locked, s3);

        var (locked, body) = await StartAsync(api.Client(), id, phone);
        Assert.Equal(HttpStatusCode.Locked, locked);
        Assert.Equal("locked", TestClient.Str(body, "code"));
    }

    [Fact]
    public async Task Individual_session_reaches_no_staff_owner_or_tenant_data()
    {
        var c = await RegisterAsync(NewId(), NewPhone());
        foreach (var path in new[] { "/api/cases", "/api/portfolio", "/api/approvals", "/api/complaints", "/api/owner/home", "/api/search?q=RH", "/api/tasks", "/api/cases/RH-2026-004172" })
        {
            var (s, _) = await c.GetAsync(path);
            Assert.True(s is HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"{path} returned {s}");
        }
        var (n, notes) = await c.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.OK, n);                      // user-scoped: only the individual's own (none)
        Assert.DoesNotContain("RH-2026", notes!.ToJsonString());
    }

    [Fact]
    public async Task Staff_owner_and_anonymous_callers_cannot_use_the_individual_account_endpoint()
    {
        var (anon, _) = await api.Client().GetAsync("/api/individual/account");
        Assert.Equal(HttpStatusCode.Unauthorized, anon);
        var staff = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (s, _) = await staff.GetAsync("/api/individual/account");
        Assert.Equal(HttpStatusCode.Forbidden, s);
    }

    [Fact]
    public async Task Session_is_an_http_only_cookie_and_no_token_is_returned_in_the_body()
    {
        using var http = api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        using var msg = new HttpRequestMessage(HttpMethod.Post, "/api/auth/individual/start") { Content = JsonContent.Create(new { nationalId = NewId(), phone = NewPhone() }) };
        msg.Headers.Add("Origin", TestClient.Origin);
        using var res = await http.SendAsync(msg);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var cookies = res.Headers.GetValues("Set-Cookie").ToList();
        var sid = Assert.Single(cookies, c => c.StartsWith("rahoon_sid="));
        Assert.Contains("httponly", sid, StringComparison.OrdinalIgnoreCase);
        var body = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain(sid.Split(';')[0].Split('=')[1], body);
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
    }
}
