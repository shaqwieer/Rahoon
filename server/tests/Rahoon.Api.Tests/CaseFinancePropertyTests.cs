using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>L07 finance, L08/L09 property &amp; mortgage (legal review guard), L10 document request preview.</summary>
[Collection(ApiCollection.Name)]
public sealed class CaseFinancePropertyTests(ApiFixture api)
{
    private const string Anchor = "RH-2026-004172";

    [Fact]
    public async Task Finance_total_is_the_sum_of_items_with_source_and_history()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (s, body) = await sara.GetAsync($"/api/cases/{Anchor}/finance");
        Assert.Equal(HttpStatusCode.OK, s);
        var snap = body!["snapshot"]!;
        var items = snap["items"]!.AsArray();
        Assert.Equal(4, items.Count);
        Assert.Equal(1_284_560.00m, items.Sum(i => i!["amount"]!.GetValue<decimal>()));
        Assert.Equal(1_284_560.00m, snap["total"]!.GetValue<decimal>());
        Assert.True(snap["totalVerified"]!.GetValue<bool>());
        Assert.StartsWith("نظام التمويل الأساسي", TestClient.Str(items[0], "source"));
        Assert.Equal("MF-88-3317•••", TestClient.Str(body["contract"], "contractNumberMasked"));
        Assert.DoesNotContain("MF-88-3317406", body.ToJsonString());

        var months = body["history"]!["months"]!.AsArray();
        Assert.Equal(12, months.Count);
        Assert.Equal("partial", TestClient.Str(months.First(m => TestClient.Str(m, "month") == "2026-05"), "status"));
        Assert.Contains("7 أقساط غير مسددة منذ 2026-02", TestClient.Str(body["history"], "summary"));
        Assert.Equal(96_420.00m, body["arrears"]!["reportedAmount"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Correction_request_creates_a_finance_task_and_never_edits_figures()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var before = await api.WithDbAsync(db => db.DebtSnapshots.AsNoTracking().FirstAsync(d => db.Cases.Any(c => c.Id == d.CaseId && c.Reference == r)));

        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/finance/correction-requests", new { field = "late_fees", proposedValue = "0" })).Status);
        var noura = await api.LoginAsync("n.alshehri@alufuq.example"); // approver: neither case.edit nor analysis.edit
        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsync($"/api/cases/{r}/finance/correction-requests",
            new { field = "late_fees", currentValue = "12000", proposedValue = "0", note = "الغرامات أُلغيت بقرار سابق موثق." })).Status);

        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        var (s, res) = await fahad.PostAsync($"/api/cases/{r}/finance/correction-requests",
            new { field = "late_fees", currentValue = "12000.00", proposedValue = "0.00", note = "الغرامات أُلغيت بقرار سابق موثق في النظام الأساسي." });
        Assert.True(s == HttpStatusCode.OK, res?.ToJsonString());
        var task = await api.WithDbAsync(db => db.Tasks.FirstAsync(t => t.Id == Guid.Parse(TestClient.Str(res, "taskId"))));
        Assert.Equal("data_correction", task.Kind);
        var assigneeRoles = await api.WithDbAsync(db => db.Memberships.Where(m => m.UserId == task.AssigneeUserId)
            .SelectMany(m => m.Roles).Select(x => x.Role!.Key).ToListAsync());
        Assert.Contains("finance", assigneeRoles);

