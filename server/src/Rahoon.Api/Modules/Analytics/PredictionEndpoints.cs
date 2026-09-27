using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Analytics;

public sealed record OpinionRequest(string Value, string? OverrideDirection, string? Reason);

public sealed record HeuristicFactor(string Key, string Text, string Direction, decimal Contribution, string EffectLabel);
public sealed record HeuristicInput(string Name, string Value, string Version, bool Verified, DateTimeOffset? AsOf);

public sealed record AdherenceInputs(
    decimal? Dsr, decimal? DsrLimit, int ArrearsInstallments, int HistoryMonths, int HistoryPaidMonths, bool IncomeVerified, bool OwnerResponsive);

public sealed record AdherenceEstimate(decimal Low, decimal Point, decimal High, string Confidence, IReadOnlyList<HeuristicFactor> Factors);

/// <summary>
/// ADH-v1.2 — a transparent, deterministic weighted heuristic (not a trained or calibrated model). The output is an
/// adherence score 0–100, NOT a probability. Protected attributes are never inputs. No accuracy metric exists or is claimed.
/// </summary>
public static class AdherenceHeuristic
{
    public const string ModelId = "ADH";
    public const string Version = "ADH-v1.2 heuristic";
    public static readonly string[] Excluded = ["الجنس", "الجنسية", "العمر", "المنطقة", "الحالة الاجتماعية"];

    public const string Limitations =
        "قاعدة وزنية ثابتة كتبها فريق المنتج؛ لم تُدرَّب ولم تُعايَر على حالات فعلية، ولا توجد لها مقاييس دقة. " +
        "لا تعكس ظروفاً خارج البيانات (مثل فترة تجربة في عمل جديد). أقل موثوقية عندما تنقص المدخلات. لا تُستخدم وحدها في أي قرار.";

