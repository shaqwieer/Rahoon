using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Tests.Infrastructure;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Tests;

/// <summary>L11 valuation (review, revaluation 409 rule, timeline) and L12 analysis (DSR, verified income, concurrency).</summary>
[Collection(ApiCollection.Name)]
public sealed class ValuationAnalysisTests(ApiFixture api)
{
    private const string Anchor = "RH-2026-004172";

    [Fact]
    public async Task Anchor_valuation_shows_report_ltv_and_assignment_timeline()
    {
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        var (s, body) = await fahad.GetAsync($"/api/cases/{Anchor}/valuation");
        Assert.Equal(HttpStatusCode.OK, s);
        var current = body!["current"]!;
        Assert.Equal(1_650_000.00m, current["marketValue"]!.GetValue<decimal>());
        Assert.Equal("1.58M – 1.72M", TestClient.Str(current["range"], "label"));
        Assert.Equal("مكتب تقييم معتمد «ب»", TestClient.Str(current, "valuer"));
        Assert.Equal(90, current["validityDays"]!.GetValue<int>());
        Assert.Equal(77.9m, body["ltv"]!["value"]!.GetValue<decimal>());
        var timeline = body["assignment"]!["timeline"]!.AsArray();
        Assert.Contains(timeline, t => TestClient.Str(t, "title") == "إنشاء التكليف");
        Assert.Contains(timeline, t => TestClient.Str(t, "title") == "المعاينة بحضور المالك");
        Assert.Contains(timeline, t => TestClient.Str(t, "type") == "assignment.access_expired");
        Assert.True(body["assignment"]!["providerAccess"]!["expired"]!.GetValue<bool>());

        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await maha.GetAsync($"/api/cases/{Anchor}/valuation")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await maha.GetAsync($"/api/cases/{Anchor}/analysis")).Status);
    }

    [Fact]
    public async Task Revaluation_is_refused_while_the_report_is_valid_unless_a_reason_is_documented()
    {
        var r = await B3Scenarios.AtValuationAsync(api, validDays: 60);
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        var (_, before) = await fahad.GetAsync($"/api/cases/{r}/valuation");
        Assert.False(before!["revaluation"]!["eligible"]!.GetValue<bool>());
        Assert.Equal("معطّل: التقرير الحالي صالح. يُتاح قبل 14 يوماً من الانتهاء أو بسبب موثق.", TestClient.Str(before["revaluation"], "disabledReason"));

        var (s409, refused) = await fahad.PostAsync($"/api/cases/{r}/valuation/revaluation-requests", new { });
        Assert.Equal(HttpStatusCode.Conflict, s409);
        Assert.Equal("revaluation_not_eligible", TestClient.Str(refused, "code"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "valuation.revaluation_blocked" && e.Blocked)));

        var (sOk, ok) = await fahad.PostAsync($"/api/cases/{r}/valuation/revaluation-requests", new { reason = "توسعة مرخصة أُضيفت للعقار بعد المعاينة وتغيّر القيمة جوهرياً." });
        Assert.True(sOk == HttpStatusCode.OK, ok?.ToJsonString());
        Assert.False(ok!["eligibleByDate"]!.GetValue<bool>());
        var task = await api.WithDbAsync(db => db.Tasks.FirstAsync(t => t.Id == Guid.Parse(TestClient.Str(ok, "taskId"))));
        Assert.Equal("revaluation", task.Kind);
        Assert.Equal(TaskStatus.Open, task.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await fahad.PostAsync($"/api/cases/{r}/valuation/revaluation-requests", new { reason = "طلب ثانٍ لنفس الحالة بسبب موثق آخر." })).Status);

        // Within 14 days of expiry no reason is needed.
        var near = await B3Scenarios.AtValuationAsync(api, validDays: 10);
        var (sNear, _) = await fahad.PostAsync($"/api/cases/{near}/valuation/revaluation-requests", new { });
        Assert.Equal(HttpStatusCode.OK, sNear);

        // Role without valuation.assign (finance) cannot request it.
        var reem = await api.LoginAsync("r.aldosari@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await reem.PostAsync($"/api/cases/{near}/valuation/revaluation-requests", new { reason = "سبب موثق طويل بما يكفي." })).Status);
    }

    [Fact]
    public async Task Accepting_a_report_keeps_a_single_accepted_current_report()
    {
        var r = await B3Scenarios.AtValuationAsync(api, validDays: 30);
        var newId = await B3Scenarios.AddReportAsync(api, r, ValuationStatus.UnderReview, 90, 1_100_000m, version: 2);

        var sara = await B3Scenarios.SaraAsync(api); // case manager: no valuation.review
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PostAsync($"/api/cases/{r}/valuation/{newId}/review", new { decision = "accept" })).Status);
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await fahad.PostAsync($"/api/cases/{r}/valuation/{newId}/review", new { decision = "return" })).Status);

        var (s, res) = await fahad.PostAsync($"/api/cases/{r}/valuation/{newId}/review", new { decision = "accept", note = "مطابق للمعاينة والمقارنات." });
        Assert.True(s == HttpStatusCode.OK, res?.ToJsonString());
        Assert.Equal(1, res!["superseded"]!.GetValue<int>());
        var statuses = await api.WithDbAsync(db => db.ValuationReports.Where(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r)).Select(x => x.Status).ToListAsync());
        Assert.Single(statuses, x => x == ValuationStatus.Accepted);
        Assert.Contains(ValuationStatus.Superseded, statuses);
        Assert.Equal(HttpStatusCode.Conflict, (await fahad.PostAsync($"/api/cases/{r}/valuation/{newId}/review", new { decision = "accept" })).Status);

        var (_, val) = await fahad.GetAsync($"/api/cases/{r}/valuation");
        Assert.Equal(1_100_000m, val!["current"]!["marketValue"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Anchor_analysis_computes_dsr_on_the_server()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (s, body) = await sara.GetAsync($"/api/cases/{Anchor}/analysis");
        Assert.Equal(HttpStatusCode.OK, s);
        var aff = body!["affordability"]!;
        Assert.Equal(32_400.00m, aff["netMonthlyIncome"]!.GetValue<decimal>());
        Assert.Equal("كشف الراتب لآخر 3 أشهر v2", TestClient.Str(aff["incomeSource"], "label"));
        Assert.Equal(0.4251m, aff["currentDsr"]!.GetValue<decimal>());
        Assert.Equal(0.4653m, aff["proposed"]!["dsr"]!.GetValue<decimal>());
        Assert.Equal(0.55m, aff["dsrLimit"]!.GetValue<decimal>());
        Assert.Equal(2_100.00m, aff["otherObligations"]!.GetValue<decimal>());
        Assert.Equal(3, body["circumstanceIndicators"]!.AsArray().Count);
        Assert.Contains(body["options"]!.AsArray(), o => TestClient.Str(o, "title") == "بيع طوعي" && !o!["feasible"]!.GetValue<bool>());
        Assert.False(body["canEdit"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Analysis_uses_verified_income_only_and_detects_concurrent_edits()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var pending = await B3Scenarios.AddDocumentVersionAsync(api, r, "salary_statement", ReviewStatus.Pending);
        var verified = await B3Scenarios.AddDocumentVersionAsync(api, r, "salary_statement", ReviewStatus.Verified);

        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PutAsync($"/api/cases/{r}/analysis", new { netMonthlyIncome = 20000 })).Status);
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await fahad.PutAsync($"/api/cases/{r}/analysis", new { netMonthlyIncome = 20000 })).Status);
        var (sUnverified, unverified) = await fahad.PutAsync($"/api/cases/{r}/analysis", new { netMonthlyIncome = 20000, incomeDocumentVersionId = pending });
        Assert.Equal(HttpStatusCode.BadRequest, sUnverified);
        Assert.NotNull(unverified!["errors"]!["incomeDocumentVersionId"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await fahad.PutAsync($"/api/cases/{r}/analysis", new { completed = true })).Status);

        var (s1, first) = await fahad.PutAsync($"/api/cases/{r}/analysis", new
        {
            netMonthlyIncome = 20000, incomeDocumentVersionId = verified, otherObligations = 1500,
            circumstanceIndicators = new[] { new { icon = "home", text = "العقار سكن رئيسي للأسرة" } },
            options = new[] { new { kind = "reschedule", title = "إعادة جدولة", feasible = true, note = "ضمن الحد" } },
        });
        Assert.True(s1 == HttpStatusCode.OK, first?.ToJsonString());
        var v1 = first!["version"]!.GetValue<uint>();
        Assert.Equal(0.325m, first["affordability"]!["currentDsr"]!.GetValue<decimal>()); // 6,500 ÷ 20,000

        var (s2, second) = await fahad.PutAsync($"/api/cases/{r}/analysis", new { version = v1, completed = true });
        Assert.True(s2 == HttpStatusCode.OK, second?.ToJsonString());
        Assert.True(second!["completed"]!.GetValue<bool>());
        Assert.NotEqual(v1, second["version"]!.GetValue<uint>());

        // Stale version (opened before the last save) → 409.
        var (sStale, stale) = await fahad.PutAsync($"/api/cases/{r}/analysis", new { version = v1, otherObligations = 0 });
        Assert.Equal(HttpStatusCode.Conflict, sStale);
        Assert.Equal("concurrency", TestClient.Str(stale, "code"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "analysis.completed")));
    }
}
