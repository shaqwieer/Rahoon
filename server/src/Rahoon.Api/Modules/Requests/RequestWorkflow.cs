using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

/// <summary>
/// A permitted request transition (ADR 0001 §4.5). <see cref="Permission"/> empty = an applicant action (the caller
/// must be the individual who owns the request); otherwise a Rahoon team permission.
/// </summary>
public sealed record RequestTransitionDef(
    string Key,
    RequestStatus[] From,
    RequestStatus To,
    string LabelAr,
    string Permission,
    bool RequiresReason = false,
    bool RequiresStepUp = false,
    string[]? Guards = null);

public static class RequestStatusInfo
{
    public static readonly RequestStatus[] Terminal = [RequestStatus.Closed, RequestStatus.NotEligible, RequestStatus.Withdrawn];
    public static readonly RequestStatus[] NonTerminal = Enum.GetValues<RequestStatus>().Except(Terminal).ToArray();

    public static bool IsTerminal(RequestStatus s) => Terminal.Contains(s);

    public static string Key(RequestStatus s) => s switch
    {
        RequestStatus.TeamReview => "team_review",
        RequestStatus.InfoRequested => "info_requested",
        RequestStatus.LenderCoordination => "lender_coordination",
        RequestStatus.OfferAvailable => "offer_available",
        RequestStatus.ResponseRecorded => "response_recorded",
        RequestStatus.NotEligible => "not_eligible",
        _ => s.ToString().ToLowerInvariant(),
    };

    public static RequestStatus? Parse(string? key) =>
        Enum.GetValues<RequestStatus>().Cast<RequestStatus?>().FirstOrDefault(s => Key(s!.Value) == key);

    /// <summary>Proposed labels (design request D-3, «نص مقترح»).</summary>
    public static string LabelAr(RequestStatus s) => s switch
    {
        RequestStatus.Draft => "مسودة",
        RequestStatus.Submitted => "مقدَّم",
        RequestStatus.TeamReview => "قيد دراسة فريق رهون",
        RequestStatus.InfoRequested => "نحتاج معلومة منك",
        RequestStatus.LenderCoordination => "قيد التنسيق مع جهتك الممولة",
        RequestStatus.OfferAvailable => "وصل عرض من جهتك الممولة",
        RequestStatus.ResponseRecorded => "سجّلنا ردك",
        RequestStatus.Closed => "مغلق",
        RequestStatus.NotEligible => "غير مناسب للخدمة حالياً",
        RequestStatus.Withdrawn => "مسحوب",
        _ => s.ToString(),
    };

    public static RequestWaitingOn DefaultWaitingOn(RequestStatus s) => s switch
    {
        RequestStatus.Draft or RequestStatus.InfoRequested or RequestStatus.OfferAvailable => RequestWaitingOn.Applicant,
        RequestStatus.Submitted or RequestStatus.TeamReview or RequestStatus.ResponseRecorded => RequestWaitingOn.Team,
        RequestStatus.LenderCoordination => RequestWaitingOn.Lender,
        _ => RequestWaitingOn.None,
    };

    public static string WaitingKey(RequestWaitingOn w) => w switch
    {
        RequestWaitingOn.Team => "team",
        RequestWaitingOn.Applicant => "applicant",
        RequestWaitingOn.Lender => "lender",
        _ => "none",
    };
}

