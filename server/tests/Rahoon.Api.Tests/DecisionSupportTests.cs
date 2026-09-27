using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Analytics;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>B11 O01–O05 and B10 PA18: decision support never decides, never reaches the owner, never moves a case.</summary>
[Collection(ApiCollection.Name)]
public sealed class DecisionSupportTests(ApiFixture api)
{
    private const string Analyst = "f.alotaibi@alufuq.example";
    private const string Admin = "l.alghamdi@alufuq.example";
    private const string Compliance = "h.almutairi@alufuq.example";

    [Fact]
    public async Task Prediction_needs_an_override_reason_never_changes_the_case_and_is_hidden_from_the_owner()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var fahad = await api.LoginAsync(Analyst);
        var before = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt, c.StageDueOn }).FirstAsync());
        var transitionsBefore = await api.WithDbAsync(db => db.AuditEvents.CountAsync(e => e.CaseReference == r && e.Type.StartsWith("case.transition")));

        var (s, p) = await fahad.PostAsync($"/api/cases/{r}/predictions");
        Assert.True(s == HttpStatusCode.OK, p?.ToJsonString());
        Assert.Equal("ADH-v1.2 heuristic", TestClient.Str(p, "modelVersion"));
        Assert.NotEmpty(p!["inputs"]!.AsArray());
        Assert.NotEmpty(TestClient.Str(p, "limitations"));
        Assert.StartsWith("heuristic_", TestClient.Str(p, "confidence"));
        Assert.DoesNotContain("accuracy", p.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        var id = TestClient.Str(p, "id");

        // Attaching to an approval requires an opinion first.
        Assert.Equal("opinion_required", TestClient.Str((await fahad.PostAsync($"/api/cases/{r}/predictions/{id}/attach-to-approval")).Body, "code"));
        // Override without a reason is refused.
        var (sNo, no) = await fahad.PostAsync($"/api/cases/{r}/predictions/{id}/opinion", new { value = "override", overrideDirection = "overestimates" });
        Assert.Equal(HttpStatusCode.BadRequest, sNo);
        Assert.NotNull(no!["errors"]!["reason"]);
        var (sOk, ok) = await fahad.PostAsync($"/api/cases/{r}/predictions/{id}/opinion",
            new { value = "override", overrideDirection = "overestimates", reason = "جهة العمل الجديدة في فترة تجربة؛ لا يظهر ذلك في البيانات." });
        Assert.True(sOk == HttpStatusCode.OK, ok?.ToJsonString());
        Assert.Equal("override", TestClient.Str(ok!["opinion"], "value"));

        // Never changes state.
        var after = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt, c.StageDueOn }).FirstAsync());
        Assert.Equal(before, after);
        Assert.Equal(transitionsBefore, await api.WithDbAsync(db => db.AuditEvents.CountAsync(e => e.CaseReference == r && e.Type.StartsWith("case.transition"))));

        // Never exposed to the owner: no owner endpoint carries the model, the estimate or its id.
        foreach (var path in new[] { "/api/owner/home", "/api/owner/journey", "/api/owner/documents", "/api/owner/debt", "/api/owner/options", "/api/owner/agreement",
                     "/api/owner/payments", "/api/owner/messages", "/api/owner/complaints", "/api/owner/closure", "/api/owner/referral" })
        {
            var (os, body) = await owner.GetAsync(path);
            Assert.True(os is HttpStatusCode.OK or HttpStatusCode.NotFound or HttpStatusCode.Conflict, $"{path}: {os}");
            var json = body?.ToJsonString() ?? "";
            Assert.DoesNotContain("ADH", json);
            Assert.DoesNotContain(id, json);
            Assert.DoesNotContain("دعم قرار", json);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/cases/{r}/predictions")).Status);
    }

    [Fact]
    public async Task Draft_is_a_suggestion_until_a_human_resolves_flags_and_approves_it()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var majed = await api.LoginAsync(ReferralScenarios.Legal);
        var (s, d) = await majed.PostAsync($"/api/cases/{r}/drafts", new { kind = "agreement_reschedule" });
        Assert.True(s == HttpStatusCode.OK, d?.ToJsonString());
        Assert.True(d!["isSuggestion"]!.GetValue<bool>());
        Assert.Equal(1, d["unresolvedFlags"]!.GetValue<int>());
        Assert.Contains("دون نموذج لغوي", TestClient.Str(d["ai"], "modelVersion"));
        var id = TestClient.Str(d, "id");

        Assert.Equal("draft_not_approved", TestClient.Str((await majed.PostAsync($"/api/cases/{r}/drafts/{id}/attach", new { target = "الاتفاق" })).Body, "code"));
        await majed.StepUpAsync();
        var (sFlag, flagged) = await majed.PostAsync($"/api/cases/{r}/drafts/{id}/approve", new { reason = "راجعت النص." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sFlag);
        Assert.Equal("unresolved_flags", TestClient.Str(flagged, "code"));

        var edited = string.Join("\n\n", TestClient.Str(d, "body").Split("\n\n").Where(p => !p.StartsWith("البند 7")));
        Assert.Equal(HttpStatusCode.OK, (await majed.PutAsync($"/api/cases/{r}/drafts/{id}", new { body = edited, note = "حذف البند 7 المتعارض" })).Status);
        var (sA, approved) = await majed.PostAsync($"/api/cases/{r}/drafts/{id}/approve", new { reason = "حُذف البند المتعارض وطابقت القيم مصادرها." });
        Assert.True(sA == HttpStatusCode.OK, approved?.ToJsonString());
        Assert.Equal("override", TestClient.Str(approved, "humanReview"));
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/drafts/{id}/attach", new { target = "مسودة الاتفاق" })).Status);
    }

    [Fact]
    public async Task Operational_setting_changes_need_a_second_person_and_respect_limits()
    {
        var layla = await api.LoginAsync(Admin);
        var tooMany = new { steps = new[] { new { day = 0, action = "إرسال العرض" } }, maxPerWeek = 3, respectContactWindows = true, suppressDuringOpenComplaint = true };
        var (sBad, bad) = await layla.PostAsync("/api/settings/operations/change-requests", new { key = OperationalSettings.ReminderCadence, value = tooMany, reason = "زيادة عدد التذكيرات الأسبوعية." });
        Assert.Equal(HttpStatusCode.BadRequest, sBad);
        Assert.NotNull(bad!["errors"]!["value"]);

        var ok = new { steps = new[] { new { day = 0, action = "إرسال العرض" }, new { day = 4, action = "تذكير لطيف" } }, maxPerWeek = 2, respectContactWindows = true, suppressDuringOpenComplaint = true };
        var (sP, proposed) = await layla.PostAsync("/api/settings/operations/change-requests", new { key = OperationalSettings.ReminderCadence, value = ok, reason = "تبسيط الإيقاع إلى تذكيرين." });
        Assert.True(sP == HttpStatusCode.OK, proposed?.ToJsonString());
        var id = TestClient.Str(proposed, "id");
        var (sSelf, self) = await layla.PostAsync($"/api/settings/operations/change-requests/{id}/decision", new { decision = "approve", reason = "أعتمد اقتراحي." });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);
        Assert.Equal("separation_of_duties", TestClient.Str(self, "code"));
        var hind = await api.LoginAsync(Compliance);
        var (sOk, decided) = await hind.PostAsync($"/api/settings/operations/change-requests/{id}/decision", new { decision = "approve", reason = "ضمن حدود الامتثال." });
        Assert.True(sOk == HttpStatusCode.OK, decided?.ToJsonString());
        Assert.Equal("Effective", TestClient.Str(decided, "status"));
        var (_, settings) = await layla.GetAsync("/api/settings/operations");
        var cadence = settings!["settings"]!.AsArray().First(x => TestClient.Str(x, "key") == OperationalSettings.ReminderCadence)!;
        Assert.Equal(decided!["version"]!.GetValue<int>(), cadence["effective"]!["version"]!.GetValue<int>());
    }

    [Fact]
    public async Task Integration_cannot_be_enabled_without_a_configured_adapter()
    {
        var ahmad = await api.LoginAsync("a.almutairi@rahoon.example");
        var (sList, list) = await ahmad.GetAsync("/api/platform/integrations");
        Assert.Equal(HttpStatusCode.OK, sList);
        Assert.Contains(list!["items"]!.AsArray(), i => TestClient.Str(i, "key") == "judicial_channel" && TestClient.Str(i, "state") == "unavailable");

        var (s, body) = await ahmad.PutAsync("/api/platform/integrations/judicial_channel", new { state = "enabled", note = "تفعيل القناة القضائية." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Equal("adapter_not_configured", TestClient.Str(body, "code"));
        Assert.Equal("unavailable", await api.WithDbAsync(db => db.IntegrationSettings.Where(i => i.Key == "judicial_channel").Select(i => i.State).FirstAsync()));

        var (sP, pending) = await ahmad.PutAsync("/api/platform/integrations/judicial_channel", new { state = "pending", note = "بانتظار اعتماد القناة من الجهة." });
        Assert.True(sP == HttpStatusCode.OK, pending?.ToJsonString());
        var (sU, _) = await ahmad.PutAsync("/api/platform/integrations/judicial_channel", new { state = "unavailable", note = "لا قناة معتمدة؛ إدخال يدوي." });
        Assert.Equal(HttpStatusCode.OK, sU);

        // Institution view is read-only and tenant staff cannot change states.
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        Assert.Equal(HttpStatusCode.OK, (await sara.GetAsync("/api/settings/integrations")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PutAsync("/api/platform/integrations/sms_gateway", new { state = "failed", note = "محاولة من منشأة." })).Status);
    }

    [Fact]
    public async Task Portfolio_analysis_uses_one_base_and_bottlenecks_label_heuristics()
    {
        var layla = await api.LoginAsync(Admin);
        var (s, p) = await layla.GetAsync("/api/analytics/portfolio");
        Assert.Equal(HttpStatusCode.OK, s);
        var n = p!["caseCount"]!.GetValue<int>();
        Assert.Equal(n, p["outcomes"]!.AsArray().Sum(o => o!["count"]!.GetValue<int>()));
        Assert.All(p["kpis"]!.AsArray().Where(k => TestClient.Str(k, "key") is "amicable_rate" or "referral_rate"), k => Assert.Equal(n, k!["denominator"]!.GetValue<int>()));
        Assert.All(p["regions"]!.AsArray(), g => Assert.True(g!["cases"]!.GetValue<int>() >= AnalyticsEndpoints.MinCell || TestClient.Str(g, "name") == "أخرى"));

        var (sb, b) = await layla.GetAsync("/api/analytics/bottlenecks");
        Assert.Equal(HttpStatusCode.OK, sb);
        Assert.NotEmpty(b!["stages"]!.AsArray());
        if (b["insight"] is { } insight)
        {
            Assert.Equal("heuristic_insight", TestClient.Str(insight, "kind"));
            Assert.Contains("ليس", TestClient.Str(insight, "modelVersion"));
            var (sf, _) = await layla.PostAsync("/api/analytics/insights/feedback", new { insightKey = TestClient.Str(insight, "insightKey"), value = "not_useful" });
            Assert.Equal(HttpStatusCode.BadRequest, sf); // «غير مفيدة…» needs a reason
        }
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.GetAsync("/api/analytics/portfolio")).Status); // case manager lacks analytics.view
    }
}

