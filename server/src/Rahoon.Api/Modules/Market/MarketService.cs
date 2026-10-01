using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Market;

/// <summary>Shared pieces of the market module: the operator tenant, references, the event log and honest notifications.</summary>
public sealed class MarketService(RahoonDbContext db, RequestContext rc, IClock clock, PiiProtector pii, IServiceScopeFactory scopes,
    ILogger<MarketService> log)
{
    private Guid? _operatorOrgId;

    /// <summary>«فريق رهون» — the operator tenant that owns every market row.</summary>
    public async Task<Guid> OperatorOrgIdAsync()
    {
        if (_operatorOrgId is { } id) return id;
        using var _ = rc.BeginSystemScope();
        _operatorOrgId = await db.Organizations.Where(o => o.Kind == OrganizationKind.Operator).OrderBy(o => o.CreatedAt).Select(o => (Guid?)o.Id).FirstOrDefaultAsync()
                         ?? throw new DomainException("operator_missing", "فريق رهون غير مهيأ بعد.", StatusCodes.Status503ServiceUnavailable);
        return _operatorOrgId.Value;
    }

    /// <summary>Allocates SR-/BR-/OP-/IN-/CM-YYYY-NNNNN atomically (shared counters table, own keys).</summary>
    public async Task<string> NextReferenceAsync(string prefix)
    {
        var year = clock.TodayRiyadh.Year;
        var key = $"market:{prefix}:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO app.reference_counters (key, value) VALUES ({key}, 1)
            ON CONFLICT (key) DO UPDATE SET value = app.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"{prefix}-{year}-{value[0]:D5}";
    }

    public string ActorKind => rc.IsIndividual ? "applicant" : rc.IsOperator ? "team" : "system";
    public string ActorLabel => rc.IsOperator ? rc.UserName : rc.IsIndividual ? "أنت" : "النظام";

    /// <summary>Adds an event to the unit of work. Visible events are the person's in-app notifications.</summary>
    public MarketEvent Event(Guid orgId, string subjectType, Guid subjectId, Guid applicantUserId, string kind, string title,
        bool visible, string? body = null, string? from = null, string? to = null, string? reason = null, object? data = null)
    {
        var e = new MarketEvent
        {
            OrganizationId = orgId, SubjectType = subjectType, SubjectId = subjectId, ApplicantUserId = applicantUserId, Kind = kind, Title = title,
            Body = body, FromStatus = from, ToStatus = to, Reason = reason, ActorKind = ActorKind, ActorUserId = rc.IsAuthenticated ? rc.UserId : null,
            ActorLabel = rc.IsOperator ? rc.UserName : rc.IsIndividual ? "صاحب الطلب" : "النظام", VisibleToApplicant = visible,
            DataJson = data is null ? null : JsonSerializer.Serialize(data), At = clock.UtcNow,
        };
        db.MarketEvents.Add(e);
        return e;
    }

    /// <summary>
    /// Tries the SMS channel after the business change is saved, in its own unit of work, and records the honest result
    /// (simulated in the sandbox, failed, unavailable). A failure never undoes the request or claims delivery. Returns that result
    /// (null when nothing could be attempted or recorded).
    /// </summary>
    public async Task<string?> NotifyBySmsAsync(Guid userId, Guid? eventId, string body)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var other = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
            var gateway = scope.ServiceProvider.GetRequiredService<ISmsGateway>();
            using var _ = other.Request.BeginSystemScope();
            var org = await OperatorOrgIdAsync();
            var profile = await other.IndividualProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile is null) return null;
            string result;
            string? error = null;
            try
            {
                var phone = pii.Unprotect(profile.PhoneEnc);
                var sent = await gateway.SendAsync(phone, body);
                result = sent.Delivered ? "sent" : sent.State == IntegrationState.Simulated ? "simulated" : "unavailable";
            }
            catch (Exception ex)
            {
                result = "failed";
                error = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message;
            }
            other.MarketNotifications.Add(new MarketNotification
            {
                OrganizationId = org, UserId = userId, EventId = eventId, Channel = "sms", DestinationMasked = profile.PhoneMasked, Body = body,
                Result = result, Error = error, At = clock.UtcNow,
            });
            await other.SaveChangesAsync();
            return result;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Market SMS notification could not be recorded");
            return null;
        }
    }

    public string? DisplayName(Guid userId) => db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefault();
}

