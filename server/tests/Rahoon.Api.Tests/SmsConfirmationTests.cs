using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// `Auth:SmsConfirmation` (default false): with it off, no SMS is sent and the issued code comes back with
/// `otpRequired=false` so the web confirms it without a code step; with it on, the code must be typed and is sent.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SmsConfirmationTests(ApiFixture api)
{
    private WebApplicationFactory<Program> WithoutSms() => api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
        cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:SmsConfirmation"] = "false",
            ["Auth:ExposeSandboxOtp"] = "false", // the code must come from the flag, not from the sandbox echo
        })));

    private static TestClient ClientOf(WebApplicationFactory<Program> f) =>
        new(f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    private Task<int> SmsCountAsync(string phone) =>
        api.WithDbAsync(db => db.OutboundSms.CountAsync(m => m.Destination == Rahoon.Api.Infrastructure.Security.Mask.Phone(phone)));

    [Fact]
    public async Task Off_returns_the_code_for_automatic_confirmation_and_sends_nothing()
    {
        await using var factory = WithoutSms();
        var c = ClientOf(factory);
        var phone = NewPhone();

        var (s, start) = await c.PostAsync("/api/auth/phone/start", new { phone });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.False(start!["otpRequired"]!.GetValue<bool>());
        var code = TestClient.Str(start, "sandboxCode");
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(0, await SmsCountAsync(phone));

        var (v, body) = await c.PostAsync("/api/auth/phone/verify", new { code, acceptTerms = true });
        Assert.Equal(HttpStatusCode.OK, v);
        Assert.Equal("/account", TestClient.Str(body, "next"));
        Assert.False(body!["smsVerified"]!.GetValue<bool>());
    }

    [Fact]
    public async Task On_requires_the_code_and_sends_it()
    {
        var c = api.Client(); // the shared fixture runs with SmsConfirmation=true
        var phone = NewPhone();
        var (s, start) = await c.PostAsync("/api/auth/phone/start", new { phone });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(start!["otpRequired"]!.GetValue<bool>());
        Assert.Equal(1, await SmsCountAsync(phone));
        var masked = Rahoon.Api.Infrastructure.Security.Mask.Phone(phone);
        var sms = await api.WithDbAsync(db => db.OutboundSms.SingleAsync(m => m.Destination == masked));
        Assert.Equal("simulated", sms.Result);
        Assert.DoesNotMatch(@"\d{6}", sms.Body); // the code is never stored readable
        Assert.DoesNotContain(phone, sms.Destination);
    }
}