public sealed class RequestWorkflow(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
{
    public static readonly IReadOnlyList<RequestTransitionDef> Transitions =
    [
        new("submit", [RequestStatus.Draft], RequestStatus.Submitted, "إرسال الطلب", "",
            Guards: ["identity_verified", "required_fields", "consent_active", "duplicate_acknowledged"]),
        new("pick_up", [RequestStatus.Submitted], RequestStatus.TeamReview, "بدء دراسة الطلب", P.RequestReview),
        new("request_info", [RequestStatus.TeamReview, RequestStatus.LenderCoordination], RequestStatus.InfoRequested, "طلب استكمال", P.RequestRequestInfo,
            RequiresReason: true),
        new("info_provided", [RequestStatus.InfoRequested], RequestStatus.InfoRequested /* resolved to the previous state */, "إرسال الاستكمال", ""),
        new("start_coordination", [RequestStatus.TeamReview], RequestStatus.LenderCoordination, "بدء التنسيق مع الجهة", P.RequestCoordinate,
            Guards: ["consent_active", "identity_checked", "coordination_recorded"]),
        new("publish_offer", [RequestStatus.LenderCoordination], RequestStatus.OfferAvailable, "نشر العرض للعميل", P.RequestOfferVerify,
            RequiresStepUp: true),
        new("respond", [RequestStatus.OfferAvailable], RequestStatus.ResponseRecorded, "رد العميل على العرض", ""),
        new("continue_coordination", [RequestStatus.ResponseRecorded], RequestStatus.LenderCoordination, "متابعة التنسيق", P.RequestResponseRelay,
            Guards: ["consent_active", "response_relayed"]),
        new("close", [RequestStatus.ResponseRecorded, RequestStatus.LenderCoordination], RequestStatus.Closed, "إغلاق الطلب", P.RequestClose,
            RequiresReason: true),
        new("not_eligible", [RequestStatus.TeamReview], RequestStatus.NotEligible, "غير مناسب للخدمة", P.RequestReview, RequiresReason: true),
        new("withdraw", RequestStatusInfo.NonTerminal, RequestStatus.Withdrawn, "سحب الطلب", ""),
    ];

    public static RequestTransitionDef Def(string key) => Transitions.First(t => t.Key == key);

    /// <summary>
    /// Performs a transition in the caller's unit of work: expected state (409), allowed source, actor, reason, step-up and
    /// named guards. Refusals are audited in their own transaction.
    /// </summary>
    public async Task TransitionAsync(Request r, string key, string? reason = null, RequestStatus? expected = null,
        string? nextStep = null, IReadOnlyList<string>? extraGuardFailures = null)
    {
        var t = Def(key);
        var from = r.Status;
        var to = key == "info_provided" ? r.StatusBeforeInfoRequest ?? RequestStatus.TeamReview : t.To;

        if (expected is { } exp && exp != r.Status)
            throw new ConflictException("stale_state", "تغيّرت حالة الطلب منذ فتحه. حدّث الصفحة لرؤية الوضع الحالي.");

        if (!t.From.Contains(from))
        {
            await Blocked(r, t, to, reason, [$"«{t.LabelAr}» غير متاح والطلب «{RequestStatusInfo.LabelAr(from)}»."]);
            throw new DomainException("transition_not_allowed", $"لا يمكن تنفيذ «{t.LabelAr}» والطلب «{RequestStatusInfo.LabelAr(from)}».", StatusCodes.Status409Conflict);
        }

        var applicantAction = t.Permission.Length == 0;
        if (applicantAction ? !(rc.IsIndividual && r.ApplicantUserId == rc.UserId) : !(rc.IsOperator && rc.Has(t.Permission)))
        {
            await Blocked(r, t, to, reason, ["لا يملك الفاعل صلاحية هذا الإجراء."]);
            throw new ForbiddenException();
        }

        if (t.RequiresReason && string.IsNullOrWhiteSpace(reason))
            Validate.Throw("reason", "السبب إلزامي لهذا الإجراء.");
        if (t.RequiresStepUp) EndpointAccess.EnsureStepUp(rc, clock);

        var failures = (await EvaluateGuardsAsync(r, t)).Concat(extraGuardFailures ?? []).ToList();
        if (failures.Count > 0)
        {
            await Blocked(r, t, to, reason, failures);
            throw new DomainException("guard_failed", $"لا يمكن تنفيذ «{t.LabelAr}» الآن.", StatusCodes.Status422UnprocessableEntity, failures);
        }

        var now = clock.UtcNow;
        if (to == RequestStatus.InfoRequested) r.StatusBeforeInfoRequest = from;
        if (key == "info_provided") r.StatusBeforeInfoRequest = null;
        if (to == RequestStatus.Submitted) r.SubmittedAt = now;
        if (to == RequestStatus.Withdrawn) { r.WithdrawnAt = now; r.WithdrawReason = reason?.Trim(); }
        if (to == RequestStatus.Closed) r.ClosedAt = now;
        if (to == RequestStatus.NotEligible) r.NotEligibleReason = reason?.Trim();

        r.Status = to;
        r.StatusChangedAt = now;
        r.WaitingOn = RequestStatusInfo.DefaultWaitingOn(to);
        r.NextStepText = string.IsNullOrWhiteSpace(nextStep) ? null : nextStep.Trim();

        await audit.RecordAsync(Entry(r, "request.transition", $"{RequestStatusInfo.LabelAr(from)} ← {RequestStatusInfo.LabelAr(to)}",
            RequestStatusInfo.Key(from), RequestStatusInfo.Key(to), reason, t.LabelAr));
    }

    /// <summary>Guard failures for display (disabled actions with reasons) and for the transition itself.</summary>
    public async Task<List<string>> EvaluateGuardsAsync(Request r, RequestTransitionDef t)
    {
        var failures = new List<string>();
        foreach (var g in t.Guards ?? [])
        {
            var msg = await CheckAsync(r, g);
            if (msg is not null) failures.Add(msg);
        }
        return failures;
    }

    public static AuditEntry Entry(Request r, string type, string title, string? from = null, string? to = null, string? reason = null,
        string? detail = null, bool blocked = false, IReadOnlyList<string>? evidence = null) =>
        new(type, title, FromState: from, ToState: to, Reason: reason, Detail: detail, Blocked: blocked, Evidence: evidence,
            OrganizationId: r.OrganizationId, SubjectType: "request", SubjectReference: r.Reference);

    private Task Blocked(Request r, RequestTransitionDef t, RequestStatus to, string? reason, IReadOnlyList<string> failures) =>
        audit.RecordBlockedAsync(Entry(r, "request.transition_blocked",
            $"محاولة محجوبة: {RequestStatusInfo.LabelAr(r.Status)} ← {RequestStatusInfo.LabelAr(to)}",
            RequestStatusInfo.Key(r.Status), RequestStatusInfo.Key(to), reason,
            "المانع: " + string.Join(" · ", failures) + ". لم يُنفذ الإجراء.", blocked: true));

    // ───────── Guards ─────────

    private async Task<string?> CheckAsync(Request r, string guard)
    {
        switch (guard)
        {
            case "identity_verified":
            {
                var verified = await db.IndividualProfiles.AsNoTracking().AnyAsync(p => p.UserId == r.ApplicantUserId && p.PhoneVerifiedAt != null);
                return verified ? null : "لم يُتحقق من رقم الجوال بعد.";
            }
            case "required_fields":
            {
                var missing = MissingFields(r);
                return missing.Count == 0 ? null : "بيانات ناقصة: " + string.Join("، ", missing);
            }
            case "consent_active":
            {
                var consent = await ActiveConsentAsync(db, r);
                if (consent is null) return "لا توجد موافقة موثقة سارية على مشاركة البيانات مع الجهة الممولة.";
                return null;
            }
            case "duplicate_acknowledged":
            {
                var dup = await FindDuplicateAsync(db, r);
                return dup is not null && !r.DuplicateAcknowledged ? $"لديك طلب قائم لنفس الجهة ({dup.Reference}). أكّد أن هذا الطلب لتمويل مختلف أو راجع الطلب القائم." : null;
            }
            case "identity_checked":
            case "coordination_recorded":
            case "response_relayed":
                // Wired with the Rahoon team workspace (Phase 1A steps 6–7).
                return null;
            default:
                throw new InvalidOperationException($"Unknown guard {guard}");
        }
    }

    /// <summary>Q5 interim required set (B13 minimum; contract number optional).</summary>
    public static List<string> MissingFields(Request r)
    {
        var missing = new List<string>();
        if (r.InstitutionId is null && string.IsNullOrWhiteSpace(r.InstitutionOtherName)) missing.Add("الجهة الممولة");
        if (string.IsNullOrWhiteSpace(r.ApplicantFullName)) missing.Add("الاسم الكامل");
        if (r.MonthlyInstallment is null) missing.Add("القسط الشهري");
        if (string.IsNullOrWhiteSpace(r.ArrearsDuration)) missing.Add("مدة التأخر");
        if (string.IsNullOrWhiteSpace(r.PropertyCity)) missing.Add("مدينة العقار");
        if (r.PathPreference is null) missing.Add("ما يناسبك");
        return missing;
    }

    /// <summary>The consent row that still names the request's current lender and has not been withdrawn.</summary>
    public static Task<RequestConsent?> ActiveConsentAsync(RahoonDbContext db, Request r) =>
        db.RequestConsents.Where(c => c.RequestId == r.Id && c.WithdrawnAt == null
                                      && (r.InstitutionId != null ? c.InstitutionId == r.InstitutionId : c.InstitutionId == null && c.RecipientName == r.InstitutionOtherName))
            .OrderByDescending(c => c.OtpVerifiedAt).FirstOrDefaultAsync();

    /// <summary>
    /// V7 interim: another live request of the same applicant for the same lender (and the same contract number when both
    /// are given). Warned and linked, never blocked.
    /// </summary>
    public static async Task<Request?> FindDuplicateAsync(RahoonDbContext db, Request r)
    {
        if (r.InstitutionId is null && string.IsNullOrWhiteSpace(r.InstitutionOtherName)) return null;
        var live = RequestStatusInfo.NonTerminal.Where(s => s != RequestStatus.Draft).ToArray();
        var candidates = await db.Requests.AsNoTracking()
            .Where(x => x.Id != r.Id && x.ApplicantUserId == r.ApplicantUserId && live.Contains(x.Status)
                        && (r.InstitutionId != null ? x.InstitutionId == r.InstitutionId : x.InstitutionId == null && x.InstitutionOtherName == r.InstitutionOtherName))
            .OrderBy(x => x.CreatedAt).ToListAsync();
        var contract = Normalize(r.ContractNumber);
        return candidates.FirstOrDefault(x => contract is null || Normalize(x.ContractNumber) is not { } other || other == contract);
    }

    private static string? Normalize(string? contract)
    {
        var s = new string((contract ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return s.Length == 0 ? null : s;
    }
}
