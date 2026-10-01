using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.OrgDirectory;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// The organization directory: idempotent imports that merge types and never overwrite administrator edits, administration
/// (search, add, edit, deactivate — never delete) with audit, and its use in the owner and buyer forms.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DirectoryTests(ApiFixture api)
{
    private static int _seq;
    private static string Unique(string name) => $"{name} {Interlocked.Increment(ref _seq)}{Guid.NewGuid().ToString("N")[..6]}";
    private static readonly DateOnly Today = new(2026, 10, 1);

    private static DirectoryCandidate Candidate(string source, string key, string nameAr, string? nameEn, string type, string? website = null,
        string? license = null) =>
        new(source, key, nameAr, nameEn, [type], website, license, null, "مصدر اختبار رسمي", "https://example.gov.sa/list", Today);

    private async Task<ImportReport> ImportAsync(params DirectoryCandidate[] candidates)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        using var _ = db.Request.BeginSystemScope();
        return await scope.ServiceProvider.GetRequiredService<DirectoryImporter>()
            .ImportAsync(candidates.GroupBy(c => c.SourceKey).Select(g => new SourceResult(g.Key, g.ToList(), [], [])), dryRun: false);
    }

    private Task<DirectoryOrganization> FindAsync(string nameAr) =>
        api.WithDbAsync(db => db.DirectoryOrganizations.AsNoTracking().SingleAsync(d => d.NormalizedNameAr == DirectoryNames.Normalize(nameAr)));

    [Fact]
    public async Task Rerunning_an_import_creates_no_duplicates_and_changes_nothing()
    {
        var bank = Candidate("test-banks", Guid.NewGuid().ToString("N"), Unique("مصرف الاختبار"), "Test Bank", OrgTypes.Bank, "https://bank.example.sa/ar/home");
        var first = await ImportAsync(bank);
        Assert.Equal(1, first.Created);
        var second = await ImportAsync(bank);
        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Updated);
        Assert.Equal(1, second.Unchanged);
        var d = await FindAsync(bank.NameAr);
        Assert.Equal("import", d.Origin);
        Assert.True(d.Active);
        Assert.Equal("https://bank.example.sa/ar/home", d.Website);
        Assert.Equal(Today, d.VerifiedOn);
    }

    [Fact]
    public async Task An_item_listed_twice_by_its_source_is_counted_once_and_reruns_stay_stable()
    {
        var key = Guid.NewGuid().ToString("N");
        var first = Candidate("test-dev", key, Unique("شركة مكررة العقارية"), null, OrgTypes.Developer) with { SourceUrl = "https://example.gov.sa/list?page=4" };
        var again = first with { SourceUrl = "https://example.gov.sa/list?page=9" };
        var run1 = await ImportAsync(first, again);
        Assert.Equal(1, run1.Created);
        var run2 = await ImportAsync(first, again);
        Assert.Equal(0, run2.Updated);
        Assert.Equal("https://example.gov.sa/list?page=4", (await FindAsync(first.NameAr)).SourceUrl);
    }

    [Fact]
    public async Task Names_are_normalized_and_types_merge_into_one_record()
    {
        var name = Unique("شركة الأفق للتطوير");
        await ImportAsync(Candidate("test-dev", Guid.NewGuid().ToString("N"), name, null, OrgTypes.Developer));
        // Same organization from another source: hamza/taa-marbuta/diacritics differ, and it is listed as a finance company.
        var variant = name.Replace("الأفق", "الافق").Replace("شركة", "شركه");
        var report = await ImportAsync(Candidate("test-fin", Guid.NewGuid().ToString("N"), variant, "Horizon Co", OrgTypes.FinanceCompany));
        Assert.Equal(0, report.Created);
        Assert.Equal(1, report.Updated);
        var d = await FindAsync(name);
        Assert.Equal([OrgTypes.Developer, OrgTypes.FinanceCompany], d.Types);
        Assert.Equal("Horizon Co", d.NameEn); // a gap filled by the second source
        Assert.Equal(name, d.NameAr);          // the first source's name is kept
        Assert.Equal(2, d.ImportKeys.Count);
    }

    [Fact]
    public async Task Administrator_edits_are_never_overwritten_by_imports()
    {
        var key = Guid.NewGuid().ToString("N");
        var original = Candidate("test-banks", key, Unique("بنك المحرر"), "Edited Bank", OrgTypes.Bank, "https://edited.example.sa");
        await ImportAsync(original);
        var d = await FindAsync(original.NameAr);

        var lead = await api.LoginAsync(Lead);
        var (s, edited) = await lead.PutAsync($"/api/team/directory/{d.Id}", new
        {
            nameAr = original.NameAr, nameEn = "Edited Bank (corrected)", types = new[] { "bank" }, website = "https://corrected.example.sa",
            sourceName = "تصحيح يدوي", version = d.Version,
        });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(edited!["adminProtected"]!.GetValue<bool>());

        // The source still says the old values (and a new website): nothing changes.
        var report = await ImportAsync(original with { Website = "https://changed-by-source.example.sa", NameEn = "Source Name" });
        Assert.Equal(1, report.SkippedAdminEdited);
        var after = await FindAsync(original.NameAr);
        Assert.Equal("Edited Bank (corrected)", after.NameEn);
        Assert.Equal("https://corrected.example.sa", after.Website);
        Assert.Equal("تصحيح يدوي", after.SourceName);
    }

    [Fact]
    public async Task Imports_never_deactivate_or_delete_and_a_failed_source_changes_nothing()
    {
        var c = Candidate("test-banks", Guid.NewGuid().ToString("N"), Unique("بنك ثابت"), null, OrgTypes.Bank);
        await ImportAsync(c);
        var before = await api.WithDbAsync(db => db.DirectoryOrganizations.CountAsync());
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
            using var _ = db.Request.BeginSystemScope();
            // The source is down: an empty result with a failure.
            var report = await scope.ServiceProvider.GetRequiredService<DirectoryImporter>()
                .ImportAsync([new SourceResult("test-banks", [], ["source unavailable: timeout"], [])], dryRun: false);
            Assert.Equal(1, report.Failed);
        }
        Assert.Equal(before, await api.WithDbAsync(db => db.DirectoryOrganizations.CountAsync()));
        Assert.True((await FindAsync(c.NameAr)).Active);
    }

    [Fact]
    public async Task Invalid_candidates_are_reported_as_failed_not_invented()
    {
        var report = await ImportAsync(
            Candidate("test-bad", "1", "", "No Arabic", OrgTypes.Bank),
            Candidate("test-bad", "2", Unique("جهة بلا نوع"), null, "lender"),
            Candidate("test-bad", "3", Unique("جهة موقعها خطأ"), null, OrgTypes.Bank, website: "not a url"));
        Assert.Equal(3, report.Failed);
        Assert.Equal(0, report.Created);
    }

    [Fact]
    public async Task Dataset_files_go_through_the_same_pipeline()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rahoon-directory-{Guid.NewGuid():N}.csv");
        var name = Unique("شركة البيانات للتمويل");
        await File.WriteAllTextAsync(path,
            "name_ar,name_en,types,website,license_number,source_name,source_url,verified_on\n" +
            $"\"{name}\",Data Finance,finance_company,https://data.example.sa,12/ ش ت/2020,\"قائمة رسمية\",https://example.gov.sa/list.csv,2026-09-30\n" +
            "\"\",Missing Arabic,bank,,,x,https://example.gov.sa,2026-09-30\n");
        try
        {
            var result = DirectoryImporter.ReadDatasetFile(path, Today);
            Assert.Single(result.Candidates);
            Assert.Single(result.Failures);
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
            using var _ = db.Request.BeginSystemScope();
            var importer = scope.ServiceProvider.GetRequiredService<DirectoryImporter>();
            var report = await importer.ImportAsync([result], dryRun: false);
            Assert.Equal(1, report.Created);
            Assert.Equal(1, report.Failed);
            var again = await importer.ImportAsync([DirectoryImporter.ReadDatasetFile(path, Today)], dryRun: false);
            Assert.Equal(0, again.Created);
            var d = await FindAsync(name);
            Assert.Equal("12/ ش ت/2020", d.LicenseNumber);
            Assert.Equal(new DateOnly(2026, 9, 30), d.VerifiedOn);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Administrators_list_search_add_edit_and_deactivate_with_audit()
    {
        var lead = await api.LoginAsync(Lead);
        var name = Unique("مطور يدوي");
        var (sc, created) = await lead.PostAsync("/api/team/directory", new
        {
            nameAr = name, nameEn = "Manual Developer", types = new[] { "developer", "bank" }, website = "manual.example.sa",
        });
        Assert.Equal(HttpStatusCode.OK, sc);
        Assert.Equal("manual", TestClient.Str(created, "origin"));
        Assert.Equal("https://manual.example.sa", TestClient.Str(created, "website"));
        var id = TestClient.Str(created, "id");

        // A duplicate (normalized) name is refused.
        var (dup, dupBody) = await lead.PostAsync("/api/team/directory", new { nameAr = name.Replace("ي", "ى"), types = new[] { "developer" } });
        Assert.Equal(HttpStatusCode.Conflict, dup);
        Assert.Equal("duplicate", TestClient.Str(dupBody, "code"));
        // A licence number needs the source that publishes it.
        var (noSource, _) = await lead.PostAsync("/api/team/directory", new { nameAr = Unique("جهة برقم"), types = new[] { "bank" }, licenseNumber = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, noSource);

        var (_, search) = await lead.GetAsync($"/api/team/directory?q={Uri.EscapeDataString(name)}&type=developer&status=active");
        Assert.Equal(1, search!["total"]!.GetValue<int>());
        var (_, byOrigin) = await lead.GetAsync("/api/team/directory?origin=manual");
        Assert.Contains(byOrigin!["items"]!.AsArray(), i => TestClient.Str(i, "id") == id);

        // Deactivation needs a reason; the record stays (no delete endpoint).
        var (noReason, _) = await lead.PostAsync($"/api/team/directory/{id}/deactivate", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason);
        var (sd, deactivated) = await lead.PostAsync($"/api/team/directory/{id}/deactivate", new { reason = "لم تعد تعمل في السوق" });
        Assert.Equal(HttpStatusCode.OK, sd);
        Assert.False(deactivated!["active"]!.GetValue<bool>());
        var (del, _) = await lead.DeleteAsync($"/api/team/directory/{id}");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, del);
        var (_, inactive) = await lead.GetAsync($"/api/team/directory?q={Uri.EscapeDataString(name)}&status=inactive");
        Assert.Equal(1, inactive!["total"]!.GetValue<int>());

        var events = await api.WithDbAsync(db => db.AuditEvents.Where(e => e.SubjectReference == id).Select(e => e.Type).ToListAsync());
        Assert.Contains("directory.created", events);
        Assert.Contains("directory.deactivated", events);
    }

    [Fact]
    public async Task Stale_edits_are_refused()
    {
        var lead = await api.LoginAsync(Lead);
        var (_, created) = await lead.PostAsync("/api/team/directory", new { nameAr = Unique("جهة نسخة"), types = new[] { "bank" } });
        var id = TestClient.Str(created, "id");
        var version = created!["version"]!.GetValue<uint>();
        var body = new { nameAr = TestClient.Str(created, "nameAr"), types = new[] { "bank" }, nameEn = "One", version };
        Assert.Equal(HttpStatusCode.OK, (await lead.PutAsync($"/api/team/directory/{id}", body)).Status);
        var (stale, b) = await lead.PutAsync($"/api/team/directory/{id}", body with { nameEn = "Two" });
        Assert.Equal(HttpStatusCode.Conflict, stale);
        Assert.Equal("stale", TestClient.Str(b, "code"));
    }

    [Fact]
    public async Task Owner_form_uses_the_directory_and_requests_keep_the_recorded_name()
    {
        var developer = Candidate("test-dev", Guid.NewGuid().ToString("N"), Unique("شركة الدليل العقارية"), null, OrgTypes.Developer);
        var bank = Candidate("test-banks", Guid.NewGuid().ToString("N"), Unique("بنك الدليل"), null, OrgTypes.Bank);
        await ImportAsync(developer, bank);
        var dev = await FindAsync(developer.NameAr);
        var bnk = await FindAsync(bank.NameAr);

        var (_, devList) = await api.Client().GetAsync("/api/directory/organizations?kind=developer");
        Assert.Contains(devList!["items"]!.AsArray(), i => TestClient.Str(i, "id") == dev.Id.ToString());
        Assert.DoesNotContain(devList["items"]!.AsArray(), i => TestClient.Str(i, "id") == bnk.Id.ToString());
        var (_, finList) = await api.Client().GetAsync("/api/directory/organizations?kind=financier");
        Assert.Contains(finList!["items"]!.AsArray(), i => TestClient.Str(i, "id") == bnk.Id.ToString());

        var owner = await SellerAsync(api);
        // A bank is not a developer: refused for a developer obligation.
        var (bad, badBody) = await owner.PostAsync("/api/market/sale-requests", Draft(bnk.Id));
        Assert.Equal(HttpStatusCode.OK, bad);
        Assert.NotNull(badBody!["invalid"]!["o0.party"]);
        Assert.Null(badBody["file"]!["obligations"]![0]!["partyId"]?.GetValue<string?>());
        var (s, body) = await owner.PostAsync("/api/market/sale-requests", Draft(dev.Id));
        Assert.Equal(HttpStatusCode.OK, s);
        var reference = body!["file"]!["reference"]!.GetValue<string>();
        var obligation = body["file"]!["obligations"]![0]!;
        Assert.Equal(dev.Id.ToString(), TestClient.Str(obligation, "partyId"));
        Assert.Equal(developer.NameAr, TestClient.Str(obligation, "partyName"));

        // The administrator renames and deactivates it: the request keeps the name and reference it recorded.
        var lead = await api.LoginAsync(Lead);
        var current = await FindAsync(developer.NameAr);
        await Ok(lead.PutAsync($"/api/team/directory/{dev.Id}", new { nameAr = developer.NameAr + " المحدّثة", types = new[] { "developer" }, version = current.Version }));
        var renamed = await api.WithDbAsync(db => db.DirectoryOrganizations.AsNoTracking().SingleAsync(d => d.Id == dev.Id));
        await Ok(lead.PostAsync($"/api/team/directory/{dev.Id}/deactivate", new { reason = "اختبار الإيقاف", version = renamed.Version }));
        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        var kept = file!["obligations"]![0]!;
        Assert.Equal(dev.Id.ToString(), TestClient.Str(kept, "partyId"));
        Assert.Equal(developer.NameAr, TestClient.Str(kept, "partyName"));
        var (_, usage) = await lead.GetAsync($"/api/team/directory/{dev.Id}");
        Assert.Equal(1, usage!["usage"]!.GetValue<int>());

        // A deactivated organization can no longer be chosen in a new request.
        var other = await SellerAsync(api);
        var (_, refused) = await other.PostAsync("/api/market/sale-requests", Draft(dev.Id));
        Assert.NotNull(refused!["invalid"]!["o0.party"]);
        var (_, listAfter) = await api.Client().GetAsync("/api/directory/organizations?kind=developer");
        Assert.DoesNotContain(listAfter!["items"]!.AsArray(), i => TestClient.Str(i, "id") == dev.Id.ToString());
    }

    [Fact]
    public async Task Buyer_can_name_a_preferred_financier_from_the_directory()
    {
        var finance = Candidate("test-fin", Guid.NewGuid().ToString("N"), Unique("شركة التمويل المفضلة"), null, OrgTypes.FinanceCompany);
        await ImportAsync(finance);
        var org = await FindAsync(finance.NameAr);
        var buyer = await SellerAsync(api);
        var (s, body) = await buyer.PostAsync("/api/market/buyer-requests", new
        {
            clientDraftId = Guid.NewGuid(), availableNow = 150000, purchaseMode = "external_finance", preferredFinancierId = org.Id,
            cities = new[] { "riyadh" }, propertyTypes = new[] { "apartment" },
        });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(org.Id.ToString(), TestClient.Str(body!["request"], "preferredFinancierId"));
        Assert.Equal(finance.NameAr, TestClient.Str(body["request"], "preferredFinancierName"));
    }

    private static object Draft(Guid partyId) => new
    {
        clientDraftId = Guid.NewGuid(), propertyType = "apartment", city = "riyadh", district = "النرجس", obligationMode = "developer",
        answers = new Dictionary<string, string?> { ["area"] = "150" },
        obligations = new[] { new { kind = "developer", partyId, answers = new Dictionary<string, string?>() } },
    };
}
