using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

public sealed record ScheduleRowBody(int? No, DateOnly? DueDate, decimal? Amount);
public sealed record RecordExecutionBody(
    string? Kind, Guid? SourceDocumentId, string? LenderReference, DateOnly? LenderDate, string? SummaryText, string? ExplanationText, bool ShareSource = true,
    DateOnly? ActivationDate = null, decimal? NewInstallment = null, int? TermMonths = null, decimal? SettlementAmount = null, string? TermsText = null,
    List<ScheduleRowBody>? Schedule = null, decimal? Amount = null, DateOnly? ReceivedOn = null, int? ScheduleItemNo = null, Guid? AnswersReportId = null,
    string? NoticeCategory = null, string? DocumentKind = null, Guid? SupersedesRecordId = null, string? CorrectionReason = null);
public sealed record VerifyExecutionBody(string? Decision, string? Reason, List<string>? Checklist);
public sealed record PaymentReportNoteBody(string? Text);
public sealed record ReportPaymentBody(decimal? Amount, DateOnly? TransferDate, string? BankReference, int? ScheduleItemNo, Guid? ProofDocumentId);

/// <summary>
/// Execution tracking of an accepted P1/P2 offer (Phase 1A-2, ADR 0002). The lender executes; Rahoon tracks and explains.
/// The team records what the lender sends (agreement and its schedule, payment confirmations, notices, closure documents),
/// each from the lender's document, and a second member verifies it (never the recorder, MFA step-up, DB check) before
/// the individual sees it. The individual can report a payment with proof; it is never shown as confirmed without a
/// published lender confirmation. Rahoon holds no funds, sends no reminders and computes no overdue state (Q6/Q12, Q16).
/// </summary>
public static class ExecutionEndpoints
{
    public static readonly string[] Kinds = ["agreement", "payment_confirmation", "lender_notice", "closure_document"];
    public static readonly string[] NoticeCategories = ["missed_installment", "agreement_change", "other"];
    public static readonly string[] ClosureDocumentKinds = ["rescheduling_confirmation", "clearance", "mortgage_release", "other"];
    public static readonly string[] VerifyChecklist = ["values_match_source", "reference_and_date_match", "kind_is_correct", "explanation_accurate"];
    public const string NoFundsNote = "رهون لا تستلم أي مبالغ. الدفع يتم لجهتك الممولة مباشرة.";
    /// <summary>V4: proposed wording, shown with every lender notice.</summary>
    public const string NoActionNote = "لم تتخذ رهون أي إجراء بخصوص تمويلك بسبب هذا الإشعار.";
    /// <summary>V4: proposed wording for withdrawing while execution is tracked.</summary>
    public const string WithdrawEffect = "سحب الطلب يوقف متابعة رهون ومشاركة البيانات فقط، ولا يغيّر اتفاقك مع جهتك الممولة.";

    /// <summary>Q18 rule: completion needs a closure document relevant to the path. Interim mapping until V13.</summary>
    public static string[] RelevantClosureKinds(string? path) => path switch
    {
        "p1" => ["rescheduling_confirmation"],
        "p2" => ["clearance", "mortgage_release"],
        _ => [],
    };

    /// <summary>Tracking, or paused by a consent withdrawal during tracking (ADR 0002 §4.1): lender evidence can still be recorded.</summary>
    public static bool InTracking(Request r) =>
        r.Status == RequestStatus.ExecutionTracking || r.Status == RequestStatus.InfoRequested && r.StatusBeforeInfoRequest == RequestStatus.ExecutionTracking;

    public static void Map(IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/api/team").RequireOrg(OrganizationKind.Operator);
        team.MapGet("/verify/execution", VerifyQueue).RequirePermission(P.RequestExecutionVerify);
        team.MapPost("/requests/{reference}/execution/start", Start).RequirePermission(P.RequestExecutionRecord).Idempotent();
        team.MapPost("/requests/{reference}/execution/records", Record).RequirePermission(P.RequestExecutionRecord).Idempotent();
        team.MapPost("/requests/{reference}/execution/records/{recordId:guid}/verify", Verify).RequirePermission(P.RequestExecutionVerify).Idempotent();
        team.MapPost("/requests/{reference}/payment-reports/{reportId:guid}/note", Note).RequirePermission(P.RequestExecutionRecord).Idempotent();

        var my = app.MapGroup("/api/my/requests/{reference}").RequireIndividual();
        my.MapPost("/payment-reports", ReportPayment).Idempotent();
    }