/// <summary>Allowed status changes of a sale request; anything else is refused with a clear reason (409).</summary>
public static class SaleRequestFlow
{
    public static readonly Dictionary<SaleRequestStatus, string> Labels = new()
    {
        [SaleRequestStatus.Draft] = "مسودة",
        [SaleRequestStatus.Submitted] = "طلب مستلم",
        [SaleRequestStatus.UnderReview] = "قيد المراجعة",
        [SaleRequestStatus.NeedsCompletion] = "يحتاج استكمال",
        [SaleRequestStatus.ApprovedForListing] = "معتمد لإعداد فرصة",
        [SaleRequestStatus.Rejected] = "مرفوض",
        [SaleRequestStatus.Withdrawn] = "مسحوب",
    };

    /// <summary>The owner may edit answers, location, photos and documents in these states (changes during review are logged).</summary>
    public static bool OwnerCanEdit(SaleRequestStatus s) => s is SaleRequestStatus.Draft or SaleRequestStatus.Submitted or SaleRequestStatus.UnderReview or SaleRequestStatus.NeedsCompletion;

    public static bool OwnerCanWithdraw(SaleRequestStatus s) => s is SaleRequestStatus.Draft or SaleRequestStatus.Submitted or SaleRequestStatus.UnderReview or SaleRequestStatus.NeedsCompletion;

    public static void Ensure(SaleRequestStatus current, params SaleRequestStatus[] allowed)
    {
        if (!allowed.Contains(current))
            throw new ConflictException("invalid_status", $"لا يمكن تنفيذ هذا الإجراء والطلب في حالة «{Labels[current]}».");
    }

    /// <summary>Plain next step for the owner (shown on the confirmation and the follow-up file).</summary>
    public static string NextStep(SaleRequestStatus s) => s switch
    {
        SaleRequestStatus.Draft => "أكمل الخطوات وأرسل الطلب.",
        SaleRequestStatus.Submitted => "استلمنا طلبك. يمكنك استكمال ملفك الآن: تفاصيل العقار والموقع والصور والمستندات.",
        SaleRequestStatus.UnderReview => "يراجع فريق رهون طلبك. يمكنك متابعة استكمال ملفك، وسنطلب منك ما ينقص إن وجد.",
        SaleRequestStatus.NeedsCompletion => "طلب الفريق استكمال بعض البيانات. أكملها ثم أرسل الملف للمراجعة.",
        SaleRequestStatus.ApprovedForListing => "اعتمد الفريق طلبك لإعداد فرصة. سنرسل لك ملخص الفرصة لتؤكده قبل أي نشر.",
        SaleRequestStatus.Rejected => "لم يُقبل الطلب. السبب موضح في السجل، ويمكنك التواصل مع الفريق.",
        SaleRequestStatus.Withdrawn => "سحبت هذا الطلب. يمكنك بدء طلب جديد في أي وقت.",
        _ => "",
    };
}

public static class BuyerRequestFlow
{
    public static readonly Dictionary<BuyerRequestStatus, string> Labels = new()
    {
        [BuyerRequestStatus.Draft] = "مسودة",
        [BuyerRequestStatus.Submitted] = "مستلم",
        [BuyerRequestStatus.UnderReview] = "قيد المراجعة",
        [BuyerRequestStatus.NeedsCompletion] = "يحتاج استكمال",
        [BuyerRequestStatus.ApprovedForMatching] = "معتمد للمطابقة",
        [BuyerRequestStatus.Rejected] = "مرفوض",
        [BuyerRequestStatus.Withdrawn] = "مسحوب",
    };