        var after = await api.WithDbAsync(db => db.DebtSnapshots.AsNoTracking().FirstAsync(d => d.Id == before.Id));
        Assert.Equal(before.LateFees, after.LateFees);
        Assert.Equal(before.Total, after.Total);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "finance.correction_requested")));
    }

    [Fact]
    public async Task Pending_legal_review_blocks_propose_solution_until_legal_completes_it()
    {
        var r = await B3Scenarios.AtValuationAsync(api);
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        var (sBlocked, blocked) = await fahad.PostAsync($"/api/cases/{r}/transitions", new { action = "propose_solution", expectedStatus = "valuation" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sBlocked);
        Assert.Contains(blocked!["reasons"]!.AsArray(), x => x!.GetValue<string>() == "مراجعة القانونية للرهن لم تكتمل.");

        var sara = await B3Scenarios.SaraAsync(api);
        var (_, prop) = await sara.GetAsync($"/api/cases/{r}/property");
        Assert.Equal("pending", TestClient.Str(prop!["mortgage"]!["legalReview"], "status"));
        Assert.NotNull(prop["blocksProposal"]);
        Assert.False(prop["canLegalReview"]!.GetValue<bool>());
        // Only legal (agreement.activate) records the review.
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PostAsync($"/api/cases/{r}/mortgage/legal-review", new { note = "الرهن سليم ولا قيود على الصك." })).Status);

        var majed = await api.LoginAsync("m.alharbi@alufuq.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await majed.PostAsync($"/api/cases/{r}/mortgage/legal-review", new { note = "قصير" })).Status);
        var (sOk, ok) = await majed.PostAsync($"/api/cases/{r}/mortgage/legal-review",
            new { status = "complete", note = "الرهن مسجل بالدرجة الأولى ولا توجد قيود لاحقة.", deedMatched = true });
        Assert.True(sOk == HttpStatusCode.OK, ok?.ToJsonString());

        var (_, prop2) = await majed.GetAsync($"/api/cases/{r}/property");
        var review = prop2!["mortgage"]!["legalReview"]!;
        Assert.Equal("complete", TestClient.Str(review, "status"));
        Assert.Equal("ماجد الحربي", TestClient.Str(review, "reviewer"));
        Assert.True(prop2["mortgage"]!["deedMatched"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.OK, (await fahad.PostAsync($"/api/cases/{r}/transitions", new { action = "propose_solution", expectedStatus = "valuation" })).Status);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "mortgage.legal_review")));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "case.transition_blocked" && e.Blocked)));
    }

    [Fact]
    public async Task Property_edit_needs_case_edit_and_a_new_deed_reopens_legal_review()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        await api.WithDbAsync(async db =>
        {
            var m = await db.Mortgages.FirstAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r));
            m.LegalReviewStatus = LegalReviewStatus.Complete;
            m.DeedMatched = true;
            return await db.SaveChangesAsync();
        });
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PatchAsync($"/api/cases/{r}/property", new { district = "حي الملقا" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PatchAsync($"/api/cases/{r}/property", new { yearBuilt = 1800 })).Status);

        var (s1, res1) = await sara.PatchAsync($"/api/cases/{r}/property", new { district = "حي الملقا", occupancy = "Tenant" });
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(2, res1!["changed"]!.AsArray().Count);
        var (_, prop) = await sara.GetAsync($"/api/cases/{r}/property");
        Assert.Equal("مؤجّر لمستأجر", TestClient.Str(prop!["property"], "occupancyLabel"));
        Assert.Equal("complete", TestClient.Str(prop["mortgage"]!["legalReview"], "status"));

        Assert.Equal(HttpStatusCode.OK, (await sara.PatchAsync($"/api/cases/{r}/property", new { deedNumber = "3109988771" })).Status);
        var (_, prop2) = await sara.GetAsync($"/api/cases/{r}/property");
        Assert.Equal("pending", TestClient.Str(prop2!["mortgage"]!["legalReview"], "status"));
        Assert.DoesNotContain("3109988771", prop2.ToJsonString());
    }

    [Fact]
    public async Task Anchor_property_shows_mortgage_inspection_and_family_protection()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (s, body) = await sara.GetAsync($"/api/cases/{Anchor}/property");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(1_650_000.00m, body!["property"]!["valuationValue"]!.GetValue<decimal>());
        Assert.Matches("^3•+[0-9]{2}$", TestClient.Str(body["property"], "deedMasked"));
        Assert.NotNull(body["property"]!["protectionNote"]);
        Assert.Equal("الأولى", TestClient.Str(body["mortgage"], "rankLabel"));
        Assert.Equal("complete", TestClient.Str(body["mortgage"]!["legalReview"], "status"));
        Assert.Equal("completed", TestClient.Str(body["inspection"], "status"));
        Assert.Null(body["blocksProposal"]);
    }

    [Fact]
    public async Task Document_request_preview_renders_the_template_without_saving()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var due = B3Scenarios.TodayRiyadh.AddDays(10);
        var (s, body) = await sara.PostAsync($"/api/cases/{r}/documents/requests/preview", new { documentTypeKey = "salary_statement", dueOn = due.ToString("yyyy-MM-dd") });
        Assert.True(s == HttpStatusCode.OK, body?.ToJsonString());
        Assert.Equal($"نحتاج منك كشف الراتب لآخر 3 أشهر لمتابعة حالتك. يمكنك رفعه من صفحة المستندات حتى {due:yyyy-MM-dd}، وإن واجهت صعوبة فاكتب لنا.", TestClient.Str(body, "ownerMessage"));
        Assert.Equal("TPL-DOCREQ-01", TestClient.Str(body!["template"], "code"));
        Assert.Equal(10, body["daysFromToday"]!.GetValue<int>());
        Assert.EndsWith("هـ", TestClient.Str(body, "dueOnHijri"));
        Assert.False(body["saved"]!.GetValue<bool>());
        Assert.False(await api.WithDbAsync(db => db.DocumentRequests.AnyAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r))));

        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/documents/requests/preview", new { documentTypeKey = "salary_statement", dueOn = "2020-01-01" })).Status);
        var noura = await api.LoginAsync("n.alshehri@alufuq.example"); // no document.request
        Assert.Equal(HttpStatusCode.Forbidden, (await noura.PostAsync($"/api/cases/{r}/documents/requests/preview", new { documentTypeKey = "salary_statement", dueOn = due.ToString("yyyy-MM-dd") })).Status);

        // Existing document filters still work.
        var (sList, list) = await sara.GetAsync($"/api/cases/{Anchor}/documents?filter=requested");
        Assert.Equal(HttpStatusCode.OK, sList);
        Assert.True(list!["counts"]!["requested"]!.GetValue<int>() >= 1);
    }
}