public sealed class WaterfallUnitTests
{
    [Fact]
    public void Canon_referral_waterfall()
    {
        var w = Waterfall.Compute(760_000m, 22_800m, 737_200m, 684_200m);
        Assert.Equal(737_200m, w.ExpectedNet);
        Assert.Equal(0m, w.Difference);
        Assert.Equal(684_200m, w.LenderShare);
        Assert.Equal(53_000m, w.OwnerSurplus);
        Assert.Equal(0m, w.Shortfall);
        Assert.Equal(w.ExpectedNet, w.LenderShare + w.OtherFees + w.OwnerSurplus);
    }

    [Fact]
    public void Debt_first_then_fees_then_surplus_and_shortfall_never_touches_surplus()
    {
        var fees = Waterfall.Compute(760_000m, 22_800m, 737_200m, 684_200m, [new FeeInput("رسم تقييم", 3_000m, "قرار 1")]);
        Assert.Equal(684_200m, fees.LenderShare);
        Assert.Equal(3_000m, fees.OtherFees);
        Assert.Equal(50_000m, fees.OwnerSurplus);

        var shortfall = Waterfall.Compute(500_000m, 20_000m, 480_000m, 684_200m);
        Assert.Equal(480_000m, shortfall.LenderShare);
        Assert.Equal(0m, shortfall.OwnerSurplus);
        Assert.Equal(204_200m, shortfall.Shortfall);

        var unbalanced = Waterfall.Compute(760_000m, 22_800m, 736_200m, 684_200m);
        Assert.False(unbalanced.Balanced);
        Assert.Equal(-1_000m, unbalanced.Difference);
    }

    [Fact]
    public void Adherence_heuristic_is_deterministic_and_bounded()
    {
        var x = new AdherenceInputs(0.465m, 0.55m, 7, 11, 4, true, true);
        var a = AdherenceHeuristic.Compute(x);
        var b = AdherenceHeuristic.Compute(x);
        Assert.Equal(a.Point, b.Point);
        Assert.InRange(a.Point, 5m, 95m);
        Assert.True(a.Low <= a.Point && a.Point <= a.High);
        Assert.Equal("heuristic_medium", a.Confidence);
        Assert.Equal("heuristic_low", AdherenceHeuristic.Compute(x with { Dsr = null }).Confidence);
    }
}