    public static async Task<bool> HasRelevantClosureDocumentAsync(RahoonDbContext db, Request r)
    {
        var kinds = RelevantClosureKinds((await RequestWorkflow.LatestResponseAsync(db, r))?.Offer.Path);
        return await db.RequestExecutionRecords.AnyAsync(x => x.RequestId == r.Id && x.Kind == "closure_document"
                                                             && x.Status == RequestExecutionRecordStatus.Published && kinds.Contains(x.DocumentKind!));
    }

    // ───────── projections ─────────

    /// <summary>Team view: every record with its verification state, schedules, and the individual's reports.</summary>
    internal static async Task<object> TeamExecutionAsync(RahoonDbContext db, Request r, Guid me)
    {
        var records = await db.RequestExecutionRecords.AsNoTracking().Where(x => x.RequestId == r.Id).OrderByDescending(x => x.RecordedAt).ToListAsync();
        var items = await db.RequestScheduleItems.AsNoTracking().Where(x => x.RequestId == r.Id).OrderBy(x => x.No).ToListAsync();
        var reports = await db.RequestPaymentReports.AsNoTracking().Where(x => x.RequestId == r.Id).OrderByDescending(x => x.At).ToListAsync();
        var path = (await RequestWorkflow.LatestResponseAsync(db, r))?.Offer.Path;
        return new
        {
            path,
            relevantClosureKinds = RelevantClosureKinds(path),
            records = records.Select(x => new
            {
                x.Id, x.Kind, status = Key(x.Status), x.SourceDocumentId, x.ShareSourceWithApplicant, x.LenderReference, x.LenderDate, x.SummaryText,
                x.ExplanationText, x.OfferId, x.Path, x.ActivationDate, x.NewInstallment, x.TermMonths, x.SettlementAmount, x.TermsText, x.Amount,
                x.ReceivedOn, x.ScheduleItemNo, x.AnswersReportId, x.NoticeCategory, x.DocumentKind, x.SupersedesRecordId, x.CorrectionReason,
                x.RecordedByLabel, x.RecordedAt, recordedByMe = x.RecordedByUserId == me, x.VerifiedByLabel, x.VerifiedAt, x.VerificationChecklist,
                x.ReturnReason, x.PublishedAt,
                schedule = items.Where(i => i.RecordId == x.Id).Select(i => new { i.No, i.DueDate, i.Amount }),
            }),
            paymentReports = reports.Select(x => new
            {
                x.Id, x.Reference, x.Amount, x.TransferDate, x.BankReference, x.ScheduleItemNo, x.ProofDocumentId, status = Key(x.Status), x.TeamNote,
                x.ConfirmationRecordId, x.At, x.AnsweredAt,
            }),
        };
    }

