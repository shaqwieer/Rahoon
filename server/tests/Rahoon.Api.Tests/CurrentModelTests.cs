using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// The product on a fresh database migrated through the full history: only the current model's schemas and tables exist,
/// the seed creates only «فريق رهون» and the market demo, the team roles carry the current permissions, the withdrawn
/// routes are gone, and the whole journey works. Also the privacy rules that depend on files and location.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CurrentModelTests(ApiFixture api)
{
    private static readonly string[] LegacySchemas =
        ["admin", "agreements", "analytics", "assessment", "cases", "closure", "comms", "complaints", "documents", "ecosystem", "providers", "referral", "requests", "sale", "solutions"];

    [Fact]
    public async Task Fresh_database_has_no_object_of_the_withdrawn_model()
    {
        var schemas = await api.WithDbAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT nspname AS \"Value\" FROM pg_namespace WHERE nspname NOT LIKE 'pg_%' AND nspname <> 'information_schema'").ToListAsync());
        Assert.Equal(["app", "audit", "directory", "files", "identity", "market", "public"], schemas.Order().ToList());
        Assert.Empty(schemas.Intersect(LegacySchemas));

        var tables = await api.WithDbAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT table_schema || '.' || table_name AS \"Value\" FROM information_schema.tables WHERE table_schema IN ('app','audit','directory','files','identity','public')").ToListAsync());
        Assert.Equal(
            ["app.idempotency_records", "app.outbound_sms", "app.reference_counters", "audit.audit_events", "directory.organizations", "files.file_blobs",
             "files.stored_files", "identity.individual_profiles", "identity.membership_roles", "identity.memberships", "identity.organizations",
             "identity.otp_challenges", "identity.role_permissions", "identity.roles", "identity.sessions", "identity.staff_invitations", "identity.terms_acceptances", "identity.users",
             "public.__ef_migrations"],
            tables.Order().ToList());

        var legacyColumns = await api.WithDbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT table_schema || '.' || table_name || '.' || column_name AS "Value" FROM information_schema.columns
            WHERE table_schema IN ('identity','audit','market')
              AND column_name IN ('case_id','case_reference','national_id_enc','national_id_hash','owner_access_id','step_up_until','team_id','storage_key','allowed_email_domains')
            """).ToListAsync());
        Assert.Empty(legacyColumns);
        var functions = await api.WithDbAsync(db => db.Database.SqlQueryRaw<string>(
            "SELECT routine_schema || '.' || routine_name AS \"Value\" FROM information_schema.routines WHERE routine_schema NOT IN ('pg_catalog','information_schema')").ToListAsync());
        Assert.Equal(["audit.reject_audit_mutation"], functions);
    }

    [Fact]
    public async Task Fresh_database_has_only_the_rahoon_team_its_current_permissions_and_the_market_demo()
    {
        var kinds = await api.WithDbAsync(db => db.Organizations.Select(o => o.Kind).ToListAsync());
        Assert.Equal([OrganizationKind.Operator], kinds.Distinct().ToList());
        Assert.True(await api.WithDbAsync(db => db.Opportunities.CountAsync()) >= 5);
        var permissions = await api.WithDbAsync(db => db.RolePermissions.Select(p => p.PermissionKey).Distinct().ToListAsync());
        Assert.All(permissions, p => Assert.Contains(p, P.AllKeys));
        var leadPerms = await api.WithDbAsync(db => db.Roles.Include(r => r.Permissions)
            .Where(r => r.Key == SystemRoles.PlatformOwner).SelectMany(r => r.Permissions.Select(p => p.PermissionKey)).ToListAsync());
        Assert.Contains(P.MarketPublish, leadPerms);
        Assert.Contains(P.DirectoryManage, leadPerms);
        // The demo never puts fictional organizations into the directory.
        Assert.Equal(0, await api.WithDbAsync(db => db.DirectoryOrganizations.CountAsync(d => d.NameAr.Contains("تجريبي"))));
    }

    [Theory]
    [InlineData("/api/cases")]
    [InlineData("/api/my/requests")]
    [InlineData("/api/team/requests")]
    [InlineData("/api/owner/case")]
    [InlineData("/api/platform/overview")]
    [InlineData("/api/public/invitations/x")]
    public async Task Withdrawn_routes_are_gone(string path)
    {
        var team = await api.LoginAsync(Lead);
        var (status, _) = await team.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task Team_signs_in_and_catalog_and_search_work()
    {
        var team = await api.LoginAsync(Lead);
        var (s1, overview) = await team.GetAsync("/api/team/market/overview");
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.NotNull(overview!["myTasks"]);
        var (_, me) = await team.GetAsync("/api/auth/me");
        Assert.Equal("/team", TestClient.Str(me, "home"));
        var (_, catalog) = await api.Client().GetAsync("/api/market/catalog");
        Assert.Null(catalog!["parties"]);
        var (_, search) = await api.Client().GetAsync("/api/market/opportunities");
        Assert.True(search!["total"]!.GetValue<int>() >= 3);
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
