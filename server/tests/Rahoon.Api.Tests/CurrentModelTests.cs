using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// The default configuration (Features:LegacyMortgage off) on a fresh database: the core seed creates only «فريق رهون», the
/// role sync grants market.* permissions, the market seed runs, the old routes are gone, and the whole journey works.
/// Also the privacy rules that depend on files and location: photo metadata and the owner's location choice.
/// </summary>
[Collection(CurrentModelCollection.Name)]
public sealed class CurrentModelTests(CurrentModelFixture api)
{
    [Fact]
    public async Task Fresh_database_has_only_the_rahoon_team_and_the_market_demo_data()
    {
        var kinds = await api.WithDbAsync(db => db.Organizations.Select(o => o.Kind).ToListAsync());
        Assert.Equal([OrganizationKind.Operator], kinds.Distinct().ToList());
        Assert.Equal(0, await api.WithDbAsync(db => db.Cases.IgnoreQueryFilters().CountAsync()));
        Assert.True(await api.WithDbAsync(db => db.Opportunities.CountAsync()) >= 5);

        var leadPerms = await api.WithDbAsync(db => db.Roles.Include(r => r.Permissions)
            .Where(r => r.Key == SystemRoles.TeamLead).SelectMany(r => r.Permissions.Select(p => p.PermissionKey)).ToListAsync());
        Assert.Contains(P.MarketPublish, leadPerms);
    }

    [Fact]
    public async Task Team_signs_in_catalog_and_search_work_and_old_routes_are_gone()
    {
        var team = await api.LoginAsync(Lead);
        var (s1, overview) = await team.GetAsync("/api/team/market/overview");
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.NotNull(overview!["myTasks"]);
        var (_, catalog) = await api.Client().GetAsync("/api/market/catalog");
        Assert.True(catalog!["parties"]!.AsArray().Count >= 4);
        var (_, search) = await api.Client().GetAsync("/api/market/opportunities");
        Assert.True(search!["total"]!.GetValue<int>() >= 3);
        var (s2, _) = await api.Client().GetAsync("/api/cases");
        Assert.Equal(HttpStatusCode.NotFound, s2);
    }

    [Fact]
    public async Task Full_journey_runs_under_the_default_configuration()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);
        var buyer = await SellerAsync(api, name: "مشتري افتراضي");
        var (_, interest) = await Ok(buyer.PostAsync($"/api/market/opportunities/{op}/interest", new { message = "مهتم" }));
        Assert.True(interest!["created"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Published_photos_carry_no_embedded_metadata()
    {
        var (owner, reference) = await SubmittedAsync(api);
        // A JPEG with an Exif APP1 segment holding a GPS-like marker, then a scan segment.
        var exif = Encoding.ASCII.GetBytes("Exif\0\0GPS-SECRET-24.835,46.655");
        var app1Len = exif.Length + 2;
        var jpeg = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE1, (byte)(app1Len >> 8), (byte)app1Len };
        jpeg.AddRange(exif);
        jpeg.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00, 0x12, 0x34, 0xFF, 0xD9]);
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(jpeg.ToArray());
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "phone.jpg");
        var (s, _) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/photos", form);
        Assert.Equal(HttpStatusCode.OK, s);

        var op = await PublishedAsync(api, owner, reference);
        var (_, detail) = await api.Client().GetAsync($"/api/market/opportunities/{op}");
        var photos = detail!["photos"]!.AsArray();
        Assert.Equal(2, photos.Count);
        foreach (var p in photos)
        {
            var (status, bytes, _) = await api.Client().GetBytesAsync(p!["url"]!.GetValue<string>());
            Assert.Equal(HttpStatusCode.OK, status);
            var text = Encoding.ASCII.GetString(bytes);
            Assert.DoesNotContain("Exif", text);
            Assert.DoesNotContain("GPS-SECRET", text);
        }
    }

    [Fact]
    public async Task Exact_location_is_never_shown_without_the_owners_explicit_choice()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await UploadPhotoAsync(owner, reference);
        var team = await ApprovedAsync(api, reference);
        // The owner's wish was set to approximate in the draft; clear it as an API client could leave it unset.
        await api.WithDbAsync(async db =>
        {
            var r = await db.SaleRequests.FirstAsync(x => x.Reference == reference);
            r.LocationDisplayWish = null;
            await db.SaveChangesAsync();
            return 0;
        });
        var (_, created) = await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/opportunity"));
        var op = TestClient.Str(created, "reference");
        var (s, body) = await team.PutAsync($"/api/team/market/opportunities/{op}/content", new { title = "شقة للاختبار في النرجس", locationPrecision = "exact" });
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.True(body!["errors"]!.AsObject().ContainsKey("locationPrecision"));
        var (_, view) = await team.GetAsync($"/api/team/market/opportunities/{op}");
        Assert.Equal("approximate", TestClient.Str(view!["location"], "precision"));
    }
}