    /// <summary>
    /// The individual's view (D-8): published records only, the current agreement's schedule as the lender stated it, and
    /// their own reports. No team identities, checklists or pending records, and nothing computed from today's date.
    /// </summary>
    internal static async Task<object?> ApplicantExecutionAsync(RahoonDbContext db, Request r)
    {
        var records = await db.RequestExecutionRecords.AsNoTracking()
            .Where(x => x.RequestId == r.Id && x.Status == RequestExecutionRecordStatus.Published).OrderByDescending(x => x.PublishedAt).ToListAsync();
        var reports = await db.RequestPaymentReports.AsNoTracking().Where(x => x.RequestId == r.Id).OrderByDescending(x => x.At).ToListAsync();
        if (r.Status != RequestStatus.ExecutionTracking && !InTracking(r) && records.Count == 0 && reports.Count == 0) return null;

        var docIds = records.Where(x => x.ShareSourceWithApplicant).Select(x => x.SourceDocumentId).ToList();
        var shared = await db.RequestDocuments.AsNoTracking()
            .Where(d => docIds.Contains(d.Id) && d.Visibility == RequestDocumentVisibility.ApplicantAndTeam).ToDictionaryAsync(d => d.Id, d => d.CurrentVersionId);
        Guid? Source(RequestExecutionRecord x) => x.ShareSourceWithApplicant ? shared.GetValueOrDefault(x.SourceDocumentId) : null;

        var agreement = records.FirstOrDefault(x => x.Kind == "agreement");
        var confirmations = records.Where(x => x.Kind == "payment_confirmation").ToList();
        var items = agreement is null ? [] : await db.RequestScheduleItems.AsNoTracking().Where(i => i.RecordId == agreement.Id).OrderBy(i => i.No).ToListAsync();
        return new
        {
            noFundsNote = NoFundsNote,
            agreement = agreement is null ? null : new
            {
                agreement.Path, agreement.LenderReference, agreement.LenderDate, agreement.ActivationDate, agreement.NewInstallment, agreement.TermMonths,
                agreement.SettlementAmount, agreement.TermsText, agreement.SummaryText, agreement.ExplanationText, agreement.PublishedAt,
                sourceVersionId = Source(agreement),
            },
            // The lender's stated schedule. A row's state comes only from a lender confirmation or the individual's own report.
            schedule = items.Select(i =>
            {
                var confirmed = confirmations.FirstOrDefault(c => c.ScheduleItemNo == i.No);
                var reported = reports.Any(p => p.ScheduleItemNo == i.No && p.Status != RequestPaymentReportStatus.ConfirmedByLender);
                return new
                {
                    i.No, i.DueDate, i.Amount,
                    state = confirmed is not null ? "confirmed_by_lender" : reported ? "reported" : null,
                    confirmedReceivedOn = confirmed?.ReceivedOn,
                };
            }),
            confirmations = confirmations.Select(x => new
            {
                x.Amount, x.ReceivedOn, x.ScheduleItemNo, x.LenderReference, x.LenderDate, x.ExplanationText, x.PublishedAt, sourceVersionId = Source(x),
            }),
            notices = records.Where(x => x.Kind == "lender_notice").Select(x => new
            {
                category = x.NoticeCategory, x.SummaryText, x.ExplanationText, x.LenderReference, x.LenderDate, x.PublishedAt, sourceVersionId = Source(x),
                noActionNote = NoActionNote,
            }),
            closureDocuments = records.Where(x => x.Kind == "closure_document").Select(x => new
            {
                x.DocumentKind, x.LenderReference, x.LenderDate, x.ExplanationText, x.PublishedAt, sourceVersionId = Source(x),
            }),
            paymentReports = reports.Select(x => new
            {
                x.Reference, x.Amount, x.TransferDate, x.BankReference, x.ScheduleItemNo, status = Key(x.Status), x.TeamNote, x.At,
            }),
        };
    }

    private static string Key(RequestExecutionRecordStatus s) => s switch
    {
        RequestExecutionRecordStatus.PendingVerification => "pending_verification",
        _ => s.ToString().ToLowerInvariant(),
    };

    private static string Key(RequestPaymentReportStatus s) => s switch
    {
        RequestPaymentReportStatus.NotConfirmedYet => "not_confirmed_yet",
        RequestPaymentReportStatus.ConfirmedByLender => "confirmed_by_lender",
        _ => "reported",
    };

    private static void EnsureWorker(Request r, RequestContext rc)
    {
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
    }

    // ───────── start ─────────

