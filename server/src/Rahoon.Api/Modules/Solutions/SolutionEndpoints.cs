using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Solutions;

public sealed record SolutionParams(string Kind, int TermMonths, DateOnly FirstDueDate, decimal WaiverAmount, decimal DownPayment,
    int GraceMonths, string? Justification, decimal? DiscountedPayoff, uint? ExpectedVersion);
public sealed record SubmitSolutionRequest(string Note, bool Attested, string? ExpectedStatus);
public sealed record ReturnSolutionRequest(string Reason);

public static class SolutionEndpoints
{
    public const int MaxTermMonths = 120;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/solutions").RequirePermission(P.CaseView);
        g.MapGet("", List);
        g.MapPost("", CreateDraft).RequirePermission(P.SolutionPrepare).Idempotent();
        g.MapPost("/calculate", Calculate);
        g.MapGet("/{n:int}", Get);
        g.MapPut("/{n:int}", Update).RequirePermission(P.SolutionPrepare);
        g.MapPost("/{n:int}/handover", Handover).RequirePermission(P.SolutionPrepare).Idempotent();
        g.MapPost("/{n:int}/return", Return).RequirePermission(P.SolutionReview).Idempotent();
        g.MapGet("/{n:int}/submission", Submission).RequirePermission(P.SolutionReview);
        g.MapPost("/{n:int}/submit", Submit).RequirePermission(P.SolutionReview).Idempotent();
        g.MapGet("/{n:int}/preview", OwnerPreview);
    }

    private sealed record CaseContext(Case Case, decimal Outstanding, decimal LateFees, decimal? NetIncome, string? IncomeSource, DateOnly? IncomeVerifiedOn,
        decimal DsrLimit, ValuationReport? Valuation, string OwnerName);

    private static async Task<CaseContext> LoadContextAsync(RahoonDbContext db, Case c)
    {
        var debt = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        var analysis = await db.Analyses.AsNoTracking().FirstOrDefaultAsync(a => a.CaseId == c.Id);
        var valuation = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted).OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var owner = await db.Parties.AsNoTracking().Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefaultAsync() ?? "—";
        return new CaseContext(c, c.OutstandingAmount ?? debt?.Total ?? 0, debt?.LateFees ?? 0, analysis?.NetMonthlyIncome, analysis?.IncomeSourceLabel,
            analysis?.IncomeVerifiedOn, analysis?.DsrLimit ?? 0.55m, valuation, owner);
    }

    private static object ContextDto(CaseContext x) => new
    {
        reference = x.Case.Reference, owner = x.OwnerName, outstanding = x.Outstanding, lateFeesDue = x.LateFees,
        marketValue = x.Valuation?.MarketValue, netIncome = x.NetIncome, incomeSource = x.IncomeSource, incomeVerifiedOn = x.IncomeVerifiedOn,
        dsrLimit = x.DsrLimit, caseStatus = CaseStatusInfo.Key(x.Case.Status), maxTermMonths = MaxTermMonths,
    };

    private static object VersionDto(SolutionVersion s, IReadOnlyDictionary<Guid, string> names) => new
    {
        version = s.VersionNo, kind = s.Kind.ToString(), status = s.Status.ToString(), s.TermMonths, s.FirstDueDate, s.LastDueDate,
        s.WaiverAmount, s.WaiverPercent, s.DownPayment, s.GraceMonths, s.RescheduledAmount, s.InstallmentAmount, s.FinalInstallmentAmount,
        s.DiscountedPayoffAmount, s.Dsr, s.DsrLimit, s.NetIncomeUsed, s.Justification, s.BreachMissedConsecutive, s.BreachCureDays, s.OfferValidityDays,
        preparedBy = names.GetValueOrDefault(s.PreparedByUserId), preparedAt = s.PreparedAt, s.LockedAt, s.ReturnReason, rowVersion = s.Version,
        firstDueHijri = Hijri.Format(s.FirstDueDate),
    };

    private static async Task<Dictionary<Guid, string>> NamesAsync(RahoonDbContext db, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Users.Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }

    private static async Task<IResult> List(string reference, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var versions = await db.Solutions.AsNoTracking().Where(s => s.CaseId == c.Id).OrderBy(s => s.VersionNo).ToListAsync();
        var names = await NamesAsync(db, versions.Select(v => v.PreparedByUserId));
        return Results.Ok(new { context = ContextDto(await LoadContextAsync(db, c)), versions = versions.Select(v => VersionDto(v, names)) });
    }

    private static async Task<IResult> Get(string reference, int n, CaseAccess access, RahoonDbContext db, RequestContext rc)
    {
        var c = await access.GetAsync(reference, track: false);
        var v = await db.Solutions.AsNoTracking().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        var previous = await db.Solutions.AsNoTracking().Where(s => s.CaseId == c.Id && s.VersionNo < n).OrderByDescending(s => s.VersionNo).FirstOrDefaultAsync();
        var names = await NamesAsync(db, new[] { v.PreparedByUserId }.Concat(previous is null ? [] : [previous.PreparedByUserId]));
        var ctx = await LoadContextAsync(db, c);
        var route = await RouteAsync(db, c, v);
        var consent = await db.ConsentRecords.AnyAsync(x => x.CaseId == c.Id && x.Kind == "sale_consent");
        return Results.Ok(new
        {
            context = ContextDto(ctx),
            solution = VersionDto(v, names),
            previous = previous is null ? null : VersionDto(previous, names),
            route,
            permissions = new
            {
                canEdit = v.Status == SolutionStatus.Draft && rc.Has(P.SolutionPrepare) && (v.PreparedByUserId == rc.UserId || rc.Has(P.SolutionReview)),
                canHandover = v.Status == SolutionStatus.Draft && v.PreparedByUserId == rc.UserId,
                canSubmit = v.Status == SolutionStatus.InReview && rc.Has(P.SolutionReview) && v.PreparedByUserId != rc.UserId,
                canReturn = v.Status == SolutionStatus.InReview && rc.Has(P.SolutionReview) && v.PreparedByUserId != rc.UserId,
                isPreparer = v.PreparedByUserId == rc.UserId,
            },
            kinds = new[]
            {
                new { key = "Reschedule", label = "إعادة جدولة", desc = "تمديد المدة وتعديل القسط", enabled = true, reason = (string?)null },
                new { key = "ReducedPayoff", label = "سداد مخفض", desc = "دفعة واحدة بخصم معتمد", enabled = true, reason = (string?)null },
                new { key = "GracePeriod", label = "فترة سماح", desc = "تأجيل مؤقت ثم استئناف", enabled = true, reason = (string?)null },
                new { key = "VoluntarySale", label = "بيع طوعي", desc = "يتطلب موافقة المالك · مسار المرحلة 2", enabled = consent,
                    reason = consent ? null : "لا توجد موافقة موثقة من المالك على البيع." },
            },
        });
    }

    /// <summary>Reviewer and approver for the version, as shown in the builder's «مسار الموافقة المطلوب».</summary>
    private static async Task<object> RouteAsync(RahoonDbContext db, Case c, SolutionVersion v)
    {
        var manager = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => new { m.UserId, m.User!.FullName }).FirstOrDefaultAsync();
        var reviewerExcluded = manager is not null && manager.UserId != v.PreparedByUserId ? manager.UserId : Guid.Empty;
        var approver = await ApprovalRouting.ResolveApproverAsync(db, c.OrganizationId, v, [v.PreparedByUserId, reviewerExcluded]);
        string? limitText = approver is null ? null : approver.MaxAmount is null ? "بلا حد" : $"المبلغ ≤ {FormatShortAmount(approver.MaxAmount.Value)}، التنازل ≤ {approver.MaxWaiverPercent * 100:0.#}%";
        return new
        {
            reviewer = manager?.FullName,
            reviewerIsPreparer = manager?.UserId == v.PreparedByUserId,
            approver = approver?.Name,
            approverTier = approver?.TierLabel,
            approverLimit = limitText,
            escalated = approver is { TierRank: > 1 },
            noApprover = approver is null,
        };
    }

    private static string FormatShortAmount(decimal v) => v >= 1_000_000 ? $"{v / 1_000_000:0.#}M" : $"{v:N0}";

    private static SolutionKind ParseKind(string kind) =>
        Enum.TryParse<SolutionKind>(kind, true, out var k) ? k : throw new ValidationFailedException(new Dictionary<string, string[]> { ["kind"] = ["اختر نوع الحل."] });

    private static void ValidateParams(SolutionParams p, CaseContext ctx, DateOnly today)
    {
        var v = new Validator();
        var kind = ParseKind(p.Kind);
        v.Require(p.TermMonths is >= 1 and <= MaxTermMonths || kind == SolutionKind.ReducedPayoff, "termMonths", $"المدة بين 1 و{MaxTermMonths} شهراً (افتراض).");
        v.Require(p.FirstDueDate > today, "firstDueDate", "تاريخ أول قسط يجب أن يكون في المستقبل.");
        v.Require(p.FirstDueDate <= today.AddMonths(6), "firstDueDate", "أول قسط خلال 6 أشهر كحد أقصى.");
        v.Require(p.WaiverAmount >= 0, "waiverAmount", "التنازل لا يكون سالباً.");
        v.Require(p.WaiverAmount <= ctx.Outstanding, "waiverAmount", "التنازل لا يتجاوز المبلغ القائم.");
        v.Require(p.DownPayment >= 0, "downPayment", "الدفعة المقدمة لا تكون سالبة.");
        v.Require(p.WaiverAmount + p.DownPayment < ctx.Outstanding || kind == SolutionKind.ReducedPayoff, "downPayment", "التنازل والدفعة المقدمة يتجاوزان المبلغ القائم.");
        v.Require(p.GraceMonths is >= 0 and <= 12, "graceMonths", "فترة السماح بين 0 و12 شهراً.");
        v.Require(kind != SolutionKind.ReducedPayoff || p.DiscountedPayoff is > 0 && p.DiscountedPayoff <= ctx.Outstanding, "discountedPayoff", "مبلغ السداد المخفض مطلوب ولا يتجاوز القائم.");
        v.Require(kind != SolutionKind.GracePeriod || p.GraceMonths > 0, "graceMonths", "حدد مدة فترة السماح.");
        v.Require((p.Justification?.Length ?? 0) <= 2000, "justification", "المبرر حتى 2000 حرف.");
        v.ThrowIfInvalid();
    }

    private static void ApplyParams(SolutionVersion s, SolutionParams p, CaseContext ctx)
    {
        s.Kind = ParseKind(p.Kind);
        s.TermMonths = s.Kind == SolutionKind.ReducedPayoff ? 1 : p.TermMonths;
        s.FirstDueDate = p.FirstDueDate;
        s.WaiverAmount = Math.Round(p.WaiverAmount, 2);
        s.DownPayment = Math.Round(p.DownPayment, 2);
        s.GraceMonths = p.GraceMonths;
        s.Justification = p.Justification?.Trim();
        s.DiscountedPayoffAmount = s.Kind == SolutionKind.ReducedPayoff ? p.DiscountedPayoff : null;
        s.OutstandingAtPreparation = ctx.Outstanding;
        s.NetIncomeUsed = ctx.NetIncome;
        s.DsrLimit = ctx.DsrLimit;
        var f = SolutionCalculator.Compute(new SolutionInput(s.Kind, ctx.Outstanding, s.TermMonths, s.FirstDueDate, s.WaiverAmount, s.DownPayment,
            s.GraceMonths, ctx.NetIncome, ctx.DsrLimit, s.DiscountedPayoffAmount));
        s.RescheduledAmount = f.RescheduledAmount;
        s.InstallmentAmount = f.InstallmentAmount;
        s.FinalInstallmentAmount = f.FinalInstallmentAmount;
        s.LastDueDate = f.LastDueDate;
        s.WaiverPercent = f.WaiverPercent;
        s.Dsr = f.Dsr;
    }

    private static async Task<IResult> Calculate(string reference, SolutionParams p, CaseAccess access, RahoonDbContext db, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var ctx = await LoadContextAsync(db, c);
        ValidateParams(p, ctx, clock.TodayRiyadh);
        var probe = new SolutionVersion { OrganizationId = c.OrganizationId, CaseId = c.Id, PreparedByUserId = Guid.Empty };
        ApplyParams(probe, p, ctx);
        return Results.Ok(new
        {
            probe.RescheduledAmount, probe.InstallmentAmount, probe.FinalInstallmentAmount, probe.LastDueDate, probe.WaiverPercent, probe.Dsr,
            dsrWithinLimit = probe.Dsr is null || probe.Dsr <= ctx.DsrLimit, installments = probe.TermMonths,
            lastDueHijri = Hijri.Format(probe.LastDueDate), firstDueHijri = Hijri.Format(probe.FirstDueDate),
            route = await RouteAsync(db, c, probe),
        });
    }

    private static async Task<IResult> CreateDraft(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        if (c.Status is not (CaseStatus.ProposedSolution or CaseStatus.Negotiation))
            throw new DomainException("not_solution_stage", "إعداد الحلول متاح في مرحلتي «حل مقترح» و«تفاوض» فقط.", 409);
        var all = await db.Solutions.Where(s => s.CaseId == c.Id).OrderByDescending(s => s.VersionNo).ToListAsync();
        var existingDraft = all.FirstOrDefault(s => s.Status == SolutionStatus.Draft);
        if (existingDraft is not null) return Results.Ok(new { version = existingDraft.VersionNo, existing = true });
        if (all.Any(s => s.Status is SolutionStatus.PendingApproval))
            throw new ConflictException("approval_pending", "يوجد إصدار بانتظار الاعتماد؛ لا يمكن إنشاء إصدار جديد قبل القرار.");

        var basis = all.FirstOrDefault();
        var ctx = await LoadContextAsync(db, c);
        var v = new SolutionVersion
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, VersionNo = (basis?.VersionNo ?? 0) + 1, PreparedByUserId = rc.UserId, PreparedAt = clock.UtcNow,
            BasedOnVersionId = basis?.Id, Status = SolutionStatus.Draft,
        };
        var today = clock.TodayRiyadh;
        var p = basis is null
            ? new SolutionParams("Reschedule", 60, new DateOnly(today.Year, today.Month, 1).AddMonths(2), 0, 0, 0, null, null, null)
            : new SolutionParams(basis.Kind.ToString(), basis.TermMonths, basis.FirstDueDate > today ? basis.FirstDueDate : new DateOnly(today.Year, today.Month, 1).AddMonths(2),
                basis.WaiverAmount, basis.DownPayment, basis.GraceMonths, basis.Justification, basis.DiscountedPayoffAmount, null);
        ApplyParams(v, p, ctx);
        foreach (var s in all.Where(s => s.Status == SolutionStatus.InReview)) s.Status = SolutionStatus.Superseded;
        db.Solutions.Add(v);
        await audit.RecordAsync(new AuditEntry("solution.created", $"إنشاء الحل v{v.VersionNo}", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { version = v.VersionNo, existing = false });
    }

    private static async Task<IResult> Update(string reference, int n, SolutionParams p, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var v = await db.Solutions.FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        if (v.Status != SolutionStatus.Draft) throw new ConflictException("locked", $"الإصدار v{n} مقفل للتعديل. أي تغيير ينشئ إصداراً جديداً.");
        if (v.PreparedByUserId != rc.UserId && !rc.Has(P.SolutionReview)) throw new ForbiddenException();
        if (p.ExpectedVersion is { } ev && ev != v.Version) throw new ConflictException("concurrency", "عدّل مستخدم آخر هذه المسودة. حدّث الصفحة.");
        var ctx = await LoadContextAsync(db, c);
        if (ParseKind(p.Kind) == SolutionKind.VoluntarySale && !await db.ConsentRecords.AnyAsync(x => x.CaseId == c.Id && x.Kind == "sale_consent"))
            throw new DomainException("sale_consent_missing", "البيع الطوعي معطّل: لا توجد موافقة موثقة من المالك.");
        ValidateParams(p, ctx, clock.TodayRiyadh);
        ApplyParams(v, p, ctx);
        await db.SaveChangesAsync();
        return Results.Ok(new { savedAt = clock.UtcNow, rowVersion = v.Version, v.InstallmentAmount, v.Dsr });
    }

    private static async Task<IResult> Handover(string reference, int n, CaseAccess access, RahoonDbContext db, RequestContext rc, AuditLog audit, Notifier notifier, IClock clock)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var v = await db.Solutions.FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        if (v.Status != SolutionStatus.Draft) throw new ConflictException("not_draft", "هذا الإصدار سُلّم مسبقاً.");
        if (v.PreparedByUserId != rc.UserId) throw new ForbiddenException("التسليم للمراجعة يتم من مُعِدّ الإصدار.");
        var v2 = new Validator();
        v2.Require(v.WaiverAmount == 0 || !string.IsNullOrWhiteSpace(v.Justification), "justification", "مبرر الحل إلزامي عند وجود تنازل.");
        v2.Require(!string.IsNullOrWhiteSpace(v.Justification), "justification", "مبرر الحل مطلوب للمراجِع والمعتمد.");
        v2.Require(v.NetIncomeUsed is > 0 || v.Kind == SolutionKind.ReducedPayoff, "netIncome", "لا يوجد دخل متحقق في تحليل القدرة.");
        v2.ThrowIfInvalid("أكمل الحقول الإلزامية قبل التسليم.");

        v.Status = SolutionStatus.InReview;
        var manager = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => new { m.UserId, m.User!.FullName }).FirstOrDefaultAsync();
        if (manager is not null && manager.UserId != rc.UserId)
        {
            notifier.Notify(manager.UserId, c.OrganizationId, "solution", $"الحل v{n} جاهز لمراجعتك", $"{c.Reference} · أعدّه {rc.UserName}", $"/cases/{c.Reference}", c.Id);
            notifier.Task(c.OrganizationId, c.Id, $"مراجعة الحل v{n} وإرساله للموافقة الداخلية", manager.UserId, BusinessDays.Add(clock.TodayRiyadh, 2), "solution_review", $"/cases/{c.Reference}", rc.UserId);
        }
        await audit.RecordAsync(new AuditEntry("solution.handed_for_review", $"تسليم الحل v{n} للمراجعة", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = v.Status.ToString(), reviewer = manager?.FullName });
    }

    private static async Task<IResult> Return(string reference, int n, ReturnSolutionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc, AuditLog audit, Notifier notifier)
    {
        if (string.IsNullOrWhiteSpace(req.Reason)) Validate.Throw("reason", "سبب الإعادة مطلوب.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var v = await db.Solutions.FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        if (v.Status != SolutionStatus.InReview) throw new ConflictException("not_in_review", "هذا الإصدار ليس بانتظار المراجعة.");
        if (v.PreparedByUserId == rc.UserId) throw new ForbiddenException("المُعِدّ لا يراجع إصداره.");
        v.Status = SolutionStatus.Returned;
        v.ReturnReason = req.Reason.Trim();
        v.ReviewedByUserId = rc.UserId;
        notifier.Notify(v.PreparedByUserId, c.OrganizationId, "solution", $"أُعيد الحل v{n} للتعديل", req.Reason.Trim(), $"/cases/{c.Reference}", c.Id, "warn");
        await CloseTasksAsync(db, c.Id, "solution_review");
        await audit.RecordAsync(new AuditEntry("solution.returned", $"إعادة الحل v{n} للتعديل", c.Id, c.Reference, Reason: req.Reason.Trim(), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = v.Status.ToString() });
    }

    /// <summary>Data for the high-impact review screen before «إرسال للموافقة».</summary>
    private static async Task<IResult> Submission(string reference, int n, CaseAccess access, RahoonDbContext db, RequestContext rc, CaseWorkflow workflow, NextActionBuilder next, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var v = await db.Solutions.AsNoTracking().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        var approver = await ApprovalRouting.ResolveApproverAsync(db, c.OrganizationId, v, [v.PreparedByUserId, rc.UserId]);
        var checks = await next.SubmissionChecksAsync(c, v);
        var valuation = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted).OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var analysis = await db.Analyses.AsNoTracking().FirstOrDefaultAsync(a => a.CaseId == c.Id);
        var preparer = await db.Users.Where(u => u.Id == v.PreparedByUserId).Select(u => u.FullName).FirstAsync();
        var blockers = new List<string>();
        if (v.Status != SolutionStatus.InReview) blockers.Add($"الإصدار v{n} ليس بانتظار المراجعة.");
        if (v.PreparedByUserId == rc.UserId) blockers.Add("أنت مُعِدّ هذا الإصدار؛ لا يمكنك مراجعته أو إرساله للاعتماد (فصل المهام).");
        if (c.Status is not (CaseStatus.ProposedSolution or CaseStatus.Negotiation)) blockers.Add("الحالة ليست في مرحلة إعداد الحل.");
        if (approver is null) blockers.Add("لا يوجد معتمد متاح ضمن الحدود لهذا الحل.");
        blockers.AddRange(checks.Where(x => !x.Ok).Select(x => x.Text + " — غير مستوفى"));
        var due = BusinessDays.Add(clock.TodayRiyadh, 3);
        return Results.Ok(new
        {
            reference = c.Reference, version = n,
            from = CaseStatusInfo.Of(c.Status).LabelAr, to = CaseStatusInfo.Of(CaseStatus.InternalApproval).LabelAr, expectedStatus = CaseStatusInfo.Key(c.Status),
            approver = approver is null ? null : new
            {
                name = approver.Name, tier = approver.TierLabel,
                limit = approver.MaxAmount is null ? "بلا حد" : $"حدها {approver.MaxAmount:N0} ر.س، التنازل حتى {approver.MaxWaiverPercent * 100:0.#}%",
            },
            dueOn = due, dueDays = 3,
            evidence = new[]
            {
                new { name = $"الحل v{n} (نسخة مقفلة)", meta = $"{v.PreparedAt.ToOffset(TimeSpan.FromHours(3)):HH:mm} · {preparer}" },
                new { name = "تحليل القدرة على السداد", meta = v.Dsr is null ? "—" : $"{v.Dsr * 100:0.#}% استقطاع" },
                new { name = $"تقرير التقييم v{valuation?.VersionNo ?? 1}", meta = valuation is null ? "غير متوفر" : $"صالح حتى {valuation.ValidUntil:yyyy-MM-dd}" },
                new { name = analysis?.IncomeSourceLabel ?? "مستند الدخل", meta = analysis?.IncomeVerifiedOn is { } d ? $"متحقق {d:yyyy-MM-dd}" : "—" },
            },
            summary = new
            {
                kind = v.Kind.ToString(), v.RescheduledAmount, v.TermMonths, v.InstallmentAmount, v.WaiverAmount, v.WaiverPercent, v.Dsr, v.DsrLimit,
                complianceNotice = v.WaiverPercent > 0.01m,
            },
            blockers,
            canSubmit = blockers.Count == 0,
        });
    }

    /// <summary>
    /// Locks the version, opens the approval request with the resolved approver (≠ preparer, ≠ reviewer),
    /// moves the case to «موافقة داخلية». The owner sees nothing until approval.
    /// </summary>
    private static async Task<IResult> Submit(string reference, int n, SubmitSolutionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        CaseWorkflow workflow, AuditLog audit, Notifier notifier, IClock clock)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length >= 10, "note", "ملاحظتك للمعتمد إلزامية (10 أحرف على الأقل).")
            .Require(req.Note?.Length <= 1000, "note", "الملاحظة حتى 1000 حرف.")
            .Require(req.Attested, "attested", "أكّد أنك راجعت الشروط والأدلة وأنك لست مُعِدّ الإصدار.").ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var v = await db.Solutions.FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        if (v.PreparedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(new AuditEntry("approval.submit_blocked", $"محاولة إرسال الحل v{n} من مُعِدّه", c.Id, c.Reference, Detail: "فصل المهام", OrganizationId: c.OrganizationId));
            throw new ForbiddenException("أنت مُعِدّ هذا الإصدار؛ لا يمكنك مراجعته أو إرساله للاعتماد (فصل المهام).");
        }
        if (v.Status != SolutionStatus.InReview) throw new ConflictException("not_in_review", $"الإصدار v{n} ليس بانتظار المراجعة (ربما أُرسل مسبقاً).");

        var approver = await ApprovalRouting.ResolveApproverAsync(db, c.OrganizationId, v, [v.PreparedByUserId, rc.UserId])
            ?? throw new DomainException("no_approver", "لا يوجد معتمد متاح ضمن حدود الموافقة لهذا الحل.");

        var expected = req.ExpectedStatus is null ? null : CaseStatusInfo.Parse(req.ExpectedStatus);
        // Transition first (may refuse and audit the refusal), then record the rest.
        await workflow.TransitionAsync(c, "submit_for_approval", req.Note.Trim(), expected,
            evidence: [$"solution:v{n}", "analysis", "valuation"]);

        var now = clock.UtcNow;
        v.Status = SolutionStatus.PendingApproval;
        v.LockedAt = now;
        v.ReviewedByUserId = rc.UserId;
        v.LockedSnapshotJson = JsonSerializer.Serialize(new
        {
            v.VersionNo, kind = v.Kind.ToString(), v.OutstandingAtPreparation, v.TermMonths, v.FirstDueDate, v.LastDueDate, v.WaiverAmount, v.WaiverPercent,
            v.DownPayment, v.GraceMonths, v.RescheduledAmount, v.InstallmentAmount, v.FinalInstallmentAmount, v.Dsr, v.DsrLimit, v.NetIncomeUsed, v.Justification,
        });
        foreach (var old in await db.ApprovalRequests.Where(a => a.CaseId == c.Id && a.Subject == ApprovalSubject.Solution && a.Status == ApprovalStatus.Pending).ToListAsync())
            old.Status = ApprovalStatus.Superseded;

        var request = new ApprovalRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.Solution, SubjectId = v.Id, SubjectVersionNo = n,
            Title = $"اعتماد الحل v{n}", PreparedByUserId = v.PreparedByUserId, SubmittedByUserId = rc.UserId, SubmittedAt = now,
            SubmitterNote = req.Note.Trim(), SubmitterAttested = true, AssignedApproverUserId = approver.UserId, RequiredTier = approver.RoleKey,
            Amount = v.OutstandingAtPreparation, WaiverPercent = v.WaiverPercent, DueOn = BusinessDays.Add(clock.TodayRiyadh, 3),
            Evidence = [$"الحل v{n} (نسخة مقفلة)", "تحليل القدرة على السداد", "تقرير التقييم", "مستند الدخل"],
        };
        db.ApprovalRequests.Add(request);
        c.StageDueOn = request.DueOn;

        await CloseTasksAsync(db, c.Id, "solution_review");
        notifier.Task(c.OrganizationId, c.Id, $"اعتماد الحل v{n} — {c.Reference}", approver.UserId, request.DueOn, "approval", $"/approvals?request={request.Id}", rc.UserId);
        notifier.Notify(approver.UserId, c.OrganizationId, "approval", $"طلب اعتماد جديد: الحل v{n}", $"{c.Reference} · المهلة {request.DueOn:yyyy-MM-dd}", $"/approvals?request={request.Id}", c.Id);
        if (v.WaiverPercent > 0.01m)
            db.ComplianceNotices.Add(new ComplianceNotice { OrganizationId = c.OrganizationId, CaseId = c.Id, SolutionVersionId = v.Id, Rule = "waiver>1%" });

        await audit.RecordAsync(new AuditEntry("approval.submitted", $"إرسال الحل v{n} للموافقة الداخلية", c.Id, c.Reference, Reason: req.Note.Trim(),
            Detail: $"أُسند إلى {approver.Name} · المهلة {request.DueOn:yyyy-MM-dd}", Evidence: request.Evidence, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { approver = approver.Name, dueOn = request.DueOn, requestId = request.Id, status = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task CloseTasksAsync(RahoonDbContext db, Guid caseId, string kind)
    {
        foreach (var t in await db.Tasks.Where(t => t.CaseId == caseId && t.Kind == kind && t.Status == TaskStatus.Open).ToListAsync())
        {
            t.Status = TaskStatus.Done;
            t.CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Owner-facing rendering of a version (L17): plain language, no internal justification.</summary>
    private static async Task<IResult> OwnerPreview(string reference, int n, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var v = await db.Solutions.AsNoTracking().FirstOrDefaultAsync(s => s.CaseId == c.Id && s.VersionNo == n) ?? throw new NotFoundException();
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var template = await db.Templates.AsNoTracking().Where(t => t.Code == "TPL-OFFER-01" && t.Status == TemplateStatus.Published).OrderByDescending(t => t.VersionNo).FirstOrDefaultAsync();
        var validUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(v.OfferValidityDays);
        var body = template?.BodyAr.Replace("{القسط}", v.InstallmentAmount.ToString("N2")).Replace("{المدة}", $"{v.TermMonths} شهراً").Replace("{تاريخ_الصلاحية}", validUntil.ToString("yyyy-MM-dd"));
        return Results.Ok(new
        {
            lender = org.NameAr, version = n, kind = v.Kind.ToString(), installment = v.InstallmentAmount, termMonths = v.TermMonths,
            firstDue = v.FirstDueDate, firstDueHijri = Hijri.Format(v.FirstDueDate), lastDue = v.LastDueDate, waiver = v.WaiverAmount,
            rescheduled = v.RescheduledAmount, offerValidityDays = v.OfferValidityDays, validUntilIfSentToday = validUntil, message = body,
            breach = $"إذا تأخر قسطان متتاليان، سنتواصل معك لمراجعة الوضع ومنحك مهلة تصحيح {v.BreachCureDays} يوماً. لا يُحال الملف تلقائياً.",
            templateCode = template is null ? null : $"{template.Code} · v{template.VersionNo}",
        });
    }
}
