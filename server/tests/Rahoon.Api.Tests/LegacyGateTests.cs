using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// The mortgage-default help model was withdrawn on 2026-10-01 (docs/redefinition/legacy-inventory.md). With
/// Features:LegacyMortgage off (the default) its routes are not mapped and only «فريق رهون» staff can sign in. The rest of
/// the suite runs with the flag on as the regression for the archived code.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class LegacyGateTests(ApiFixture api)
{
    private WebApplicationFactory<Program> CurrentModel() => api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
        cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Features:LegacyMortgage"] = "false" })));

    private static TestClient ClientOf(WebApplicationFactory<Program> f) =>
        new(f.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));

    [Theory]
    [InlineData("/api/cases")]
    [InlineData("/api/portfolio")]
    [InlineData("/api/my/requests")]
    [InlineData("/api/team/requests")]
    [InlineData("/api/institutions")]
    [InlineData("/api/public/invitations/demo-RH-2026-004172")]
    public async Task Old_model_routes_answer_404(string path)
    {
        // Mapped with the flag on (401/200), gone with it off.
        var (legacyStatus, _) = await api.Client().GetAsync(path);
        Assert.NotEqual(HttpStatusCode.NotFound, legacyStatus);

        await using var f = CurrentModel();
        var (status, _) = await ClientOf(f).GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task Old_individual_sign_in_by_national_id_is_gone()
    {
        await using var f = CurrentModel();
        var (status, _) = await ClientOf(f).PostAsync("/api/auth/individual/start", new { nationalId = "1087654321", phone = "0551110001" });
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Theory]
    [InlineData("s.alqahtani@alufuq.example")]   // lender
    [InlineData("o.alanazi@valuer-b.example")]   // provider
    [InlineData("y.alhamdan@agent-j.example")]   // judicial agent
    [InlineData("a.almutairi@rahoon.example")]   // platform admin
    public async Task Withdrawn_workspaces_cannot_sign_in(string email)
    {
        await using var f = CurrentModel();
        var (status, body) = await ClientOf(f).PostAsync("/api/auth/login", new { email, password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal("workspace_withdrawn", TestClient.Str(body, "code"));
    }

    [Fact]
    public async Task Rahoon_team_still_signs_in_and_lands_on_team()
    {
        await using var f = CurrentModel();
        var c = ClientOf(f);
        var (s1, login) = await c.PostAsync("/api/auth/login", new { email = "l.alharbi@team.rahoon.example", password = ApiFixture.Password });
        Assert.Equal(HttpStatusCode.OK, s1);
        var (s2, verify) = await c.PostAsync("/api/auth/mfa/verify", new { code = TestClient.Str(login, "sandboxCode") });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("/team", TestClient.Str(verify, "next"));
    }
}