    public static void Ensure(BuyerRequestStatus current, params BuyerRequestStatus[] allowed)
    {
        if (!allowed.Contains(current))
            throw new ConflictException("invalid_status", $"لا يمكن تنفيذ هذا الإجراء والطلب في حالة «{Labels[current]}».");
    }

    public static string NextStep(BuyerRequestStatus s) => s switch
    {
        BuyerRequestStatus.Draft => "أكمل الخطوات وأرسل طلبك.",
        BuyerRequestStatus.Submitted => "استلمنا طلبك. يمكنك تصفح الفرص الآن، وسيراجع الفريق ملفك للمطابقة.",
        BuyerRequestStatus.UnderReview => "يراجع الفريق ملفك ليصبح جاهزًا للمطابقة.",
        BuyerRequestStatus.NeedsCompletion => "طلب الفريق استكمال بعض البيانات. عدّلها ثم أرسل الملف.",
        BuyerRequestStatus.ApprovedForMatching => "ملفك معتمد للمطابقة. هذا لا يعني موافقة أي جهة تمويل.",
        BuyerRequestStatus.Rejected => "لم يُقبل الطلب. السبب موضح في السجل.",
        BuyerRequestStatus.Withdrawn => "سحبت هذا الطلب. يمكنك بدء طلب جديد.",
        _ => "",
    };
}

public static class OpportunityFlow
{
    public static readonly Dictionary<OpportunityStatus, string> Labels = new()
    {
        [OpportunityStatus.Preparing] = "قيد الإعداد",
        [OpportunityStatus.AwaitingOwnerConfirmation] = "بانتظار تأكيد المالك",
        [OpportunityStatus.ReadyToPublish] = "جاهزة للنشر",
        [OpportunityStatus.Published] = "منشورة",
        [OpportunityStatus.Paused] = "موقوفة",
        [OpportunityStatus.ProvisionallyReserved] = "محجوزة مبدئيًا",
        [OpportunityStatus.Closing] = "قيد الإتمام",
        [OpportunityStatus.Completed] = "مكتملة",
        [OpportunityStatus.Withdrawn] = "مسحوبة",
    };

    public static void Ensure(OpportunityStatus current, params OpportunityStatus[] allowed)
    {
        if (!allowed.Contains(current))
            throw new ConflictException("invalid_status", $"لا يمكن تنفيذ هذا الإجراء والفرصة في حالة «{Labels[current]}».");
    }

    /// <summary>Pre-publication checks the team ticks (the system checks the rest: owner confirmation, photos, location).</summary>
    public static readonly (string Key, string Label)[] Checklist =
    [
        ("relationship", "راجعنا علاقة صاحب العقار به ومستنداتها"),
        ("figures", "راجعنا الأرقام الجوهرية ومصدرها وتاريخها"),
        ("photos_location", "راجعنا الصور والموقع ودقة ما يُعرض منه"),
        ("completion_path", "حددنا طريق الإتمام المقترح"),
        ("approvals", "راجعنا موافقات المطور أو جهة التمويل ومتطلبات الإعلان التي تنطبق"),
    ];
}

public static class InterestFlow
{
    public static readonly Dictionary<InterestStatus, string> Labels = new()
    {
        [InterestStatus.Received] = "مستلم",
        [InterestStatus.InFollowUp] = "قيد المتابعة",
        [InterestStatus.Closed] = "مغلق",
        [InterestStatus.Withdrawn] = "مسحوب",
    };
}

public static class ExternalApprovalLabels
{
    public static readonly Dictionary<ExternalApprovalStatus, string> Labels = new()
    {
        [ExternalApprovalStatus.NotRequested] = "لم تُطلب",
        [ExternalApprovalStatus.Requested] = "قيد الطلب",
        [ExternalApprovalStatus.Conditional] = "مشروطة",
        [ExternalApprovalStatus.Approved] = "موافق عليها",
        [ExternalApprovalStatus.Rejected] = "مرفوضة",
        [ExternalApprovalStatus.Expired] = "انتهت صلاحيتها",
    };
}
