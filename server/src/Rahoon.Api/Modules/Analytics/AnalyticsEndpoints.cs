using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Analytics;

public sealed record InsightFeedbackRequest(string InsightKey, string Value, string? Reason);

/// <summary>
/// O01 portfolio analysis and O02 trends & bottlenecks, computed live from the tenant's own cases.
/// Aggregates only; groups under 10 cases are merged into «أخرى». Nothing here changes any case.
/// </summary>
public static class AnalyticsEndpoints
{
    public const int MinCell = 10;
    public const string BottleneckModel = "BNK-rule-v1 (قاعدة ثابتة — ليس نموذجاً إحصائياً)";

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/analytics").RequireOrg(OrganizationKind.Lender).RequirePermission(P.AnalyticsView);
        g.MapGet("/portfolio", Portfolio);
        g.MapGet("/bottlenecks", Bottlenecks);
        g.MapPost("/insights/feedback", Feedback).Idempotent();
    }

    private sealed record Row(Guid Id, CaseStatus Status, string? Region, DateOnly? OpenedOn, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt,
        DateTimeOffset StatusChangedAt, decimal? Outstanding, int? ContractYear);

    private static string Outcome(Row r, HashSet<string> path) => r.Status switch
    {
        CaseStatus.Cancelled => "cancelled",
        CaseStatus.Closed when path.Contains("judicial_referral") || path.Contains("external_judicial_sale") => "judicial",
        CaseStatus.Closed when path.Contains("voluntary_sale") => "voluntary_sale",
        CaseStatus.Closed when path.Contains("active_settlement") => "amicable",
        CaseStatus.Closed => "other_closed",
        _ => "open",
    };

    private static readonly (string Key, string Label)[] Outcomes =
    [
        ("amicable", "تسوية ودية"), ("other_closed", "سداد كامل أو إغلاق آخر"), ("voluntary_sale", "بيع طوعي"), ("judicial", "إحالة قضائية"),
        ("cancelled", "أُلغيت"), ("open", "ما زالت مفتوحة"),
    ];

    private static async Task<IResult> Portfolio(int? months, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var m = Math.Clamp(months ?? 12, 1, 36);
        var now = clock.UtcNow;
        var since = now.AddMonths(-m);
        var org = rc.OrganizationId!.Value;
        // One base for every figure: all non-draft cases that were open at some point in the window
        // (still open now, or closed/cancelled within the window).
        var rows = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == org && c.Status != CaseStatus.Draft
                && ((c.Status != CaseStatus.Closed && c.Status != CaseStatus.Cancelled) || c.StatusChangedAt >= since))
            .Select(c => new Row(c.Id, c.Status, c.Region, c.OpenedOn, c.CreatedAt, c.ClosedAt, c.StatusChangedAt, c.OutstandingAmount,
                db.FinancingContracts.Where(f => f.CaseId == c.Id).Select(f => f.ContractDate.HasValue ? (int?)f.ContractDate.Value.Year : null).FirstOrDefault()))
            .ToListAsync();
        var ids = rows.Select(r => r.Id).ToList();
        string[] pathStates = ["active_settlement", "voluntary_sale", "judicial_referral", "external_judicial_sale"];
        var pathEvents = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId != null && ids.Contains(e.CaseId.Value) && e.Type == "case.transition" && !e.Blocked && pathStates.Contains(e.ToState!))
            .Select(e => new { CaseId = e.CaseId!.Value, e.ToState }).ToListAsync();
        var paths = pathEvents.GroupBy(e => e.CaseId).ToDictionary(g => g.Key, g => g.Select(x => x.ToState!).ToHashSet());
        HashSet<string> PathOf(Row r)
        {
            var p = paths.GetValueOrDefault(r.Id) ?? [];
            // Current status is part of the path too (bulk-imported history may lack transition events).
            if (r.Status is CaseStatus.ActiveSettlement or CaseStatus.VoluntarySale or CaseStatus.JudicialReferral or CaseStatus.ExternalJudicialSale) p = [.. p, CaseStatusInfo.Key(r.Status)];
            return p;
        }
        var classified = rows.Select(r => (Row: r, Outcome: Outcome(r, PathOf(r)), Referred: PathOf(r).Overlaps(["judicial_referral", "external_judicial_sale"]))).ToList();
        var recon = await db.Reconciliations.AsNoTracking().Where(x => ids.Contains(x.CaseId) && x.Status == ReconciliationStatus.Approved)
            .GroupBy(x => x.CaseId).Select(g => new { CaseId = g.Key, Received = g.Sum(x => x.ReceivedAmount) }).ToDictionaryAsync(x => x.CaseId, x => x.Received);

        var n = classified.Count;
        decimal? Pct(int part, int whole) => whole == 0 ? null : Math.Round(part * 100m / whole, 1);
        var closed = classified.Where(x => x.Row.Status == CaseStatus.Closed).ToList();
        var recoveredBase = closed.Where(x => recon.ContainsKey(x.Row.Id) && x.Row.Outstanding is > 0).ToList();
        decimal? recovery = recoveredBase.Count == 0 ? null : Math.Round(recoveredBase.Sum(x => recon[x.Row.Id]) * 100m / recoveredBase.Sum(x => x.Row.Outstanding!.Value), 1);
        double? avgDays = closed.Count == 0 ? null : Math.Round(closed.Average(x => (x.Row.ClosedAt ?? x.Row.StatusChangedAt).Subtract(OpenedAt(x.Row)).TotalDays), 0);

        var outcomes = Outcomes.Select(o => new { key = o.Key, label = o.Label, count = classified.Count(x => x.Outcome == o.Key) }).ToList();
        var regions = classified.GroupBy(x => x.Row.Region ?? "أخرى").Select(g => (Name: g.Key, Items: g.ToList())).ToList();
        var small = regions.Where(r => r.Items.Count < MinCell).SelectMany(r => r.Items).ToList();
        var regionRows = regions.Where(r => r.Items.Count >= MinCell && r.Name != "أخرى").Select(r => RegionRow(r.Name, r.Items.Select(i => i.Row).ToList(), recon)).ToList();
        var other = regions.Where(r => r.Name == "أخرى" && r.Items.Count >= MinCell).SelectMany(r => r.Items).Concat(small).ToList();
        if (other.Count > 0) regionRows.Add(RegionRow("أخرى", other.Select(i => i.Row).ToList(), recon));

        var cohorts = classified.GroupBy(x => x.Row.ContractYear).OrderBy(g => g.Key)
            .Select(g => (Year: g.Key, Items: g.ToList())).ToList();
        var cohortRows = cohorts.Where(c => c.Items.Count >= MinCell && c.Year is not null).Select(c => CohortRow(c.Year!.Value.ToString(), c.Items)).ToList();
        var restCohort = cohorts.Where(c => c.Items.Count < MinCell || c.Year is null).SelectMany(c => c.Items).ToList();
        if (restCohort.Count > 0) cohortRows.Add(CohortRow("أخرى", restCohort));

        return Results.Ok(new
        {
            caseCount = n, dataAsOf = now, period = new { months = m, from = since, to = now },
            provenance = $"{n} حالة (مفتوحة خلال آخر {m} شهراً أو أُغلقت/أُلغيت خلالها) · بيانات حية حتى {now.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
            kpis = new object[]
            {
                new { key = "amicable_rate", label = "حالات أُغلقت بحل ودي", value = Pct(classified.Count(x => x.Outcome == "amicable"), n), unit = "%", goodDirection = "up", numerator = classified.Count(x => x.Outcome == "amicable"), denominator = n },
                new { key = "recovery_rate", label = "نسبة الاسترداد", value = recovery, unit = "%", goodDirection = "up", numerator = recoveredBase.Count, denominator = closed.Count,
                    note = recovery is null ? "لا توجد حالات مغلقة بتسوية معتمدة في الفترة." : "المسترد (تسويات معتمدة) ÷ المديونية عند الفتح، للحالات المغلقة من القاعدة نفسها." },
                new { key = "avg_days_to_resolve", label = "متوسط أيام الحل", value = avgDays, unit = "يوم", goodDirection = "down", numerator = closed.Count, denominator = closed.Count },
                new { key = "referral_rate", label = "إحالات", value = Pct(classified.Count(x => x.Referred), n), unit = "%", goodDirection = "down", numerator = classified.Count(x => x.Referred), denominator = n },
            },
            comparison = new { available = false, note = "المقارنة بالفترة السابقة تتطلب لقطات تاريخية للمحفظة؛ لم تُفعّل بعد، ولا تُعرض فروق مقدَّرة." },
            outcomes,
            outcomesAlt = "نتائج الحالات: " + string.Join("، ", outcomes.Select(o => $"{o.label} {o.count}")),
            regions = regionRows,
            regionsNote = $"الاسترداد = المسترد ÷ المديونية عند الفتح للحالات المغلقة · مناطق بأقل من {MinCell} حالات مدمجة في «أخرى» · المحور يبدأ من الصفر.",
            cohorts = cohortRows,
            definitions = new[]
            {
                "القاعدة الموحدة (حل تعارض B11-1): كل النسب تُحسب على المجموعة نفسها من الحالات، وتظهر الحالات المفتوحة فئةً ضمن النتائج كي يكون المجموع = عدد الحالات.",
                "حل ودي = حالة مغلقة مرّت بتسوية نشطة؛ إحالة = حالة دخلت الإحالة القضائية في أي وقت.",
                "متوسط أيام الحل = من الفتح إلى الإغلاق للحالات المغلقة في القاعدة.",
            },
        });
    }

    private static DateTimeOffset OpenedAt(Row r) => r.OpenedOn is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)) : r.CreatedAt;

    private static object RegionRow(string name, List<Row> items, Dictionary<Guid, decimal> recon)
    {
        var closed = items.Where(i => i.Status == CaseStatus.Closed && recon.ContainsKey(i.Id) && i.Outstanding is > 0).ToList();
        decimal? rate = closed.Count == 0 ? null : Math.Round(closed.Sum(i => recon[i.Id]) * 100m / closed.Sum(i => i.Outstanding!.Value), 1);
        return new { name, cases = items.Count, recoveryRate = rate, closedWithReconciliation = closed.Count, aria = $"{name}: {(rate is null ? "لا توجد تسويات مغلقة" : $"{rate}%")} من {items.Count} حالة" };
    }

    private static object CohortRow(string year, List<(Row Row, string Outcome, bool Referred)> items)
    {
        var n = items.Count;
        decimal P(int x) => Math.Round(x * 100m / n, 0);
        var closed = items.Where(i => i.Row.Status == CaseStatus.Closed).ToList();
        return new
        {
            year, cases = n,
            amicablePct = P(items.Count(i => i.Outcome == "amicable")), voluntarySalePct = P(items.Count(i => i.Outcome == "voluntary_sale")),
            referralPct = P(items.Count(i => i.Referred)), openPct = P(items.Count(i => i.Outcome == "open")),
            otherPct = P(items.Count(i => i.Outcome is "other_closed" or "cancelled")),
            avgDaysToResolve = closed.Count == 0 ? (double?)null : Math.Round(closed.Average(i => (i.Row.ClosedAt ?? i.Row.StatusChangedAt).Subtract(OpenedAt(i.Row)).TotalDays), 0),
        };
    }

    // ───────── O02 ─────────

    private static readonly CaseStatus[] Stages =
    [
        CaseStatus.AwaitingData, CaseStatus.Verification, CaseStatus.Valuation, CaseStatus.ProposedSolution, CaseStatus.InternalApproval,
        CaseStatus.AwaitingCustomer, CaseStatus.Negotiation, CaseStatus.AwaitingReconciliation,
    ];

    private static double Median(IReadOnlyList<double> xs)
    {
        if (xs.Count == 0) return 0;
        var s = xs.OrderBy(x => x).ToList();
        return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2;
    }

    private static async Task<IResult> Bottlenecks(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var org = rc.OrganizationId!.Value;
        var today = clock.TodayRiyadh;
        var now = clock.UtcNow;
        var open = await db.Cases.AsNoTracking().Where(c => c.OrganizationId == org && Stages.Contains(c.Status))
            .Select(c => new { c.Id, c.Status, c.StatusChangedAt, c.StageDueOn, c.AssignedManagerId, c.SlaPausedAt }).ToListAsync();
        var sla = await db.SlaRules.AsNoTracking().Where(r => r.OrganizationId == org).ToDictionaryAsync(r => r.Status, r => r.BusinessDays);
        var teamOf = await db.Memberships.AsNoTracking().Where(m => m.OrganizationId == org)
            .Select(m => new { m.Id, Team = m.Team == null ? null : m.Team.NameAr }).ToDictionaryAsync(m => m.Id, m => m.Team ?? "بلا فريق");

        // Completed stage durations from the transition log (last 12 months).
        var since = now.AddMonths(-12);
        var transitions = await db.AuditEvents.AsNoTracking().Where(e => e.OrganizationId == org && e.Type == "case.transition" && !e.Blocked && e.CaseId != null && e.OccurredAt >= since.AddMonths(-3))
            .OrderBy(e => e.Seq).Select(e => new { CaseId = e.CaseId!.Value, e.FromState, e.ToState, e.OccurredAt }).ToListAsync();
        var completed = new List<(string Stage, DateTimeOffset Left, double Days)>();
        foreach (var g in transitions.GroupBy(t => t.CaseId))
        {
            var list = g.OrderBy(t => t.OccurredAt).ToList();
            for (var i = 1; i < list.Count; i++)
                if (list[i].FromState == list[i - 1].ToState && list[i].OccurredAt >= since)
                    completed.Add((list[i].FromState!, list[i].OccurredAt, BusinessDays.Between(DateOnly.FromDateTime(list[i - 1].OccurredAt.UtcDateTime), DateOnly.FromDateTime(list[i].OccurredAt.UtcDateTime))));
        }

        var stages = Stages.Select(s =>
        {
            var key = CaseStatusInfo.Key(s);
            var items = open.Where(c => c.Status == s).ToList();
            var ages = items.Select(c => (double)BusinessDays.Between(DateOnly.FromDateTime(c.StatusChangedAt.ToOffset(TimeSpan.FromHours(3)).DateTime), today)).ToList();
            var done = completed.Where(x => x.Stage == key).Select(x => x.Days).ToList();
            var limit = sla.TryGetValue(s.ToString(), out var d) ? d : (int?)null;
            var medianAge = Median(ages);
            return new
            {
                key, name = CaseStatusInfo.Of(s).LabelAr, openCount = items.Count, medianAgeBusinessDays = Math.Round(medianAge, 1),
                medianCompletedBusinessDays = done.Count == 0 ? (double?)null : Math.Round(Median(done), 1), completedCount = done.Count,
                overdue = items.Count(c => c.StageDueOn is { } due && due < today && c.SlaPausedAt is null), slaBusinessDays = limit,
                // Data-driven threshold (B11 conflict 3): «فوق المعتاد» = median age of open cases exceeds the stage SLA.
                aboveNormal = limit is { } l && items.Count > 0 && medianAge > l,
            };
        }).ToList();

        var byTeam = open.Where(c => c.StageDueOn is { } due && due < today && c.SlaPausedAt is null)
            .GroupBy(c => c.AssignedManagerId is { } mid ? teamOf.GetValueOrDefault(mid) ?? "بلا فريق" : "بلا فريق")
            .Select(g => new { team = g.Key, overdue = g.Count(), byStage = g.GroupBy(x => CaseStatusInfo.Of(x.Status).LabelAr).Select(x => new { stage = x.Key, count = x.Count() }) })
            .OrderByDescending(x => x.overdue).ToList();

        var trend = Enumerable.Range(0, 12).Select(i =>
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(-11 + i);
            var vals = completed.Where(x => x.Stage == "valuation" && x.Left.ToOffset(TimeSpan.FromHours(3)).Year == month.Year && x.Left.ToOffset(TimeSpan.FromHours(3)).Month == month.Month).Select(x => x.Days).ToList();
            return new { month = month.ToString("yyyy-MM"), medianDays = vals.Count == 0 ? (double?)null : Math.Round(Median(vals), 1), count = vals.Count };
        }).ToList();

        // Decision-support note: a fixed, labelled rule — not a model and not a causal claim.
        var hot = stages.Where(s => s.aboveNormal && s.slaBusinessDays is > 0).OrderByDescending(s => s.medianAgeBusinessDays / s.slaBusinessDays!.Value).FirstOrDefault();
        object? insight = null;
        if (hot is not null)
        {
            var insightKey = $"bottleneck:{hot.key}:{today:yyyy-MM}";
            var feedback = await db.Set<InsightFeedback>().AsNoTracking().Where(f => f.OrganizationId == org && f.InsightKey == insightKey)
                .GroupBy(f => f.Value).Select(g => new { value = g.Key, count = g.Count() }).ToListAsync();
            insight = new
            {
                insightKey, eyebrow = "ملاحظة تحليلية · دعم قرار", kind = "heuristic_insight", modelVersion = BottleneckModel,
                title = $"«{hot.name}» أكبر اختناق حالياً",
                body = $"وسيط عمر الحالات المفتوحة في «{hot.name}» {hot.medianAgeBusinessDays} يوم عمل مقابل مهلة {hot.slaBusinessDays} أيام، و{hot.overdue} حالة متأخرة.",
                source = $"الحالات المفتوحة ({hot.openCount}) وسجل الانتقالات · حتى {now.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}",
                confidence = hot.openCount >= MinCell ? "متوسطة (قاعدة بسيطة على عدد كافٍ)" : "منخفضة (عدد حالات قليل)",
                causalityNote = "ليست سبباً مؤكداً: مقارنة بالمهلة فقط، والسبب يحتاج تأكيداً بشرياً.",
                limitations = "لا يفرّق بين وقت العمل ووقت الانتظار (البيانات لا تسجّل سبب الانتظار بعد)؛ لا يأخذ الإيقاف المؤقت التاريخي في الحسبان.",
                affectedCases = $"/cases?status={hot.key}&overdue=1",
                feedback,
            };
        }

        return Results.Ok(new
        {
            dataAsOf = now, stages, overdueByTeam = byTeam, valuationTrend = trend,
            trendAlt = "وسيط أيام التقييم شهرياً: " + string.Join("، ", trend.Select(t => t.medianDays?.ToString() ?? "—")),
            workWaitNote = "تقسيم «عمل مقابل انتظار» يتطلب تسجيل سبب الانتظار لكل مرحلة؛ غير متاح بعد، فيُعرض عمر المرحلة فقط.",
            insight,
            rules = "الإجراءات: فتح الحالات المتأثرة فقط؛ لا تغيير آلي في العمليات.",
        });
    }

    private static async Task<IResult> Feedback(InsightFeedbackRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.InsightKey) && req.InsightKey.Length <= 200, "insightKey", "مرجع الملاحظة مطلوب.")
            .Require(req.Value is "useful" or "not_useful", "value", "اختر: مفيدة أو غير مفيدة.")
            .Require(req.Value != "not_useful" || !string.IsNullOrWhiteSpace(req.Reason), "reason", "اذكر لماذا لم تكن الملاحظة مفيدة.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Set<InsightFeedback>().Add(new InsightFeedback
        {
            OrganizationId = rc.OrganizationId!.Value, InsightKey = req.InsightKey, ModelVersion = BottleneckModel, Value = req.Value, Reason = req.Reason?.Trim(),
            UserId = rc.UserId, At = clock.UtcNow,
        });
        await audit.RecordAsync(new AuditEntry("analytics.insight_feedback", req.Value == "useful" ? "ملاحظة تحليلية مفيدة" : "ملاحظة تحليلية غير مفيدة",
            Reason: req.Reason?.Trim(), Detail: $"{req.InsightKey} · {(req.Value == "useful" ? HumanReview.Agree : HumanReview.NotUsed)}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { recorded = true });
    }
}