    private static async Task<IResult> Start(string reference, NextStepBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        await workflow.TransitionAsync(r, "start_execution_tracking", expected: RequestStatus.ResponseRecorded, nextStep: b.NextStep);
        svc.AddUpdate(r, "execution_started", "بدأ فريق رهون متابعة تنفيذ اتفاقك مع جهتك الممولة",
            "جهتك الممولة هي من ينفذ الاتفاق. نعرض هنا ما يصلنا منها من اتفاق وتأكيدات ومستندات بعد أن يتحقق منه عضو آخر من الفريق، " +
            "ويمكنك إبلاغنا بأي دفعة سددتها. " + NoFundsNote, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "بدأت متابعة تنفيذ اتفاقك", "تابع ما يصل من جهتك الممولة في صفحة طلبك.", $"/my/requests/{r.Reference}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    // ───────── record (team, from the lender's document) ─────────

    private static async Task<IResult> Record(string reference, RecordExecutionBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var today = clock.TodayRiyadh;
        var kind = b.Kind;
        var v = new Validator()
            .Require(kind is not null && Kinds.Contains(kind), "kind", "اختر نوع السجل.")
            .Require(b.SourceDocumentId is not null, "sourceDocumentId", "أرفق مستند الجهة أو اختره من المستندات (إلزامي).")
            .Require(!string.IsNullOrWhiteSpace(b.LenderReference), "lenderReference", "أدخل مرجع مستند الجهة.")
            .Require(b.LenderDate is { } d && d <= today, "lenderDate", "أدخل تاريخ مستند الجهة (ليس في المستقبل).")
            .Require(!string.IsNullOrWhiteSpace(b.SummaryText), "summaryText", "اكتب ما ذكرته الجهة.")
            .Require(!string.IsNullOrWhiteSpace(b.ExplanationText), "explanationText", "اكتب «ماذا يعني لك» بلغة يفهمها العميل.")
            .Require(b.SupersedesRecordId is null || !string.IsNullOrWhiteSpace(b.CorrectionReason), "correctionReason", "اكتب سبب التصحيح.");
        var rows = b.Schedule ?? [];
        if (kind == "agreement")
        {
            v.Require(rows.Count <= 600, "schedule", "الجدول أطول من المسموح (600 قسط).")
             .Require(rows.All(x => x.No is > 0 && x.DueDate is not null && x.Amount is > 0), "schedule", "أكمل رقم كل قسط وتاريخه ومبلغه كما في مستند الجهة.")
             .Require(rows.Select(x => x.No).Distinct().Count() == rows.Count, "schedule", "أرقام الأقساط مكررة.")
             .Require(b.NewInstallment is null or > 0, "newInstallment", "القسط يجب أن يكون أكبر من صفر.")
             .Require(b.TermMonths is null or > 0 and <= 600, "termMonths", "أدخل المدة بالأشهر.")
             .Require(b.SettlementAmount is null or > 0, "settlementAmount", "المبلغ يجب أن يكون أكبر من صفر.");
        }
        if (kind == "payment_confirmation")
            v.Require(b.Amount is > 0, "amount", "أدخل المبلغ الذي أكدت الجهة استلامه.")
             .Require(b.ReceivedOn is { } rd && rd <= today, "receivedOn", "أدخل تاريخ الاستلام بحسب الجهة (ليس في المستقبل).");
        if (kind == "lender_notice") v.Require(b.NoticeCategory is not null && NoticeCategories.Contains(b.NoticeCategory), "noticeCategory", "اختر نوع الإشعار.");
        if (kind == "closure_document") v.Require(b.DocumentKind is not null && ClosureDocumentKinds.Contains(b.DocumentKind), "documentKind", "اختر نوع مستند الإغلاق.");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        if (!InTracking(r)) throw new ConflictException("state", "تُسجَّل سجلات التنفيذ أثناء متابعة التنفيذ فقط.");
        // Every id in the body belongs to this request (and is of the right kind or state).
        var source = await db.RequestDocuments.FirstOrDefaultAsync(d => d.Id == b.SourceDocumentId && d.RequestId == r.Id && d.Kind == "lender_letter");
        if (source is null) Validate.Throw("sourceDocumentId", "اختر مستند الجهة من مستندات هذا الطلب.");
        if (b.SupersedesRecordId is { } supId
            && !await db.RequestExecutionRecords.AnyAsync(x => x.Id == supId && x.RequestId == r.Id && x.Kind == kind && x.Status == RequestExecutionRecordStatus.Published))
            Validate.Throw("supersedesRecordId", "السجل المصحَّح غير موجود في هذا الطلب أو ليس منشوراً.");
        if (b.AnswersReportId is { } repId
            && !await db.RequestPaymentReports.AnyAsync(x => x.Id == repId && x.RequestId == r.Id && x.Status != RequestPaymentReportStatus.ConfirmedByLender))
            Validate.Throw("answersReportId", "بلاغ السداد غير موجود في هذا الطلب أو سبق تأكيده.");

        var accepted = (await RequestWorkflow.LatestResponseAsync(db, r))?.Offer;
        var rec = new RequestExecutionRecord
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind!, SourceDocumentId = source!.Id,
            ShareSourceWithApplicant = b.ShareSource, LenderReference = Clean(b.LenderReference, 100)!, LenderDate = b.LenderDate!.Value,
            SummaryText = Clean(b.SummaryText, 2000)!, ExplanationText = Clean(b.ExplanationText, 2000)!,
            SupersedesRecordId = b.SupersedesRecordId, CorrectionReason = Clean(b.CorrectionReason, 1000),
            RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName, RecordedAt = clock.UtcNow,
        };
        switch (kind)
        {
            case "agreement":
                // As the lender stated it: prefilled from the accepted offer, the letter wins where given.
                rec.OfferId = accepted?.Id;
                rec.Path = accepted?.Path;
                rec.ActivationDate = b.ActivationDate;
                rec.NewInstallment = b.NewInstallment ?? accepted?.NewInstallment;
                rec.TermMonths = b.TermMonths ?? accepted?.TermMonths;
                rec.SettlementAmount = b.SettlementAmount ?? accepted?.SettlementAmount;
                rec.TermsText = Clean(b.TermsText, 2000);
                foreach (var row in rows)
                    db.RequestScheduleItems.Add(new RequestScheduleItem
                    {
                        OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, RecordId = rec.Id,
                        No = row.No!.Value, DueDate = row.DueDate!.Value, Amount = row.Amount!.Value,
                    });
                break;
            case "payment_confirmation":
                rec.Amount = b.Amount;
                rec.ReceivedOn = b.ReceivedOn;
                rec.ScheduleItemNo = b.ScheduleItemNo;
                rec.AnswersReportId = b.AnswersReportId;
                break;
            case "lender_notice":
                rec.NoticeCategory = b.NoticeCategory;
                break;
            case "closure_document":
                rec.DocumentKind = b.DocumentKind;
                break;
        }
        db.RequestExecutionRecords.Add(rec);
        svc.AddUpdate(r, "execution_recorded", $"سُجّل {KindLabel(rec.Kind)} من الجهة وينتظر التحقق", authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.execution_recorded", $"تسجيل {KindLabel(rec.Kind)} من الجهة",
            detail: $"مرجع الجهة {rec.LenderReference}" + (rows.Count > 0 ? $" · جدول {rows.Count} قسطاً" : ""), evidence: [source.Id.ToString()]));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { rec.Id, rec.Kind, status = Key(rec.Status) });
    }

    internal static string KindLabel(string kind) => kind switch
    {
        "agreement" => "الاتفاق",
        "payment_confirmation" => "تأكيد سداد",
        "lender_notice" => "إشعار",
        _ => "مستند إغلاق",
    };

    // ───────── verify (another member, step-up) ─────────

    private static async Task<IResult> VerifyQueue(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.RequestExecutionRecords.AsNoTracking().Where(x => x.Status == RequestExecutionRecordStatus.PendingVerification)
            .Join(db.Requests.Include(r => r.Institution), x => x.RequestId, r => r.Id, (x, r) => new { x, r })
            .OrderBy(y => y.x.RecordedAt).ToListAsync();
        return Results.Ok(rows.Select(y => new
        {
            y.r.Reference, institutionName = y.r.InstitutionDisplayName, recordId = y.x.Id, y.x.Kind, y.x.RecordedByLabel, y.x.RecordedAt,
            recordedByMe = y.x.RecordedByUserId == rc.UserId,
        }));
    }

    private static async Task<IResult> Verify(string reference, Guid recordId, VerifyExecutionBody b, RequestAccess access, RahoonDbContext db,
        RequestContext rc, RequestService svc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = b.Decision;
        new Validator()
            .Require(decision is "publish" or "return", "decision", "اختر: اعتماد ونشر، أو إعادة مع سبب.")
            .Require(decision != "return" || !string.IsNullOrWhiteSpace(b.Reason), "reason", "اكتب سبب الإعادة لمن سجّل السجل.")
            .Require(decision != "publish" || VerifyChecklist.All(i => b.Checklist?.Contains(i) == true), "checklist", "أكمل قائمة المطابقة مع مستند الجهة قبل النشر.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        var rec = await db.RequestExecutionRecords.FirstOrDefaultAsync(x => x.Id == recordId && x.RequestId == r.Id) ?? throw new NotFoundException();
        if (rec.Status != RequestExecutionRecordStatus.PendingVerification) throw new ConflictException("state", "السجل ليس بانتظار التحقق.");
        if (rec.RecordedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(RequestWorkflow.Entry(r, "request.execution_verify_blocked", "محاولة التحقق من سجل تنفيذ سجّله العضو نفسه",
                detail: "المانع: المتحقق لا يكون من سجّل السجل. لم يُنشر.", blocked: true));
            throw new ForbiddenException("لا يمكنك التحقق من سجل سجّلته بنفسك؛ يتحقق منه عضو آخر.");
        }
        var now = clock.UtcNow;
        var label = KindLabel(rec.Kind);
        if (decision == "return")
        {
            rec.Status = RequestExecutionRecordStatus.Returned;
            rec.ReturnReason = Clean(b.Reason, 1000);
            svc.AddUpdate(r, "execution_returned", $"أُعيد {label} للتصحيح", rec.ReturnReason, authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
            notifier.Notify(rec.RecordedByUserId, r.OrganizationId, "request", $"أُعيد {label} على {r.Reference}", rec.ReturnReason, $"/team/requests/{r.Reference}", tone: "warn");
            await audit.RecordAsync(RequestWorkflow.Entry(r, "request.execution_returned", $"إعادة {label} مع سبب", reason: rec.ReturnReason));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { status = "returned" });
        }

        EndpointAccess.EnsureStepUp(rc, clock);
        if (!InTracking(r)) throw new ConflictException("state", "لم يعد الطلب في متابعة التنفيذ.");
        rec.VerifiedByUserId = rc.UserId;
        rec.VerifiedByLabel = rc.UserName;
        rec.VerifiedAt = now;
        rec.VerificationChecklist = [.. b.Checklist!.Where(VerifyChecklist.Contains).Distinct()];
        rec.Status = RequestExecutionRecordStatus.Published;
        rec.PublishedAt = now;

        // A newer agreement replaces the previous one (and its schedule); a correction replaces the record it names.
        var superseded = await db.RequestExecutionRecords
            .Where(x => x.RequestId == r.Id && x.Id != rec.Id && x.Status == RequestExecutionRecordStatus.Published
                        && (rec.Kind == "agreement" && x.Kind == "agreement" || x.Id == rec.SupersedesRecordId))
            .ToListAsync();
        foreach (var old in superseded) old.Status = RequestExecutionRecordStatus.Superseded;
        if (rec.AnswersReportId is { } repId)
        {
            var report = await db.RequestPaymentReports.FirstAsync(x => x.Id == repId && x.RequestId == r.Id);
            report.Status = RequestPaymentReportStatus.ConfirmedByLender;
            report.ConfirmationRecordId = rec.Id;
            report.AnsweredAt = now;
        }
        if (rec.ShareSourceWithApplicant)
        {
            var doc = await db.RequestDocuments.FirstAsync(d => d.Id == rec.SourceDocumentId);
            doc.Visibility = RequestDocumentVisibility.ApplicantAndTeam;
        }
        var title = rec.Kind switch
        {
            "agreement" => "سجّلنا اتفاقك مع جهتك الممولة كما ورد في مستندها",
            "payment_confirmation" => "أكدت جهتك الممولة استلام دفعة",
            "lender_notice" => "وصلنا إشعار من جهتك الممولة",
            _ => "وصل مستند إغلاق من جهتك الممولة",
        };
        svc.AddUpdate(r, "execution_published", title, rec.Kind == "lender_notice" ? $"{rec.ExplanationText} {NoActionNote}" : rec.ExplanationText,
            authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", title, "راجع التفاصيل في صفحة طلبك.", $"/my/requests/{r.Reference}");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.execution_published", $"تحقق من {label} ونشره للعميل",
            detail: $"سجّله {rec.RecordedByLabel} · تحقق منه {rc.UserName}" + (superseded.Count > 0 ? $" · استبدل {superseded.Count}" : ""),
            evidence: rec.VerificationChecklist));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "published" });
    }

    // ───────── payment reports ─────────

    /// <summary>The team's plain note on a report the lender hasn't confirmed yet. Never a rejection.</summary>
    private static async Task<IResult> Note(string reference, Guid reportId, PaymentReportNoteBody b, RequestAccess access, RahoonDbContext db,
        RequestContext rc, RequestService svc, IClock clock, AuditLog audit)
    {
        var text = Clean(b.Text, 1000);
        if (text is null) Validate.Throw("text", "اكتب الملاحظة كما سيراها العميل.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        EnsureWorker(r, rc);
        var report = await db.RequestPaymentReports.FirstOrDefaultAsync(x => x.Id == reportId && x.RequestId == r.Id) ?? throw new NotFoundException();
        if (report.Status == RequestPaymentReportStatus.ConfirmedByLender) throw new ConflictException("state", "أكدت الجهة هذا السداد مسبقاً.");
        report.Status = RequestPaymentReportStatus.NotConfirmedYet;
        report.TeamNote = text;
        report.AnsweredAt = clock.UtcNow;
        svc.AddUpdate(r, "payment_report_note", $"ملاحظة من فريق رهون على بلاغ السداد {report.Reference}", text, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.payment_report_noted", $"ملاحظة على بلاغ السداد {report.Reference}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = Key(report.Status) });
    }

    /// <summary>The individual reports a payment they made to their lender, with proof (a request document of kind payment_proof).</summary>
    private static async Task<IResult> ReportPayment(string reference, ReportPaymentBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var today = clock.TodayRiyadh;
        new Validator()
            .Require(b.Amount is > 0, "amount", "أدخل المبلغ الذي سددته.")
            .Require(b.TransferDate is { } d && d <= today && d >= today.AddYears(-5), "transferDate", "أدخل تاريخ التحويل (ليس في المستقبل).")
            .Require(b.ProofDocumentId is not null, "proofDocumentId", "أرفق إثبات السداد.")
            .ThrowIfInvalid();
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status != RequestStatus.ExecutionTracking) throw new ConflictException("state", "يُبلَّغ عن السداد أثناء متابعة التنفيذ فقط.");
        var proof = await db.RequestDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == b.ProofDocumentId && d.RequestId == r.Id && d.Source == "applicant" && d.Kind == "payment_proof");
        if (proof is null) Validate.Throw("proofDocumentId", "اختر إثبات السداد الذي رفعته لهذا الطلب.");
        if (b.ScheduleItemNo is { } no)
        {
            var agreementId = await db.RequestExecutionRecords.Where(x => x.RequestId == r.Id && x.Kind == "agreement" && x.Status == RequestExecutionRecordStatus.Published)
                .Select(x => (Guid?)x.Id).FirstOrDefaultAsync();
            if (agreementId is null || !await db.RequestScheduleItems.AnyAsync(i => i.RecordId == agreementId && i.No == no))
                Validate.Throw("scheduleItemNo", "رقم القسط غير موجود في جدول جهتك.");
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var n = await db.RequestPaymentReports.CountAsync(x => x.RequestId == r.Id) + 1;
        var report = new RequestPaymentReport
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Reference = $"{r.Reference}-P{n}",
            Amount = b.Amount!.Value, TransferDate = b.TransferDate!.Value, BankReference = Clean(b.BankReference, 100), ScheduleItemNo = b.ScheduleItemNo,
            ProofDocumentId = proof!.Id, At = clock.UtcNow,
        };
        db.RequestPaymentReports.Add(report);
        svc.AddUpdate(r, "payment_reported", "أبلغتَنا بسداد دفعة لجهتك الممولة",
            "سجّلنا بلاغك. سنطلب من جهتك الممولة تأكيده، ونعرض التأكيد هنا.", authorKind: "applicant");
        if (r.AssignedCoordinatorId is { } coordinator)
            notifier.Notify(coordinator, r.OrganizationId, "request", $"بلاغ سداد على {r.Reference}", report.Reference, $"/team/requests/{r.Reference}");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.payment_reported", $"بلاغ سداد من العميل ({report.Reference})",
            evidence: [proof.Id.ToString()]));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { report.Reference, status = Key(report.Status), report.At });
    }

    private static string? Clean(string? s, int max) => TeamRequestEndpoints.Clean(s, max);
}
