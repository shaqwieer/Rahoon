using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>V01–V04 and the lender side of assignments: isolation, the access window and the review loop.</summary>
[Collection(ApiCollection.Name)]
public sealed class ProviderAssignmentTests(ApiFixture api)
{
    internal static byte[] Pdf(string label) => Encoding.ASCII.GetBytes($"%PDF-1.4\n% rahoon test {label}\n%%EOF\n");

    internal sealed record Asg(string CaseRef, string Id, string DocId, TestClient Sara);

    /// <summary>Wizard case + an uploaded photo document + a valuation assignment to «مكتب تقييم معتمد «ب»».</summary>
    internal static async Task<Asg> NewAssignmentAsync(ApiFixture api)
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Pdf("photos " + r)), "file", "photos.pdf" },
            { new StringContent("property_photos"), "documentTypeKey" },
        };
        var (su, up) = await sara.PostMultipartAsync($"/api/cases/{r}/documents", form);
        Assert.True(su == HttpStatusCode.OK, up?.ToJsonString());
        var docId = TestClient.Str(up, "documentId");

        var valuer = await api.WithDbAsync(db => db.Organizations.FirstAsync(o => o.ShortCode == "valuer-b"));
        var (s, created) = await sara.PostAsync($"/api/cases/{r}/assignments", new
        {
            providerOrganizationId = valuer.Id, type = "valuation", dueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(6).ToString("yyyy-MM-dd"),
            scope = new[] { "نوع التقييم — قيمة سوقية · للعقار السكني" }, sharedDocumentIds = new[] { docId },
            inspectionAt = DateTimeOffset.UtcNow.AddDays(1),
        });
        Assert.True(s == HttpStatusCode.OK, created?.ToJsonString());
        Assert.StartsWith("ASG-", TestClient.Str(created, "reference"));
        return new Asg(r, TestClient.Str(created, "id"), docId, sara);
    }

    internal static MultipartFormDataContent SubmissionForm(bool independence = true, string market = "742,000.00")
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Pdf("report")), "file", "تقرير_التقييم.pdf" },
            { new StringContent(market), "marketValue" },
            { new StringContent("720000"), "rangeLow" },
            { new StringContent("765000"), "rangeHigh" },
            { new StringContent("أسلوب المقارنة بالمبيعات"), "methodology" },
            { new StringContent("5"), "comparablesCount" },
            { new StringContent(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd")), "inspectionDate" },
            { new StringContent("comparables_12m"), "checklist" },
            { new StringContent("occupancy_stated"), "checklist" },
            { new StringContent("photos_no_faces"), "checklist" },
        };
        if (independence) form.Add(new StringContent("true"), "independenceDeclared");
        return form;
    }

    [Fact]
    public async Task Provider_sees_only_its_own_assignment_and_no_lender_data()
    {
        var a = await NewAssignmentAsync(api);
        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        var waleed = await api.LoginAsync("w.alqahtani@broker-d.example");

        // Another provider: same refusal as an unknown id, and nothing in its inbox.
        var foreign = await waleed.GetAsync($"/api/provider/assignments/{a.Id}");
        var unknown = await waleed.GetAsync($"/api/provider/assignments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.Status);
        Assert.Equal(unknown.Status, foreign.Status);
        Assert.Equal(TestClient.Str(unknown.Body, "title"), TestClient.Str(foreign.Body, "title"));
        var (_, inbox) = await waleed.GetAsync("/api/provider/assignments");
        Assert.Empty(inbox!["items"]!.AsArray());

        // The assigned provider cannot use lender endpoints for the same case.
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync($"/api/cases/{a.CaseRef}/assignments")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync($"/api/cases/{a.CaseRef}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.GetAsync($"/api/cases/{a.CaseRef}/documents")).Status);
        // Live assignments put the lender in his readable organizations; no other lender-data endpoint may use that.
        foreach (var path in new[] { "/api/search?q=RH-2026", "/api/tasks", "/api/complaints", "/api/approvals", "/api/portfolio", "/api/settings/users" })
        {
            var (ls, lb) = await omar.GetAsync(path);
            Assert.True(ls == HttpStatusCode.Forbidden, $"{path}: {ls} {lb?.ToJsonString()}");
        }
        var (_, notes) = await omar.GetAsync("/api/notifications");
        Assert.DoesNotContain(a.CaseRef, notes?.ToJsonString() ?? "");

        // Detail: scope, shared documents, masked contact — no case reference, debt figures or party identities.
        var (s, detail) = await omar.GetAsync($"/api/provider/assignments/{a.Id}");
        Assert.Equal(HttpStatusCode.OK, s);
        var json = detail!.ToJsonString();
        Assert.DoesNotContain(a.CaseRef, json);
        Assert.DoesNotContain("RH-", json);
        Assert.DoesNotContain("الغامدي", json); // owner full name; only «خالد س.» is shown
        Assert.DoesNotContain("1012345678", json);
        Assert.DoesNotContain("700000", json);
        Assert.DoesNotContain("outstanding", json, StringComparison.OrdinalIgnoreCase);
        Assert.Single(detail["documents"]!.AsArray());
        Assert.Contains("المالك", TestClient.Str(detail["inspection"], "contact"));

        // Only shared documents can be downloaded; any other id is refused as unknown.
        var otherDoc = await api.WithDbAsync(db => db.Documents.Where(d => db.Cases.Any(c => c.Id == d.CaseId && c.Reference == "RH-2026-004172")).Select(d => d.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await omar.GetBytesAsync($"/api/provider/assignments/{a.Id}/documents/{otherDoc}")).Status);
        var (ds, bytes, type) = await omar.GetBytesAsync($"/api/provider/assignments/{a.Id}/documents/{a.DocId}");
        Assert.Equal(HttpStatusCode.OK, ds);
        Assert.Equal("application/pdf", type);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public async Task Submission_requires_independence_declaration()
    {
        var a = await NewAssignmentAsync(api);
        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        var (s, body) = await omar.PostMultipartAsync($"/api/provider/assignments/{a.Id}/submissions", SubmissionForm(independence: false));
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.NotNull(body!["errors"]!["independenceDeclared"]);

        // Control: the same form with the declaration is accepted as v1.
        var (ok, sub) = await omar.PostMultipartAsync($"/api/provider/assignments/{a.Id}/submissions", SubmissionForm());
        Assert.True(ok == HttpStatusCode.OK, sub?.ToJsonString());
        Assert.Equal(1, sub!["version"]!.GetValue<int>());
    }

    [Fact]
    public async Task Access_window_write_until_delivery_read_only_seven_days_then_gone()
    {
        var a = await NewAssignmentAsync(api);
        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");

        // Before delivery: RFI thread and inspection confirmation work; the lender sees the question.
        var (cs, confirmed) = await omar.PostAsync($"/api/provider/assignments/{a.Id}/inspection/confirm", new { });
        Assert.Equal(HttpStatusCode.OK, cs);
        Assert.Equal("inProgress", TestClient.Str(confirmed, "status"));
        Assert.Equal(HttpStatusCode.OK, (await omar.PostAsync($"/api/provider/assignments/{a.Id}/messages", new { body = "هل يتوفر مخطط البناء المعتمد؟" })).Status);
        var (_, lenderThread) = await a.Sara.GetAsync($"/api/cases/{a.CaseRef}/assignments/{a.Id}/messages");
        Assert.Contains(lenderThread!.AsArray(), m => TestClient.Str(m, "side") == "provider");
        Assert.Equal(HttpStatusCode.OK, (await a.Sara.PostAsync($"/api/cases/{a.CaseRef}/assignments/{a.Id}/messages", new { body = "أُضيف المخطط إلى مستندات التكليف." })).Status);

        // Delivery (v1): write access ends, read-only access remains.
        var (ss, sub) = await omar.PostMultipartAsync($"/api/provider/assignments/{a.Id}/submissions", SubmissionForm());
        Assert.True(ss == HttpStatusCode.OK, sub?.ToJsonString());
        Assert.Equal(HttpStatusCode.Conflict, (await omar.PostAsync($"/api/provider/assignments/{a.Id}/messages", new { body = "سؤال بعد التسليم" })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await omar.PostAsync($"/api/provider/assignments/{a.Id}/inspection/confirm", new { })).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await omar.PostMultipartAsync($"/api/provider/assignments/{a.Id}/submissions", SubmissionForm())).Status);
        var (rs, ro) = await omar.GetAsync($"/api/provider/assignments/{a.Id}");
        Assert.Equal(HttpStatusCode.OK, rs);
        Assert.Equal("read_only", TestClient.Str(ro!["access"], "mode"));
        Assert.Equal(HttpStatusCode.OK, (await omar.GetBytesAsync($"/api/provider/assignments/{a.Id}/documents/{a.DocId}")).Status);

        // Return with itemised notes reopens write access (C1); v2 is then accepted.
        var due = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(4).ToString("yyyy-MM-dd");
        var (ret, retBody) = await fahad.PostAsync($"/api/cases/{a.CaseRef}/assignments/{a.Id}/submissions/1/review",
            new { decision = "return", notes = new[] { "صفقات المقارنة أقدم من 12 شهراً (2 من 5). يرجى تحديثها.", "ينقص ذكر حالة الإشغال." }, resubmitDueOn = due });
        Assert.True(ret == HttpStatusCode.OK, retBody?.ToJsonString());
        var (_, returned) = await omar.GetAsync($"/api/provider/assignments/{a.Id}");
        Assert.Equal("returned", TestClient.Str(returned, "status"));
        Assert.Equal(2, returned!["returnAlert"]!["notes"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.OK, (await omar.PostAsync($"/api/provider/assignments/{a.Id}/messages", new { body = "سنحدّث صفقات المقارنة." })).Status);
        var (s2, v2) = await omar.PostMultipartAsync($"/api/provider/assignments/{a.Id}/submissions", SubmissionForm(market: "745000"));
        Assert.True(s2 == HttpStatusCode.OK, v2?.ToJsonString());
        Assert.Equal(2, v2!["version"]!.GetValue<int>());

        // Reviewing a superseded version is refused; accepting v2 creates the accepted valuation.
        Assert.Equal(HttpStatusCode.Conflict, (await fahad.PostAsync($"/api/cases/{a.CaseRef}/assignments/{a.Id}/submissions/1/review", new { decision = "accept" })).Status);
        var (acc, accBody) = await fahad.PostAsync($"/api/cases/{a.CaseRef}/assignments/{a.Id}/submissions/2/review", new { decision = "accept", note = "التقرير مكتمل." });
        Assert.True(acc == HttpStatusCode.OK, accBody?.ToJsonString());
        var asgId = Guid.Parse(a.Id);
        var (asg, report) = await api.WithDbAsync(async db =>
            (await db.Assignments.FirstAsync(x => x.Id == asgId), await db.ValuationReports.FirstOrDefaultAsync(r => r.AssignmentId == asgId)));
        Assert.NotNull(report);
        Assert.Equal(ValuationStatus.Accepted, report.Status);
        Assert.Equal(745_000m, report.MarketValue);
        Assert.Equal(2, report.VersionNo);
        Assert.NotNull(asg.DeliveredAt);
        Assert.Equal(asg.DeliveredAt!.Value.AddDays(7), asg.AccessExpiresAt);
        Assert.Equal(HttpStatusCode.OK, (await omar.GetAsync($"/api/provider/assignments/{a.Id}")).Status);

        // After the 7-day window: the assignment is gone — same refusal as an unknown id.
        await api.WithDbAsync(async db =>
        {
            var x = await db.Assignments.FirstAsync(y => y.Id == asgId);
            x.AccessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        var gone = await omar.GetAsync($"/api/provider/assignments/{a.Id}");
        var unknown = await omar.GetAsync($"/api/provider/assignments/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, gone.Status);
        Assert.Equal(TestClient.Str(unknown.Body, "title"), TestClient.Str(gone.Body, "title"));
        Assert.Equal(HttpStatusCode.NotFound, (await omar.GetBytesAsync($"/api/provider/assignments/{a.Id}/documents/{a.DocId}")).Status);
        var (_, delivered) = await omar.GetAsync("/api/provider/assignments?status=delivered");
        Assert.DoesNotContain(delivered!["items"]!.AsArray(), i => TestClient.Str(i, "id") == a.Id);
    }

    [Fact]
    public async Task Seeded_inbox_spans_lenders_and_shows_returned_notes()
    {
        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        var (s, inbox) = await omar.GetAsync("/api/provider/assignments?status=active");
        Assert.Equal(HttpStatusCode.OK, s);
        var refs = inbox!["items"]!.AsArray().Select(i => TestClient.Str(i, "reference")).ToList();
        Assert.Contains("ASG-2026-0871", refs);
        Assert.Contains("ASG-2026-0864", refs);
        Assert.Contains("ASG-2026-0880", refs);
        Assert.DoesNotContain("ASG-2026-0418", refs); // delivered and expired long ago
        var returned = inbox["items"]!.AsArray().First(i => TestClient.Str(i, "reference") == "ASG-2026-0864");
        Assert.Equal("error", TestClient.Str(returned, "slaTone"));
    }

    [Fact]
    public async Task Sensitive_and_foreign_documents_cannot_be_shared()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var valuer = await api.WithDbAsync(db => db.Organizations.FirstAsync(o => o.ShortCode == "valuer-b"));
        var (salary, contractDoc) = await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == "RH-2026-004172");
            return (await db.Documents.Where(d => d.CaseId == c.Id && d.DocumentTypeKey == "salary_statement").Select(d => d.Id).FirstAsync(),
                    await db.Documents.Where(d => d.CaseId == c.Id && d.DocumentTypeKey == "financing_contract").Select(d => d.Id).FirstAsync());
        });
        foreach (var doc in new[] { salary, contractDoc })
        {
            var (s, body) = await sara.PostAsync("/api/cases/RH-2026-004172/assignments", new
            {
                providerOrganizationId = valuer.Id, type = "valuation", dueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd"),
                scope = new[] { "تقييم" }, sharedDocumentIds = new[] { doc },
            });
            Assert.Equal(HttpStatusCode.BadRequest, s);
            Assert.NotNull(body!["errors"]!["sharedDocumentIds"]);
        }
        // A lender org cannot be chosen as the provider.
        var sunbula = await api.WithDbAsync(db => db.Organizations.FirstAsync(o => o.ShortCode == "sunbula"));
        var (s2, b2) = await sara.PostAsync("/api/cases/RH-2026-004172/assignments", new
        {
            providerOrganizationId = sunbula.Id, type = "valuation", dueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5).ToString("yyyy-MM-dd"), scope = new[] { "تقييم" },
        });
        Assert.Equal(HttpStatusCode.BadRequest, s2);
        Assert.NotNull(b2!["errors"]!["providerOrganizationId"]);
    }
}
