using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>Independent cases and users for the B3 / L04 / L24 / C01 tests (never depends on test order).</summary>
public static class B3Scenarios
{
    public const string Alufuq = "مصرف الأفق";

    public static DateOnly TodayRiyadh => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    public static Task<TestClient> SaraAsync(ApiFixture api) => api.LoginAsync("s.alqahtani@alufuq.example", Alufuq);

    /// <summary>Wizard case moved to «تقييم». Legal review stays pending. Optionally an accepted report valid for <paramref name="validDays"/> days.</summary>
    public static async Task<string> AtValuationAsync(ApiFixture api, int? validDays = 87)
    {
        var sara = await SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_verification", expectedStatus = "awaiting_data" })).Status);
        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            foreach (var (key, name) in new[] { ("title_deed", "صك الملكية"), ("national_id", "صورة الهوية الوطنية"), ("financing_contract", "عقد التمويل") })
                db.Documents.Add(new CaseDocument { OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = key, Name = name, Status = DocumentStatus.Verified, Source = DocumentSource.Lender });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_valuation", expectedStatus = "verification" })).Status);
        if (validDays is { } days) await AddReportAsync(api, r, ValuationStatus.Accepted, days, 1_000_000m);
        return r;
    }

    public static Task<Guid> AddReportAsync(ApiFixture api, string r, ValuationStatus status, int validDays, decimal value, int version = 1) =>
        api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var report = new ValuationReport
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, VersionNo = version, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = value,
                RangeLow = value * 0.96m, RangeHigh = value * 1.04m, Methodology = "المقارنة", ReportDate = TodayRiyadh.AddDays(validDays - 90),
                ValidUntil = TodayRiyadh.AddDays(validDays), Status = status,
            };
            db.ValuationReports.Add(report);
            await db.SaveChangesAsync();
            return report.Id;
        });

    /// <summary>Adds a document version (verified or pending) of the given type directly; returns the version id.</summary>
    public static Task<Guid> AddDocumentVersionAsync(ApiFixture api, string r, string typeKey, ReviewStatus review) =>
        api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var doc = new CaseDocument
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = typeKey, Name = typeKey == "salary_statement" ? "كشف الراتب لآخر 3 أشهر" : typeKey,
                Status = review == ReviewStatus.Verified ? DocumentStatus.Verified : DocumentStatus.InReview, Source = DocumentSource.Owner, VersionCount = 1,
            };
            var v = new DocumentVersion
            {
                OrganizationId = c.OrganizationId, DocumentId = doc.Id, CaseId = c.Id, VersionNo = 1, FileName = "x.pdf", ContentType = "application/pdf",
                SizeBytes = 10, Sha256 = "00", StorageKey = "test/none", UploadedAt = DateTimeOffset.UtcNow, ReviewStatus = review,
                ReviewedAt = review == ReviewStatus.Verified ? DateTimeOffset.UtcNow : null,
            };
            doc.CurrentVersionId = v.Id;
            db.Documents.Add(doc);
            db.DocumentVersions.Add(v);
            await db.SaveChangesAsync();
            return v.Id;
        });

    /// <summary>Creates an Alufuq user whose single role holds exactly <paramref name="permissions"/> and signs them in.</summary>
    public static async Task<TestClient> UserWithPermissionsAsync(ApiFixture api, string label, params string[] permissions)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{label}-{suffix}@alufuq.example";
        await api.WithDbAsync(async db =>
        {
            var hasher = api.Services.GetRequiredService<IPasswordHasher<User>>();
            var org = await db.Organizations.FirstAsync(o => o.ShortCode == "alufuq");
            var team = await db.Teams.Where(t => t.OrganizationId == org.Id).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
            var role = new Role { OrganizationId = org.Id, Key = $"test_{label}_{suffix}", NameAr = "دور اختبار", NameEn = "Test role" };
            role.Permissions.AddRange(permissions.Distinct().Select(p => new RolePermission { PermissionKey = p }));
            db.Roles.Add(role);
            var now = DateTimeOffset.UtcNow;
            var user = new User { Email = email, FullName = "مستخدم اختبار " + suffix, Phone = "0550009" + Random.Shared.Next(100, 999), MfaEnrolled = true, CreatedAt = now, UpdatedAt = now };
            user.PasswordHash = hasher.HashPassword(user, ApiFixture.Password);
            db.Users.Add(user);
            var m = new Membership { OrganizationId = org.Id, UserId = user.Id, Title = "اختبار", TeamId = team, CreatedAt = now, UpdatedAt = now };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = role.Id });
            db.Memberships.Add(m);
            return await db.SaveChangesAsync();
        });
        return await api.LoginAsync(email);
    }

    public static MultipartFormDataContent CsvContent(string csv, string fileName = "batch.csv")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);
        return content;
    }

    public static async Task<(HttpStatusCode Status, JsonNode? Body)> UploadCsvAsync(TestClient client, string csv, string fileName = "batch.csv")
    {
        var (s, bytes, _) = await client.SendRawAsync(HttpMethod.Post, "/api/cases/imports", CsvContent(csv, fileName));
        return (s, bytes.Length == 0 ? null : JsonNode.Parse(bytes));
    }
}
