using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Requests;

public sealed record RecordOfferBody(
    string? Path, decimal? NewInstallment, int? TermMonths, string? StartText, decimal? SettlementAmount, string? PaymentConditions,
    string? RemainingText, string? SaleTerms, string? Conditions, string? EffectText, string? LenderReference, DateOnly? LenderLetterDate,
    string? LenderValidityText, Guid? LetterDocumentId, bool ShareLetter = true);
public sealed record VerifyOfferBody(string? Decision, string? Reason, List<string>? Checklist);
public sealed record RelayBody(string? Channel, DateTimeOffset? OccurredAt, string? Counterpart, string? Summary, string? ApplicantText);
public sealed record CloseBody(string? OutcomeCode, string? Summary);
public sealed record RespondBody(string? Kind, string? Text, string? Code);

/// <summary>
/// Offers and responses (Phase 1A step 7, design requests D-4 T05–T07 and D-5). The lender decides its offer outside
/// Rahoon; the team records it from the lender's letter, a second member verifies it (never the recorder, with an MFA
/// step-up), and only then does the individual see it. The individual decides the response; accepting is an
/// SMS-confirmed consent record, and a decline triggers no action against them. The team relays the response over the
/// documented manual channel, then continues coordinating or closes the request.
/// </summary>
public static class OfferEndpoints
{
    public static readonly string[] Paths = ["p1", "p2", "p3"];
    public static readonly string[] VerifyChecklist = ["amounts_match_letter", "terms_match_letter", "reference_and_date_match", "effect_text_accurate"];
    public static readonly string[] OutcomeCodes = ["offer_accepted", "offer_declined", "lender_no_offer", "other"];
    public const string AcceptTextVersion = "offer-acceptance-draft-2026-09";

    public static string AcceptText(string lender, string lenderReference) =>
        $"أوافق على عرض {lender} كما سجّله فريق رهون من خطابها (مرجع {lenderReference})، وأفهم أن هذه موافقة مسجلة في منصة رهون وليست توقيعاً ملزماً، " +
        "وأن فريق رهون سينقلها إلى جهتي الممولة، وأن تنفيذ العرض يتم مع جهتي الممولة.";

    public static void Map(IEndpointRouteBuilder app)
    {
        var team = app.MapGroup("/api/team").RequireOrg(OrganizationKind.Operator);
        team.MapGet("/verify", VerifyQueue).RequirePermission(P.RequestOfferVerify);
        team.MapPost("/requests/{reference}/offers", Record).RequirePermission(P.RequestOfferRecord).Idempotent();
        team.MapPost("/requests/{reference}/offers/{offerId:guid}/verify", Verify).RequirePermission(P.RequestOfferVerify).Idempotent();
        team.MapPost("/requests/{reference}/relay", Relay).RequirePermission(P.RequestResponseRelay).Idempotent();
        team.MapPost("/requests/{reference}/continue", Continue).RequirePermission(P.RequestResponseRelay).Idempotent();
        team.MapPost("/requests/{reference}/close", Close).RequirePermission(P.RequestClose).Idempotent();

        var my = app.MapGroup("/api/my/requests/{reference}/offer").RequireIndividual();
        my.MapPost("/accept/otp", AcceptOtp);
        my.MapPost("/respond", Respond).Idempotent();
    }

    /// <summary>Offer as the individual sees it (published only). Internal verification details are not projected.</summary>
    internal static object? ApplicantOffer(RequestOffer? o, RequestDocument? letter) => o is null ? null : new
    {
        o.Id, o.VersionNo, o.Path, o.NewInstallment, o.TermMonths, o.StartText, o.SettlementAmount, o.PaymentConditions, o.RemainingText,
        o.SaleTerms, o.Conditions, o.EffectText, o.LenderReference, o.LenderLetterDate, o.LenderValidityText, o.PublishedAt,
        letterVersionId = o.ShareLetterWithApplicant && letter?.Visibility == RequestDocumentVisibility.ApplicantAndTeam ? letter.CurrentVersionId : null,
        verified = o.VerifiedAt is not null,
    };

