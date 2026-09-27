using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Modules.Analytics;
using Rahoon.Api.Modules.Cases;

namespace Rahoon.Api.Seed;

/// <summary>
/// B11: effective operational settings (v1 = platform defaults) for both lenders, one pending change awaiting a second
/// person, and one ADH-v1.2 heuristic estimate on the anchor case with the analyst's override (O05 design state).
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedAnalyticsAsync()
    {
        foreach (var code in new[] { "alufuq", "sunbula" })
        {
            var org = _orgs[code];
            foreach (var def in OperationalSettings.Catalog)
                db.Set<OperationalSetting>().Add(new OperationalSetting
                {
                    OrganizationId = org.Id, Key = def.Key, VersionNo = 1, ValueJson = def.DefaultJson, Status = SettingChangeStatus.Effective,
                    ProposedByUserId = UserId(code == "alufuq" ? "layla" : "maha"), ProposedAt = DemoToday.AddDays(-84), Reason = "الإعداد الأولي بقيم المنصة الافتراضية",
                    DecidedAt = DemoToday.AddDays(-84), DecisionReason = "قيم افتراضية معتمدة عند التفعيل",
                });
        }
        db.Set<OperationalSetting>().Add(new OperationalSetting
        {
            OrganizationId = _orgs["alufuq"].Id, Key = OperationalSettings.TeamCapacity, VersionNo = 2, Status = SettingChangeStatus.PendingApproval,
            ValueJson = """[{"team":"التحصيل — الرياض","max":140},{"team":"التحصيل — جدة","max":110},{"team":"المخاطر","max":30}]""",
            ProposedByUserId = UserId("layla"), ProposedAt = DemoToday.AddDays(-1), Reason = "رفع سعة فريق جدة إلى 110 بعد انضمام موظفَين جديدين.",
        });

        // O05 on the anchor case: solution v2, analyst override (design state), computed by the same heuristic as the API.
        var c = _cases["RH-2026-004172"];
        var v2 = await db.Solutions.FirstAsync(s => s.CaseId == c.Id && s.VersionNo == 2);
        var history = await db.InstallmentHistory.Where(h => h.CaseId == c.Id && h.Status != InstallmentHistoryStatus.Due).ToListAsync();
        var paid = history.Count(h => h.Status == InstallmentHistoryStatus.Paid);
        var est = AdherenceHeuristic.Compute(new AdherenceInputs(v2.Dsr, v2.DsrLimit, c.ArrearsInstallments ?? 0, history.Count, paid, true, true));
        var p = new Prediction
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, SubjectRef = "solution:v2", ModelId = AdherenceHeuristic.ModelId, ModelVersion = AdherenceHeuristic.Version,
            Question = "درجة الالتزام التقديرية بالحل v2 خلال أول 12 قسطاً (0–100) — ليست احتمالاً معايَراً",
            ScoreLow = est.Low, ScorePoint = est.Point, ScoreHigh = est.High, Confidence = est.Confidence,
            ConfidenceNote = "الثقة قاعدية: تعكس اكتمال المدخلات فقط، لا دقة مُقاسة. لا توجد مقاييس دقة لهذه القاعدة.",
            FactorsJson = JsonSerializer.Serialize(est.Factors, JsonOptions.Web),
            InputsJson = JsonSerializer.Serialize(new[]
            {
                new HeuristicInput("solution", $"v2 · قسط {v2.InstallmentAmount:N2}", "v2", false, v2.PreparedAt),
                new HeuristicInput("net_income", "32,400.00", "كشف الراتب v2", true, At("2026-09-20T00:00:00")),
                new HeuristicInput("installment_history", $"{paid} من {history.Count}", $"{history.Count} شهراً", true, At("2026-09-22T18:40:00")),
                new HeuristicInput("arrears", (c.ArrearsInstallments ?? 0).ToString(), "نظام التمويل الأساسي", true, At("2026-09-22T18:40:00")),
                new HeuristicInput("owner_messages_60d", "نعم", "سجل تواصل الحالة", true, DemoToday),
            }, JsonOptions.Web),
            ExcludedAttributes = [.. AdherenceHeuristic.Excluded], Limitations = AdherenceHeuristic.Limitations, DataAsOf = At("2026-09-22T18:40:00"),
            CreatedByUserId = UserId("fahad"), CreatedAt = At("2026-09-22T16:10:00"),
        };
        db.Set<Prediction>().Add(p);
        db.Set<PredictionOpinion>().Add(new PredictionOpinion
        {
            OrganizationId = c.OrganizationId, PredictionId = p.Id, CaseId = c.Id, UserId = UserId("fahad"), Value = HumanReview.Override, OverrideDirection = "overestimates",
            Reason = "جهة العمل الجديدة في فترة تجربة حتى 2026-12؛ لا يظهر ذلك في البيانات.", At = At("2026-09-22T16:20:00"),
        });
        Audit(c.OrganizationId, c, "decision_support.opinion", "رأي المحلل في التقدير: override", At("2026-09-22T16:20:00"), "fahad",
            reason: "جهة العمل الجديدة في فترة تجربة حتى 2026-12؛ لا يظهر ذلك في البيانات.", detail: $"{AdherenceHeuristic.Version} · solution:v2 · overestimates");
        await db.SaveChangesAsync();
    }
}