    public static AdherenceEstimate Compute(AdherenceInputs x)
    {
        var factors = new List<HeuristicFactor>();
        decimal score = 60m;
        void Add(string key, string text, decimal c)
        {
            score += c;
            factors.Add(new(key, text, c > 0 ? "up" : c < 0 ? "down" : "neutral", c, c > 0 ? "يرفع التقدير" : c < 0 ? "يخفض التقدير" : "أثر محدود"));
        }

        if (x.Dsr is { } dsr)
        {
            var pct = Math.Round(dsr * 100, 1);
            var limit = x.DsrLimit ?? 0.55m;
            var c = dsr <= 0.40m ? 12m : dsr <= 0.50m ? 8m : dsr <= limit ? 2m : -15m;
            Add("dsr", $"الاستقطاع {pct}% {(dsr <= limit ? $"(ضمن حد {limit * 100:0.#}%)" : $"(فوق حد {limit * 100:0.#}%)")}", c);
        }
        if (x.HistoryMonths > 0)
        {
            var ratio = (decimal)x.HistoryPaidMonths / x.HistoryMonths;
            var c = Math.Round((ratio - 0.5m) * 30m, 1);
            Add("history", $"سداد {x.HistoryPaidMonths} من {x.HistoryMonths} أشهر في السجل", c);
        }
        if (x.ArrearsInstallments > 0)
            Add("arrears", $"{x.ArrearsInstallments} أقساط متأخرة حالياً", -Math.Min(x.ArrearsInstallments, 12));
        Add("income_verified", x.IncomeVerified ? "الدخل متحقق منه بمستند" : "الدخل غير متحقق منه", x.IncomeVerified ? 4m : -4m);
        if (x.OwnerResponsive) Add("responsiveness", "تجاوب المالك عبر البوابة خلال آخر 60 يوماً", 3m);
        else Add("responsiveness", "لا تواصل من المالك خلال آخر 60 يوماً", 0m);

        var point = Math.Clamp(Math.Round(score, 0), 5m, 95m);
        var complete = x.Dsr is not null && x.HistoryMonths > 0 && x.IncomeVerified;
        var spread = complete ? 7m : 12m;
        return new AdherenceEstimate(Math.Max(0, point - spread), point, Math.Min(100, point + spread), complete ? "heuristic_medium" : "heuristic_low", factors);
    }
}

/// <summary>
/// O05 decision support. Staff-only; never exposed to owner endpoints; never read by the case workflow or any guard.
/// The analyst's opinion (agree | override with reason | not_used) is mandatory before the estimate can be attached to an approval.
/// </summary>
public static class PredictionEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/predictions").RequireOrg(OrganizationKind.Lender).RequirePermission(P.CaseView)
            .RequireAnyPermission(P.AnalysisEdit, P.AnalyticsView);
        g.MapGet("", List);
        g.MapPost("", Create).RequirePermission(P.AnalysisEdit).Idempotent();
        g.MapPost("/{id:guid}/opinion", Opinion).RequirePermission(P.AnalysisEdit).Idempotent();
        g.MapPost("/{id:guid}/attach-to-approval", AttachToApproval).RequirePermission(P.AnalysisEdit).Idempotent();
        app.MapGet("/api/analytics/models/{version}/governance", Governance).RequireOrg(OrganizationKind.Lender).RequireAnyPermission(P.AnalysisEdit, P.AnalyticsView);
    }

    private static object View(Prediction p, PredictionOpinion? o, string? opinionBy) => new
    {
        p.Id, p.SubjectRef, p.ModelVersion, p.Question, tag = "دعم قرار · غير ملزم",
        range = new { low = p.ScoreLow, point = p.ScorePoint, high = p.ScoreHigh, unit = "درجة 0–100 (ليست احتمالاً)" },
        summary = $"بين {p.ScoreLow:0} و{p.ScoreHigh:0} (الأرجح {p.ScorePoint:0}) · الثقة: {(p.Confidence == "heuristic_medium" ? "متوسطة (قاعدية)" : "منخفضة (قاعدية)")}",
        aria = $"التقدير بين {p.ScoreLow:0} و{p.ScoreHigh:0}، الوسط {p.ScorePoint:0}",
        p.Confidence, p.ConfidenceNote,
        factors = JsonDocument.Parse(p.FactorsJson).RootElement, inputs = JsonDocument.Parse(p.InputsJson).RootElement,
        excluded = p.ExcludedAttributes, p.Limitations, p.DataAsOf, p.CreatedAt,
        opinion = o is null ? null : new { o.Value, o.OverrideDirection, o.Reason, by = opinionBy, o.At },
        note = "التقدير لا يظهر للمالك، ولا يحجب ولا يفتح انتقالاً، ولا يُرفق بطلب الموافقة إلا مع رأيك.",
    };

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var preds = await db.Set<Prediction>().AsNoTracking().Where(p => p.CaseId == c.Id).OrderByDescending(p => p.CreatedAt).Take(10).ToListAsync();
        var ids = preds.Select(p => p.Id).ToList();
        var opinions = await db.Set<PredictionOpinion>().AsNoTracking().Where(o => ids.Contains(o.PredictionId)).OrderByDescending(o => o.At).ToListAsync();
        var users = await db.Users.AsNoTracking().Where(u => opinions.Select(o => o.UserId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return Results.Ok(new
        {
            items = preds.Select(p =>
            {
                var o = opinions.FirstOrDefault(x => x.PredictionId == p.Id);
                return View(p, o, o is null ? null : users.GetValueOrDefault(o.UserId));
            }),
        });
    }

    private static async Task<IResult> Create(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var v = await db.Solutions.AsNoTracking().Where(s => s.CaseId == c.Id && s.Status != SolutionStatus.Rejected && s.Status != SolutionStatus.Superseded)
                    .OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync()
                ?? throw new ConflictException("no_solution", "لا يوجد حل لتقدير الالتزام به.");
        var analysis = await db.Analyses.AsNoTracking().FirstOrDefaultAsync(a => a.CaseId == c.Id);
        var history = await db.InstallmentHistory.AsNoTracking().Where(h => h.CaseId == c.Id && h.Status != InstallmentHistoryStatus.Due).ToListAsync();
        var responsive = await db.Messages.AnyAsync(m => m.CaseId == c.Id && m.AuthorType == "owner" && m.At >= clock.UtcNow.AddDays(-60));
        var net = v.NetIncomeUsed ?? analysis?.NetMonthlyIncome;
        var dsr = v.Dsr ?? (net is > 0 && v.InstallmentAmount > 0 ? Math.Round((v.InstallmentAmount + (analysis?.OtherObligations ?? 0)) / net.Value, 4) : null);
        var paid = history.Count(h => h.Status == InstallmentHistoryStatus.Paid);
        var inputs = new AdherenceInputs(dsr, v.DsrLimit, c.ArrearsInstallments ?? 0, history.Count, paid, analysis?.IncomeVerifiedOn is not null, responsive);
        var est = AdherenceHeuristic.Compute(inputs);

        var inputRows = new List<HeuristicInput>
        {
            new("solution", $"v{v.VersionNo} · قسط {v.InstallmentAmount:N2}", $"v{v.VersionNo}", v.LockedAt is not null, v.LockedAt ?? v.PreparedAt),
            new("net_income", net?.ToString("N2") ?? "—", analysis?.IncomeSourceLabel ?? "—", analysis?.IncomeVerifiedOn is not null,
                analysis?.IncomeVerifiedOn is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)) : null),
            new("installment_history", $"{paid} من {history.Count}", $"{history.Count} شهراً", true, c.OutstandingAsOf),
            new("arrears", (c.ArrearsInstallments ?? 0).ToString(), c.OutstandingSource ?? "—", true, c.OutstandingAsOf),
            new("owner_messages_60d", responsive ? "نعم" : "لا", "سجل تواصل الحالة", true, clock.UtcNow),
        };
        var p = new Prediction
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, SubjectRef = $"solution:v{v.VersionNo}", ModelId = AdherenceHeuristic.ModelId, ModelVersion = AdherenceHeuristic.Version,
            Question = $"درجة الالتزام التقديرية بالحل v{v.VersionNo} خلال أول 12 قسطاً (0–100) — ليست احتمالاً معايَراً",
            ScoreLow = est.Low, ScorePoint = est.Point, ScoreHigh = est.High, Confidence = est.Confidence,
            ConfidenceNote = "الثقة قاعدية: تعكس اكتمال المدخلات فقط، لا دقة مُقاسة. لا توجد مقاييس دقة لهذه القاعدة.",
            FactorsJson = JsonSerializer.Serialize(est.Factors, JsonOptions.Web), InputsJson = JsonSerializer.Serialize(inputRows, JsonOptions.Web),
            ExcludedAttributes = [.. AdherenceHeuristic.Excluded], Limitations = AdherenceHeuristic.Limitations, DataAsOf = clock.UtcNow, CreatedByUserId = rc.UserId,
        };
        db.Set<Prediction>().Add(p);
        await audit.RecordAsync(new AuditEntry("decision_support.generated", $"تقدير دعم قرار ({AdherenceHeuristic.Version}) للحل v{v.VersionNo}", c.Id, c.Reference,
            Detail: $"النطاق {est.Low:0}–{est.High:0} · لا يغيّر حالة الحالة ولا يظهر للمالك", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(View(p, null, null));
    }

    private static async Task<IResult> Opinion(string reference, Guid id, OpinionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(HumanReview.All.Contains(req.Value), "value", "اختر: أتفق، أتجاوزه، أو لا أستخدمه.")
            .Require(req.Value != HumanReview.Override || (!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10), "reason", "سبب التجاوز إلزامي (10 أحرف على الأقل).")
            .Require(req.Value != HumanReview.Override || req.OverrideDirection is "overestimates" or "underestimates", "overrideDirection", "حدد اتجاه التجاوز: التقدير أعلى من الواقع أو أقل.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var p = await db.Set<Prediction>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var o = new PredictionOpinion
        {
            OrganizationId = c.OrganizationId, PredictionId = p.Id, CaseId = c.Id, UserId = rc.UserId, Value = req.Value,
            OverrideDirection = req.Value == HumanReview.Override ? req.OverrideDirection : null, Reason = req.Reason?.Trim(), At = clock.UtcNow,
        };
        db.Set<PredictionOpinion>().Add(o);
        await audit.RecordAsync(new AuditEntry("decision_support.opinion", $"رأي المحلل في التقدير: {req.Value}", c.Id, c.Reference, Reason: o.Reason,
            Detail: $"{p.ModelVersion} · {p.SubjectRef}" + (o.OverrideDirection is null ? "" : $" · {o.OverrideDirection}"), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(View(p, o, rc.UserName));
    }

    private static async Task<IResult> AttachToApproval(string reference, Guid id, CaseAccess access, RahoonDbContext db, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var p = await db.Set<Prediction>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.CaseId == c.Id) ?? throw new NotFoundException();
        var o = await db.Set<PredictionOpinion>().AsNoTracking().Where(x => x.PredictionId == p.Id).OrderByDescending(x => x.At).FirstOrDefaultAsync();
        if (o is null) throw new ConflictException("opinion_required", "سجّل رأيك (أتفق / أتجاوز بسبب / لا أستخدم) قبل إرفاق التقدير.");
        if (o.Value == HumanReview.NotUsed) throw new ConflictException("not_used", "اخترت عدم استخدام التقدير؛ لا يُرفق.");
        var request = await db.ApprovalRequests.FirstOrDefaultAsync(a => a.CaseId == c.Id && a.Status == ApprovalStatus.Pending)
                      ?? throw new ConflictException("no_pending_approval", "لا يوجد طلب موافقة قائم لإرفاق التقدير به.");
        var evidence = $"دعم قرار {p.ModelVersion} ({p.SubjectRef}): {p.ScoreLow:0}–{p.ScoreHigh:0} · رأي المحلل: {o.Value}" + (o.Reason is null ? "" : $" — {o.Reason}");
        if (!request.Evidence.Contains(evidence)) request.Evidence = [.. request.Evidence, evidence];
        await audit.RecordAsync(new AuditEntry("decision_support.attached", "إرفاق تقدير دعم القرار مع رأي المحلل بطلب الموافقة", c.Id, c.Reference, Detail: evidence, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { attached = true, approvalRequestId = request.Id });
    }

    private static async Task<IResult> Governance(string version, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        if (version != AdherenceHeuristic.Version && version != "ADH-v1.2") throw new NotFoundException();
        var org = rc.OrganizationId!.Value;
        var monthStart = new DateTimeOffset(clock.TodayRiyadh.Year, clock.TodayRiyadh.Month, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var opinions = await db.Set<PredictionOpinion>().AsNoTracking().Where(o => o.OrganizationId == org && o.At >= monthStart).GroupBy(o => o.Value)
            .Select(g => new { value = g.Key, count = g.Count() }).ToListAsync();
        var total = opinions.Sum(o => o.count);
        var overrides = opinions.Where(o => o.value == HumanReview.Override).Sum(o => o.count);
        return Results.Ok(new
        {
            modelVersion = AdherenceHeuristic.Version, kind = "heuristic", trained = false,
            description = "قاعدة وزنية ثابتة وشفافة (الاستقطاع، سجل السداد، المتأخرات، التحقق من الدخل، التجاوب). ليست نموذجاً مدرَّباً.",
            accuracyMetrics = (object?)null, accuracyNote = "لا توجد مقاييس دقة: لم تُعايَر القاعدة على نتائج فعلية بعد.",
            review = new { by = (string?)null, at = (DateTimeOffset?)null, note = "بانتظار مراجعة لجنة المخاطر (افتراض — التصميم يذكر مراجعة 2026-08-15 لنموذج غير موجود هنا)." },
            excluded = AdherenceHeuristic.Excluded, limitations = AdherenceHeuristic.Limitations,
            overrideRateThisMonth = total == 0 ? (decimal?)null : Math.Round(overrides * 100m / total, 1), opinionsThisMonth = opinions,
        });
    }
}
