using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// `Auth:SmsConfirmation` (default false): with it off, no SMS is sent and the issued code comes back with
/// `otpRequired=false` so the web confirms it without a code step; with it on, the code must be typed.
/// A decoy challenge (anti-enumeration) never returns its code in either mode.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SmsConfirmationTests(ApiFixture api)
{
    private static readonly Random Rng = new();
    private static string NewId() => "1" + string.Concat(Enumerable.Range(0, 9).Select(_ => Rng.Next(10)));
    private static string NewPhone() => "05" + string.Concat(Enumerable.Range(0, 8).Select(_ => Rng.Next(10)));

    private WebApplicationFactory<Program> WithoutSms() => api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
        cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SmsConfirmation"] = "false",
            ["Auth:ExposeSandboxOtp"] = "false", // the code must come from the flag, not from the sandbox echo
        })));

    private static TestClient ClientOf(WebApplicationFactory<Program> f) =>
        new(f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private Task<int> SmsCountAsync(string phone) => api.WithDbAsync(db => db.OutboundMessages.CountAsync(m => m.Destination == phone));

    [Fact]
    public async Task Off_returns_the_code_for_automatic_confirmation_and_sends_nothing()
    {
        await using var factory = WithoutSms();
        var c = ClientOf(factory);
        var phone = NewPhone();

        var (s, start) = await c.PostAsync("/api/auth/individual/start", new { nationalId = NewId(), phone });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.False(start!["otpRequired"]!.GetValue<bool>());
        var code = TestClient.Str(start, "sandboxCode");
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(0, await SmsCountAsync(phone));

        var (v, body) = await c.PostAsync("/api/auth/individual/verify", new { code, acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.OK, v);
        Assert.Equal("/my", TestClient.Str(body, "next"));
    }

    [Fact]
    public async Task Off_still_hides_a_decoy_code()
    {
        await using var factory = WithoutSms();
        var id = NewId();
        var owner = ClientOf(factory);
        var (_, first) = await owner.PostAsync("/api/auth/individual/start", new { nationalId = id, phone = NewPhone() });
        await owner.PostAsync("/api/auth/individual/verify", new { code = TestClient.Str(first, "sandboxCode"), acceptTerms = true, awarenessOptIn = false });

        var other = ClientOf(factory);
        var (s, start) = await other.PostAsync("/api/auth/individual/start", new { nationalId = id, phone = NewPhone() });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(start!["otpRequired"]!.GetValue<bool>());
        Assert.Null(start["sandboxCode"]?.GetValue<string?>());
    }

    [Fact]
    public async Task On_requires_the_code_and_sends_it()
    {
        var c = api.Client(); // the shared fixture runs with SmsConfirmation=true
        var phone = NewPhone();
        var (s, start) = await c.PostAsync("/api/auth/individual/start", new { nationalId = NewId(), phone });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(start!["otpRequired"]!.GetValue<bool>());
        Assert.Equal(1, await SmsCountAsync(phone));
    }
}