    internal static object TeamOffer(RequestOffer o, Guid me) => new
    {
        o.Id, o.VersionNo, o.Path, status = o.Status.ToString().ToLowerInvariant(), o.NewInstallment, o.TermMonths, o.StartText, o.SettlementAmount,
        o.PaymentConditions, o.RemainingText, o.SaleTerms, o.Conditions, o.EffectText, o.LenderReference, o.LenderLetterDate, o.LenderValidityText,
        o.LetterDocumentId, o.ShareLetterWithApplicant, o.RecordedByLabel, o.RecordedAt, recordedByMe = o.RecordedByUserId == me,
        o.VerifiedByLabel, o.VerifiedAt, o.VerificationChecklist, o.ReturnReason, o.PublishedAt,
    };

    // ───────── T05 record ─────────

    private static async Task<IResult> Record(string reference, RecordOfferBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var path = b.Path;
        var v = new Validator()
            .Require(path is not null && Paths.Contains(path), "path", "اختر مسار العرض.")
            .Require(!string.IsNullOrWhiteSpace(b.EffectText), "effectText", "اكتب «أثره عليك» بلغة يفهمها العميل.")
            .Require(!string.IsNullOrWhiteSpace(b.LenderReference), "lenderReference", "أدخل مرجع خطاب الجهة.")
            .Require(b.LenderLetterDate is { } d && d <= clock.TodayRiyadh, "lenderLetterDate", "أدخل تاريخ خطاب الجهة.")
            .Require(b.LetterDocumentId is not null, "letterDocumentId", "أرفق خطاب الجهة أو اختره من المستندات (إلزامي).");
        if (path == "p1")
            v.Require(b.NewInstallment is > 0, "newInstallment", "أدخل القسط الجديد كما في الخطاب.").Require(b.TermMonths is > 0 and <= 600, "termMonths", "أدخل المدة بالأشهر.");
        if (path == "p2") v.Require(b.SettlementAmount is > 0, "settlementAmount", "أدخل مبلغ التسوية كما في الخطاب.");
        if (path == "p3") v.Require(!string.IsNullOrWhiteSpace(b.SaleTerms), "saleTerms", "اكتب شروط تنسيق البيع كما في الخطاب.");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
        if (r.Status != RequestStatus.LenderCoordination) throw new ConflictException("state", "يُسجَّل العرض أثناء التنسيق مع الجهة فقط.");
        if (await RequestWorkflow.ActiveConsentAsync(db, r) is null) throw new DomainException("consent_required", "لا توجد موافقة سارية من العميل.");
        var letter = await db.RequestDocuments.FirstOrDefaultAsync(d => d.Id == b.LetterDocumentId && d.RequestId == r.Id && d.Kind == "lender_letter");
        if (letter is null) Validate.Throw("letterDocumentId", "اختر خطاب الجهة من مستندات هذا الطلب.");

        foreach (var old in await db.RequestOffers.Where(o => o.RequestId == r.Id && (o.Status == RequestOfferStatus.PendingVerification || o.Status == RequestOfferStatus.Returned)).ToListAsync())
            old.Status = RequestOfferStatus.Superseded;
        var version = (await db.RequestOffers.Where(o => o.RequestId == r.Id).MaxAsync(o => (int?)o.VersionNo) ?? 0) + 1;
        var offer = new RequestOffer
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, VersionNo = version, Path = path!,
            NewInstallment = path == "p1" ? b.NewInstallment : null, TermMonths = path == "p1" ? b.TermMonths : null, StartText = Clean(b.StartText, 300),
            SettlementAmount = path == "p2" ? b.SettlementAmount : null, PaymentConditions = Clean(b.PaymentConditions, 1000), RemainingText = Clean(b.RemainingText, 1000),
            SaleTerms = path == "p3" ? Clean(b.SaleTerms, 2000) : null, Conditions = Clean(b.Conditions, 2000), EffectText = Clean(b.EffectText, 2000)!,
            LenderReference = Clean(b.LenderReference, 100)!, LenderLetterDate = b.LenderLetterDate!.Value, LenderValidityText = Clean(b.LenderValidityText, 300),
            LetterDocumentId = letter!.Id, ShareLetterWithApplicant = b.ShareLetter, RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName, RecordedAt = clock.UtcNow,
        };
        db.RequestOffers.Add(offer);
        svc.AddUpdate(r, "offer_recorded", $"سُجّل عرض الجهة v{version} وينتظر التحقق", authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.offer_recorded", $"تسجيل عرض الجهة v{version} ({path})",
            detail: $"مرجع الجهة {offer.LenderReference}", evidence: [letter.Id.ToString()]));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { offer.Id, offer.VersionNo });
    }

    // ───────── T06 verify ─────────

    private static async Task<IResult> VerifyQueue(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.RequestOffers.AsNoTracking().Where(o => o.Status == RequestOfferStatus.PendingVerification)
            .Join(db.Requests.Include(r => r.Institution), o => o.RequestId, r => r.Id, (o, r) => new { o, r })
            .OrderBy(x => x.o.RecordedAt).ToListAsync();
        return Results.Ok(rows.Select(x => new
        {
            x.r.Reference, institutionName = x.r.InstitutionDisplayName, offerId = x.o.Id, x.o.VersionNo, x.o.Path, x.o.RecordedByLabel, x.o.RecordedAt,
            recordedByMe = x.o.RecordedByUserId == rc.UserId,
        }));
    }

    private static async Task<IResult> Verify(string reference, Guid offerId, VerifyOfferBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = b.Decision;
        new Validator()
            .Require(decision is "publish" or "return", "decision", "اختر: اعتماد ونشر، أو إعادة مع سبب.")
            .Require(decision != "return" || !string.IsNullOrWhiteSpace(b.Reason), "reason", "اكتب سبب الإعادة لمن سجّل العرض.")
            .Require(decision != "publish" || VerifyChecklist.All(i => b.Checklist?.Contains(i) == true), "checklist", "أكمل قائمة المطابقة مع خطاب الجهة قبل النشر.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        var offer = await db.RequestOffers.FirstOrDefaultAsync(o => o.Id == offerId && o.RequestId == r.Id) ?? throw new NotFoundException();
        if (offer.Status != RequestOfferStatus.PendingVerification) throw new ConflictException("state", "العرض ليس بانتظار التحقق.");
        if (offer.RecordedByUserId == rc.UserId)
        {
            await audit.RecordBlockedAsync(RequestWorkflow.Entry(r, "request.offer_verify_blocked", "محاولة التحقق من عرض سجّله العضو نفسه",
                detail: "المانع: المتحقق لا يكون من سجّل العرض. لم يُنشر.", blocked: true));
            throw new ForbiddenException("لا يمكنك التحقق من عرض سجّلته بنفسك؛ يتحقق منه عضو آخر.");
        }
        var now = clock.UtcNow;
        if (decision == "return")
        {
            offer.Status = RequestOfferStatus.Returned;
            offer.ReturnReason = Clean(b.Reason, 1000);
            svc.AddUpdate(r, "offer_returned", $"أُعيد عرض v{offer.VersionNo} للتصحيح", offer.ReturnReason, authorKind: "team", authorLabel: rc.UserName, authorUserId: rc.UserId, visible: false);
            notifier.Notify(offer.RecordedByUserId, r.OrganizationId, "request", $"أُعيد العرض على {r.Reference}", offer.ReturnReason, $"/team/requests/{r.Reference}", tone: "warn");
            await audit.RecordAsync(RequestWorkflow.Entry(r, "request.offer_returned", $"إعادة عرض v{offer.VersionNo} مع سبب", reason: offer.ReturnReason));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { status = "returned" });
        }

        offer.VerifiedByUserId = rc.UserId;
        offer.VerifiedByLabel = rc.UserName;
        offer.VerifiedAt = now;
        offer.VerificationChecklist = [.. b.Checklist!.Where(VerifyChecklist.Contains).Distinct()];
        await workflow.TransitionAsync(r, "publish_offer", reason: $"عرض v{offer.VersionNo}", expected: RequestStatus.LenderCoordination);
        foreach (var old in await db.RequestOffers.Where(o => o.RequestId == r.Id && o.Status == RequestOfferStatus.Published).ToListAsync())
            old.Status = RequestOfferStatus.Superseded;
        offer.Status = RequestOfferStatus.Published;
        offer.PublishedAt = now;
        if (offer.ShareLetterWithApplicant)
        {
            var letter = await db.RequestDocuments.FirstAsync(d => d.Id == offer.LetterDocumentId);
            letter.Visibility = RequestDocumentVisibility.ApplicantAndTeam;
        }
        svc.AddUpdate(r, "offer_published", "وصل عرض من جهتك الممولة",
            "سجّل فريق رهون العرض من خطاب جهتك، وتحقق منه عضو آخر من الفريق. راجعه، والقرار لك.", authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "وصل عرض من جهتك الممولة", "راجع العرض في صفحة طلبك. القرار لك.", $"/my/requests/{r.Reference}/offer");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.offer_published", $"تحقق من عرض v{offer.VersionNo} ونشره للعميل",
            detail: $"سجّله {offer.RecordedByLabel} · تحقق منه {rc.UserName}", evidence: offer.VerificationChecklist));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = "published" });
    }

    // ───────── T07 relay, continue, close ─────────

    private static async Task<IResult> Relay(string reference, RelayBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestService svc, IClock clock, AuditLog audit)
    {
        var now = clock.UtcNow;
        new Validator()
            .Require(b.Channel is not null && TeamRequestEndpoints.Channels.Contains(b.Channel), "channel", "اختر قناة التواصل.")
            .Require(b.OccurredAt is { } at && at <= now.AddMinutes(5) && at >= now.AddYears(-1), "occurredAt", "أدخل تاريخ ووقت النقل (ليس في المستقبل).")
            .Require(!string.IsNullOrWhiteSpace(b.Counterpart), "counterpart", "اكتب الطرف لدى الجهة.")
            .Require(!string.IsNullOrWhiteSpace(b.Summary), "summary", "اكتب ملخص النقل.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
        if (r.Status != RequestStatus.ResponseRecorded) throw new ConflictException("state", "لا يوجد رد بانتظار النقل.");
        if (await RequestWorkflow.ActiveConsentAsync(db, r) is null) throw new DomainException("consent_required", "لا توجد موافقة سارية من العميل.");
        var response = await db.RequestResponses.Where(x => x.RequestId == r.Id).OrderByDescending(x => x.At).FirstAsync();
        if (response.RelayedAt is not null) throw new ConflictException("already", "نُقل هذا الرد مسبقاً.");
        var text = Clean(b.ApplicantText, 1000) ?? "نقلنا ردك إلى جهتك الممولة، وسنبلغك بالخطوة التالية.";
        var entry = new CoordinationEntry
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = "response_relay", Channel = b.Channel!,
            OccurredAt = b.OccurredAt!.Value, Counterpart = Clean(b.Counterpart, 200)!, Summary = Clean(b.Summary, 2000)!, VisibleToApplicant = true, ApplicantText = text,
            RecordedByUserId = rc.UserId, RecordedByLabel = rc.UserName,
        };
        db.CoordinationEntries.Add(entry);
        response.RelayedAt = now;
        response.RelayedByUserId = rc.UserId;
        response.RelayEntryId = entry.Id;
        svc.AddUpdate(r, "relay", "نقلنا ردك إلى جهتك الممولة", text, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.response_relayed", $"نقل رد العميل ({response.Reference}) إلى الجهة", detail: $"{entry.Channel} · {entry.Counterpart}"));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { entry.Id });
    }

    private static async Task<IResult> Continue(string reference, NextStepBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
        await workflow.TransitionAsync(r, "continue_coordination", expected: RequestStatus.ResponseRecorded, nextStep: b.NextStep);
        svc.AddUpdate(r, "status", "يتابع فريق رهون التنسيق مع جهتك الممولة", authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    /// <summary>Outcome + summary shown to the individual. Execution of an accepted offer is tracked in Phase 1A-2.</summary>
    private static async Task<IResult> Close(string reference, CloseBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        RequestWorkflow workflow, RequestService svc, Notifier notifier)
    {
        var summary = Clean(b.Summary, 1500);
        new Validator()
            .Require(b.OutcomeCode is not null && OutcomeCodes.Contains(b.OutcomeCode), "outcomeCode", "اختر نتيجة الطلب.")
            .Require(summary is not null, "summary", "اكتب ملخص النتيجة كما سيراه العميل.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = await access.ForTeamAsync(reference);
        if (r.AssignedCoordinatorId != rc.UserId && !rc.Has(P.RequestViewAll)) throw new ForbiddenException("هذا الطلب مسند لعضو آخر.");
        await workflow.TransitionAsync(r, "close", summary);
        r.OutcomeCode = b.OutcomeCode;
        r.OutcomeSummary = summary;
        svc.AddUpdate(r, "closed", "أُغلق طلبك", summary, authorKind: "team", authorLabel: "فريق رهون", authorUserId: rc.UserId);
        notifier.Notify(r.ApplicantUserId, null, "request", "أُغلق طلبك", summary, $"/my/requests/{r.Reference}");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = RequestStatusInfo.Key(r.Status) });
    }

    // ───────── individual: D09 accept (SMS consent) and D08 / decline ─────────

    private static async Task<RequestOffer> PublishedOfferAsync(RahoonDbContext db, Request r) =>
        await db.RequestOffers.Where(o => o.RequestId == r.Id && o.Status == RequestOfferStatus.Published).OrderByDescending(o => o.VersionNo).FirstOrDefaultAsync()
        ?? throw new ConflictException("no_offer", "لا يوجد عرض منشور على هذا الطلب.");

    private static async Task<IResult> AcceptOtp(string reference, RequestAccess access, RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii)
    {
        var r = await access.ForApplicantAsync(reference, write: true, track: false);
        if (r.Status != RequestStatus.OfferAvailable) throw new ConflictException("state", "لا يوجد عرض بانتظار ردك.");
        var offer = await PublishedOfferAsync(db, r);
        var profile = await db.IndividualProfiles.AsNoTracking().FirstAsync(p => p.UserId == rc.UserId);
        var issued = await otp.IssueAsync(OtpPurpose.Consent, pii.Unprotect(profile.PhoneEnc), rc.UserId, rc.SessionId, context: $"offer-accept:{offer.Id}");
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task<IResult> Respond(string reference, RespondBody b, RequestAccess access, RahoonDbContext db, RequestContext rc,
        OtpService otp, RequestWorkflow workflow, RequestService svc, IClock clock, AuditLog audit)
    {
        var kind = b.Kind;
        var text = Clean(b.Text, 2000);
        new Validator()
            .Require(kind is "accept" or "decline" or "question" or "counter", "kind", "اختر ردك على العرض.")
            .Require(kind is not ("question" or "counter") || text is not null, "text", "اكتب سؤالك أو اقتراحك.")
            .ThrowIfInvalid();
        var r = await access.ForApplicantAsync(reference, write: true);
        if (r.Status != RequestStatus.OfferAvailable) throw new ConflictException("state", "لا يوجد عرض بانتظار ردك.");
        var offer = await PublishedOfferAsync(db, r);
        var now = clock.UtcNow;
        string? consentText = null;
        if (kind == "accept")
        {
            if (!await otp.VerifyAsync(OtpPurpose.Consent, rc.SessionId, b.Code ?? "", $"offer-accept:{offer.Id}"))
                throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. اطلب رمزاً جديداً.", StatusCodes.Status423Locked);
            consentText = AcceptText(r.InstitutionDisplayName, offer.LenderReference);
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        var n = await db.RequestResponses.CountAsync(x => x.RequestId == r.Id) + 1;
        var response = new RequestResponse
        {
            OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, OfferId = offer.Id, Reference = $"{r.Reference}-R{n}",
            Kind = kind!, Text = text, ConsentTextSnapshot = consentText, OtpVerifiedAt = kind == "accept" ? now : null, IpMasked = rc.IpMasked, At = now,
        };
        db.RequestResponses.Add(response);
        await workflow.TransitionAsync(r, "respond", reason: kind, expected: RequestStatus.OfferAvailable);
        var title = kind switch
        {
            "accept" => "سجّلنا موافقتك على العرض",
            "decline" => "سجّلنا أن العرض لا يناسبك",
            "question" => "سجّلنا سؤالك عن العرض",
            _ => "سجّلنا اقتراحك على العرض",
        };
        svc.AddUpdate(r, "response", title, text, authorKind: "applicant");
        await audit.RecordAsync(RequestWorkflow.Entry(r, "request.response_recorded", $"{title} ({response.Reference})",
            detail: kind == "accept" ? $"{AcceptTextVersion} · مؤكدة برمز الجوال" : null));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { response.Reference, response.Kind, response.At, status = RequestStatusInfo.Key(r.Status) });
    }

    private static string? Clean(string? s, int max) => TeamRequestEndpoints.Clean(s, max);
}
