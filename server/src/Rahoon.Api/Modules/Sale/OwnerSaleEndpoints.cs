using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Sale;

public sealed record OwnerSaleRequestBody(string? Message, decimal? ProposedMinPrice, List<string>? Acknowledgements, string Code);
public sealed record OwnerSaleConsentBody(decimal MinPrice, List<string>? VisitDays, string? VisitWindow, bool Acknowledged, string Code);
public sealed record OwnerSaleWithdrawBody(string? Reason, bool Confirm);
public sealed record OwnerOfferAcceptBody(bool Acknowledged, string Code);
public sealed record OwnerOfferDeclineBody(string? Reason);

/// <summary>
/// Owner portal for the voluntary sale (D15, D16). Plain language; the owner sees prices and terms only —
/// never buyer identities or the broker's internal data. Every consent is a record + OTP, not a licensed signature.
/// </summary>
public static class OwnerSaleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/owner/sale").RequireOwner();
        g.MapGet("", Progress);
        g.MapGet("/explain", Explain);
        g.MapPost("/request/otp", RequestOtp);
        g.MapPost("/request", Request).Idempotent();
        g.MapGet("/consent", ConsentView);
        g.MapPost("/consent/otp", ConsentOtp);
        g.MapPost("/consent", Consent).Idempotent();
        g.MapPost("/withdraw", Withdraw).Idempotent();
        g.MapGet("/offers", Offers);
        g.MapPost("/offers/{offerCode}/accept/otp", AcceptOtp);
        g.MapPost("/offers/{offerCode}/accept", Accept).Idempotent();
        g.MapPost("/offers/{offerCode}/decline", Decline).Idempotent();
    }

    private static async Task<Case> OwnCaseAsync(RahoonDbContext db, RequestContext rc, bool track = false)
    {
        var q = db.Cases.Where(c => c.Id == rc.OwnerCaseId);
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync() ?? throw new ForbiddenException();
    }

    private static string Hash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Device(HttpContext http)
    {
        var ua = http.Request.Headers.UserAgent.ToString();
        return ua.Contains("iPhone") ? "iPhone" : ua.Contains("Android") ? "Android" : ua.Contains("Windows") ? "Windows" : ua.Contains("Mac") ? "Mac" : "متصفح";
    }

    private static async Task<IResult> IssueOtpAsync(RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii, Case c, string context)
    {
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        if (party.PhoneEnc is null) throw new DomainException("no_phone", "لا يوجد رقم جوال مسجل لإرسال الرمز. تواصل مع مسؤول حالتك.");
        var issued = await otp.IssueAsync(OtpPurpose.Consent, pii.Unprotect(party.PhoneEnc), rc.UserId, rc.SessionId, context: context, orgId: c.OrganizationId, caseId: c.Id);
        return Results.Ok(new { destination = issued.DestinationMasked, issued.ResendInSeconds, sandboxCode = issued.SandboxCode });
    }

    private static async Task VerifyOtpAsync(OtpService otp, RequestContext rc, string code, string context)
    {
        if (!await otp.VerifyAsync(OtpPurpose.Consent, rc.SessionId, code, context))
            throw new DomainException("otp_exhausted", "تجاوزت عدد المحاولات. اطلب رمزاً جديداً.", 401);
    }

    // ───────── D15a — explanation ─────────

    private static async Task<IResult> Explain(RahoonDbContext db, RequestContext rc, SaleService sales)
    {
        var c = await OwnCaseAsync(db, rc);
        var latest = await sales.LatestAsync(c.Id);
        var valuation = await sales.ValidValuationAsync(c.Id);
        var reasons = new List<string>();
        if (!CaseStatusInfo.SolutionStates.Contains(c.Status)) reasons.Add("يُتاح طلب البيع الطوعي بعد اكتمال التحقق والتقييم وخلال مرحلة الحلول.");
        if (latest is not null && SaleService.IsOpen(latest.Status)) reasons.Add("لديك طلب بيع طوعي قائم.");
        return Results.Ok(new
        {
            title = "البيع الطوعي", sub = "ما يعنيه لك",
            intro = "تبيع العقار بنفسك بسعر السوق عبر وسيط مرخّص، ويُسدَّد التمويل من ثمن البيع، ويعود لك ما يتبقى.",
            points = new[]
            {
                new { key = "proceeds_repay", icon = "payments", title = "يُسدَّد تمويلك من ثمن البيع", body = "ويعود لك الفائض بعد العمولة والرسوم." },
                new { key = "owner_decides", icon = "person_check", title = "أنت من يقرر", body = "تحدد أقل سعر تقبله، وتُعرض عليك كل العروض." },
                new { key = "privacy", icon = "visibility_off", title = "خصوصيتك محفوظة", body = "المشترون لا يرون اسمك أو وضع التمويل." },
                new { key = "withdrawal_right", icon = "undo", title = "يمكنك الانسحاب", body = "في أي وقت قبل قبول عرض شراء." },
            },
            independentValuation = valuation?.MarketValue,
            canRequest = reasons.Count == 0, reasons,
            primary = "مراجعة الموافقة", talkFirst = "أريد التحدث مع مسؤول حالتي أولاً",
        });
    }

    // ───────── Owner request (documented, OTP) — satisfies the transition guard ─────────

    private static async Task<IResult> RequestOtp(RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii)
    {
        var c = await OwnCaseAsync(db, rc);
        return await IssueOtpAsync(db, rc, otp, pii, c, $"sale:request:{c.Id}");
    }

    private static async Task<IResult> Request(OwnerSaleRequestBody req, HttpContext http, RahoonDbContext db, RequestContext rc, IClock clock,
        OtpService otp, SaleService sales, AuditLog audit, Notifier notifier)
    {
        new Validator()
            .Require(req.Acknowledgements is not null && SaleTexts.RequestAcks.All(req.Acknowledgements.Contains), "acknowledgements", "يرجى تأكيد قراءة النقاط الأربع قبل الطلب.")
            .Require((req.Message?.Length ?? 0) <= 1000, "message", "الرسالة حتى 1000 حرف.")
            .Require(req.ProposedMinPrice is null or > 0, "proposedMinPrice", "أدخل مبلغاً أكبر من صفر أو اتركه فارغاً.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await OwnCaseAsync(db, rc, track: true);
        if (!CaseStatusInfo.SolutionStates.Contains(c.Status))
            throw new DomainException("sale_not_available", "يُتاح طلب البيع الطوعي بعد اكتمال التحقق والتقييم.", StatusCodes.Status409Conflict);
        var latest = await sales.LatestAsync(c.Id);
        if (latest is not null && SaleService.IsOpen(latest.Status))
            throw new ConflictException("sale_exists", "لديك طلب بيع طوعي قائم.");
        await VerifyOtpAsync(otp, rc, req.Code, $"sale:request:{c.Id}");

        var now = clock.UtcNow;
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var text = string.IsNullOrWhiteSpace(req.Message) ? "أطلب فتح مسار البيع الطوعي لعقاري بسعر السوق وسداد التمويل من ثمنه." : req.Message.Trim();
        var consent = new ConsentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, Kind = "sale_consent", AcceptedAt = now, Channel = "portal",
            OtpDestinationMasked = party.PhoneMasked, OtpVerifiedAt = now, Device = Device(http), IpMasked = rc.IpMasked,
            Acknowledgements = [.. SaleTexts.RequestAcks, "explicit_request"],
            TextHash = Hash($"{c.Reference}|sale_request|{text}|{req.ProposedMinPrice}"),
        };
        db.ConsentRecords.Add(consent);

        var valuation = await sales.ValidValuationAsync(c.Id);
        var debt = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id);
        var sale = new VoluntarySale
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, BuyerReference = await sales.NextBuyerReferenceAsync(clock.TodayRiyadh.Year),
            RequestText = text, RequestedAt = now, RequestConsentRecordId = consent.Id, ProposedMinPrice = req.ProposedMinPrice,
            ValuationReportId = valuation?.Id, ValuationAmount = valuation?.MarketValue, ValuationDate = valuation?.ReportDate, ValuerName = valuation?.ValuerName,
            OutstandingDebt = debt?.Total ?? c.OutstandingAmount, DebtAsOf = debt?.AsOf ?? c.OutstandingAsOf,
            AreaLabel = property?.City, OccupancyNote = property?.OccupancyNote,
        };
        db.Set<VoluntarySale>().Add(sale);

        var mgr = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        notifier.Task(c.OrganizationId, c.Id, "طلب المالك فتح مسار البيع الطوعي", mgr, BusinessDays.Add(clock.TodayRiyadh, 3), "sale_request", $"/cases/{c.Reference}/sale/decision", rc.UserId);
        await sales.NotifyManagerAsync(c, "طلب المالك البيع الطوعي", text.Length > 120 ? text[..120] + "…" : text, $"/cases/{c.Reference}/sale/decision");
        await audit.RecordAsync(new AuditEntry("owner.sale_requested", "طلب المالك فتح مسار البيع الطوعي", c.Id, c.Reference, Reason: text,
            Detail: $"عبر البوابة · رمز تحقق إلى {party.PhoneMasked} · سجل موافقة وليس توقيعاً مرخّصاً", Evidence: [consent.TextHash], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            status = sale.Status.ToString(), recordedAt = now,
            message = "وصل طلبك إلى مسؤول حالتك. سيراجعه المصرف، وبعد الاعتماد نطلب موافقتك على نطاق البيع وحدّه الأدنى. يمكنك الانسحاب في أي وقت قبل قبول عرض شراء.",
        });
    }

    // ───────── D15b — formal scoped consent (after the decision is approved) ─────────

    private static async Task<IResult> ConsentView(RahoonDbContext db, RequestContext rc, SaleService sales)
    {
        var c = await OwnCaseAsync(db, rc);
        var s = await sales.LatestAsync(c.Id);
        if (s is null) return Results.Ok(new { available = false, reason = "لا يوجد طلب بيع طوعي." });
        var consent = await sales.ConsentAsync(s.Id);
        return Results.Ok(new
        {
            available = s.Status == SaleStatus.AwaitingConsent,
            status = s.Status.ToString(),
            reason = s.Status switch
            {
                SaleStatus.Requested or SaleStatus.PendingDecision => "طلبك قيد المراجعة لدى المصرف. سنطلب موافقتك على نطاق البيع بعد الاعتماد.",
                SaleStatus.AwaitingConsent => null,
                _ => consent is null ? "لا توجد موافقة مطلوبة الآن." : "سجّلنا موافقتك.",
            },
            independentValuation = s.ValuationAmount,
            suggestedMinPrice = consent?.MinPrice ?? s.ProposedMinPrice,
            visitDayOptions = new[] { "thu", "sat", "sun" }.Select(k => new { key = k, label = SaleTexts.VisitDayLabel(k) }),
            terms = new[] { "التفويض 90 يوماً", "يمكنك الانسحاب حتى قبول عرض شراء", "لا يرى المشترون اسمك أو بياناتك", "تُعرض عليك العروض قبل أي قبول" },
            acknowledgement = "أوافق باختياري على عرض عقاري للبيع بهذه الشروط.",
            signed = consent is null ? null : new
            {
                consent.MinPrice, consent.MandateStart, consent.MandateEnd, visitDays = consent.VisitDays.Select(SaleTexts.VisitDayLabel), consent.VisitWindow,
                status = consent.Status.ToString(), consent.SignedAt,
            },
        });
    }

    private static async Task<IResult> ConsentOtp(RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii, SaleService sales)
    {
        var c = await OwnCaseAsync(db, rc);
        var s = await sales.RequireOpenAsync(c.Id);
        if (s.Status != SaleStatus.AwaitingConsent) throw new ConflictException("consent_not_requested", "لا توجد موافقة مطلوبة منك الآن.");
        return await IssueOtpAsync(db, rc, otp, pii, c, $"sale:consent:{s.Id}");
    }

    private static async Task<IResult> Consent(OwnerSaleConsentBody req, HttpContext http, RahoonDbContext db, RequestContext rc, IClock clock,
        OtpService otp, SaleService sales, AuditLog audit)
    {
        var days = (req.VisitDays ?? []).Distinct().ToList();
        new Validator()
            .Require(req.MinPrice > 0, "minPrice", "أدخل أقل سعر تقبله.")
            .Require(days.Count >= 1 && days.All(SaleTexts.VisitDayKeys.Contains), "visitDays", "اختر يوماً واحداً على الأقل للزيارة.")
            .Require(req.Acknowledged, "acknowledged", "يجب تأكيد الإقرار قبل الموافقة.")
            .Require((req.VisitWindow?.Length ?? 0) <= 60, "visitWindow", "حتى 60 حرفاً.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await OwnCaseAsync(db, rc, track: true);
        var s = await sales.RequireOpenAsync(c.Id, track: true);
        if (s.Status != SaleStatus.AwaitingConsent) throw new ConflictException("consent_not_requested", "لا توجد موافقة مطلوبة منك الآن.");
        await VerifyOtpAsync(otp, rc, req.Code, $"sale:consent:{s.Id}");

        var now = clock.UtcNow;
        var today = clock.TodayRiyadh;
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var previous = await sales.ConsentAsync(s.Id);
        var scope = $"{c.Reference}|{SaleTexts.ConsentTextVersion}|min {req.MinPrice}|mandate 90|visits {string.Join(',', days)} {req.VisitWindow}|withdraw until offer acceptance";
        var record = new ConsentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, Kind = "sale_scope_consent", AcceptedAt = now, Channel = "portal",
            OtpDestinationMasked = party.PhoneMasked, OtpVerifiedAt = now, Device = Device(http), IpMasked = rc.IpMasked,
            Acknowledgements = [SaleTexts.ConsentAck], TextHash = Hash(scope),
        };
        db.ConsentRecords.Add(record);
        var consent = new SaleConsent
        {
            OrganizationId = c.OrganizationId, SaleId = s.Id, CaseId = c.Id, VersionNo = (previous?.VersionNo ?? 0) + 1, MinPrice = req.MinPrice,
            MandateDays = 90, MandateStart = today, MandateEnd = today.AddDays(90), VisitDays = days, VisitWindow = req.VisitWindow?.Trim(),
            ConsentRecordId = record.Id, SignedAt = now, TextHash = record.TextHash,
        };
        db.Set<SaleConsent>().Add(consent);
        s.Status = SaleStatus.Active;
        s.AskingPrice ??= s.ValuationAmount;
        s.VisitTerms ??= "بموعد مسبق، " + string.Join(" و", days.Select(SaleTexts.VisitDayLabel)) + (string.IsNullOrWhiteSpace(req.VisitWindow) ? "" : " " + req.VisitWindow.Trim());
        sales.CreatePrepItems(s, consent);

        var debt = await sales.DebtAsync(c, s);
        var belowCosts = req.MinPrice < debt + SaleCalculator.Money(req.MinPrice * s.BrokerRate);
        await sales.NotifyManagerAsync(c, "وقّعت المالكة موافقة البيع بنطاقه", $"الحد الأدنى {req.MinPrice:N0} · التفويض حتى {consent.MandateEnd:yyyy-MM-dd}", $"/cases/{c.Reference}/sale/file", "ok");
        await audit.RecordAsync(new AuditEntry("owner.sale_consent_signed", "موافقة المالك الصريحة بنطاق البيع", c.Id, c.Reference,
            Detail: $"الحد الأدنى {req.MinPrice:N2} · التفويض 90 يوماً حتى {consent.MandateEnd:yyyy-MM-dd} · رمز تحقق إلى {party.PhoneMasked} · ليس توقيعاً مرخّصاً",
            Evidence: [record.TextHash], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            mandateEnd = consent.MandateEnd, recordedAt = now,
            warning = belowCosts ? "الحد الأدنى الذي اخترته قد لا يغطي المديونية والعمولة؛ سيتواصل معك مسؤول حالتك لتوضيح الأثر." : null,
            message = "سجّلنا موافقتك. يبدأ تجهيز العقار، وتُعرض عليك كل العروض قبل أي قبول. يمكنك الانسحاب حتى قبول عرض شراء.",
        });
    }

    // ───────── D16 — progress & withdrawal ─────────

    private static readonly string[] OwnerStages = ["وافقتِ على البيع", "تجهيز العقار وتصويره", "عرضه على مشترين مؤهلين", "مراجعة العروض", "نقل الملكية واستلام الفائض"];

    private static async Task<IResult> Progress(RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales)
    {
        var c = await OwnCaseAsync(db, rc);
        var s = await sales.LatestAsync(c.Id);
        if (s is null) return Results.Ok(new { hasSale = false });
        var offers = await db.Set<BuyerOffer>().AsNoTracking().Where(o => o.SaleId == s.Id).ToListAsync();
        var shared = offers.Where(o => o.SharedWithOwnerAt != null && o.Status is not (BuyerOfferStatus.Withdrawn or BuyerOfferStatus.Superseded or BuyerOfferStatus.Expired)).ToList();
        var consent = await sales.ConsentAsync(s.Id);
        var stage = SaleService.Stage(s, offers);
        var current = s.Status switch
        {
            SaleStatus.Requested or SaleStatus.PendingDecision or SaleStatus.AwaitingConsent => 0,
            SaleStatus.OfferApproved or SaleStatus.Completed => 4,
            _ => stage switch { <= 2 => 1, 3 => 2, _ => shared.Count > 0 ? 3 : 2 },
        };
        object? next = s.Status switch
        {
            SaleStatus.Requested or SaleStatus.PendingDecision => new { title = "طلبك قيد المراجعة", body = "يراجع المصرف طلب البيع الطوعي. لا شيء مطلوب منك الآن.", cta = (string?)null, route = (string?)null },
            SaleStatus.AwaitingConsent => new { title = "وافقي على نطاق البيع", body = "حددي أقل سعر تقبلينه وأوقات الزيارة، ثم أكدي برمز التحقق.", cta = (string?)"مراجعة الموافقة", route = (string?)"/owner/sale/consent" },
            SaleStatus.Active when shared.Count > 0 => OffersCard(shared),
            SaleStatus.Active => new { title = "نجهّز العقار للعرض", body = "يعمل مسؤول حالتك والوسيط على التجهيز. العرض مضبوط ولا يُنشر للعامة.", cta = (string?)null, route = (string?)null },
            SaleStatus.OfferApproved => new { title = "اعتُمد العرض", body = "نقل الملكية والسداد يتمّان خارج المنصة، ونطلعك على كل خطوة.", cta = (string?)null, route = (string?)null },
            SaleStatus.Completed => new { title = "اكتمل البيع", body = "نراجع التسوية المالية، ثم نغلق الحالة ونرسل لك مستنداتها.", cta = (string?)null, route = (string?)null },
            SaleStatus.Withdrawn => new { title = "انسحبتِ من البيع", body = "عادت حالتك إلى مرحلة الحلول، وسيتواصل معك مسؤول حالتك.", cta = (string?)null, route = (string?)null },
            _ => null,
        };
        var canWithdraw = await sales.CanWithdrawAsync(s);
        return Results.Ok(new
        {
            hasSale = true, status = s.Status.ToString(), statusLabel = SaleService.StatusLabel(s.Status),
            nextStep = next,
            stages = OwnerStages.Select((t, i) => new
            {
                title = t,
                status = s.Status == SaleStatus.Completed || i < current ? "done" : i == current ? "current" : "todo",
                note = i == current && s.Status != SaleStatus.Completed ? "أنتِ هنا" : null,
            }),
            mandateEnd = consent?.MandateEnd, minPrice = consent?.MinPrice,
            canWithdraw, withdrawLabel = canWithdraw ? "أريد الانسحاب من البيع" : null,
            withdrawNote = canWithdraw ? "يمكنك الانسحاب حتى قبول عرض شراء. تعود حالتك إلى مرحلة الحلول." : s.Status is SaleStatus.OfferApproved or SaleStatus.Completed || offers.Any(o => SaleService.AcceptedStates.Contains(o.Status)) ? "لم يعد الانسحاب متاحاً بعد قبول عرض شراء." : null,
        });
    }

    private static object OffersCard(List<BuyerOffer> shared)
    {
        var top = shared.MaxBy(o => o.Price)!;
        var cash = shared.Where(o => o.PaymentMethod == BuyerPaymentMethod.Cash).MaxBy(o => o.Price);
        var n = shared.Count;
        var count = n switch { 1 => "وصل عرض شراء واحد", 2 => "وصل عرضا شراء", <= 10 => $"وصلت {n} عروض شراء", _ => $"وصل {n} عرضاً" };
        var body = $"أعلاها {top.Price:N0} ريال" + (top.PaymentMethod == BuyerPaymentMethod.Cash ? " (نقداً)" : " (بشرط تمويل)")
                   + (cash is not null && cash.Id != top.Id ? $"، وأعلى عرض نقدي {cash.Price:N0}." : ".");
        return new { title = count, body, cta = (string?)"مقارنة العروض", route = (string?)"/owner/sale/offers" };
    }

    private static async Task<IResult> Withdraw(OwnerSaleWithdrawBody req, RahoonDbContext db, RequestContext rc, SaleService sales)
    {
        new Validator().Require(req.Confirm, "confirm", "أكّد رغبتك في الانسحاب.").Require((req.Reason?.Length ?? 0) <= 500, "reason", "حتى 500 حرف.").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await OwnCaseAsync(db, rc, track: true);
        var s = await sales.RequireOpenAsync(c.Id, track: true);
        await sales.WithdrawAsync(c, s, string.IsNullOrWhiteSpace(req.Reason) ? "المالك: الانسحاب من البيع الطوعي" : "المالك: " + req.Reason.Trim());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            status = s.Status.ToString(), caseStatus = CaseStatusInfo.Key(c.Status),
            message = "سجّلنا انسحابك من البيع. أُوقف العرض وسُحب وصول الوسيط، وسيتواصل معك مسؤول حالتك لمناقشة الخيارات الأخرى.",
        });
    }

    // ───────── Anonymised offers & owner acceptance ─────────

    private static async Task<IResult> Offers(RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales)
    {
        var c = await OwnCaseAsync(db, rc);
        var s = await sales.LatestAsync(c.Id);
        if (s is null) return Results.Ok(new { items = Array.Empty<object>() });
        var consent = await sales.ConsentAsync(s.Id);
        var debt = await sales.DebtAsync(c, s);
        var offers = await db.Set<BuyerOffer>().AsNoTracking().Where(o => o.SaleId == s.Id && o.SharedWithOwnerAt != null).OrderBy(o => o.Code).ToListAsync();
        var today = clock.TodayRiyadh;
        return Results.Ok(new
        {
            note = "أسماء المشترين لا تُعرض؛ تُعرض الأسعار والشروط فقط. القرار النهائي يتطلب موافقتك ثم اعتماد المصرف.",
            items = offers.Select(o =>
            {
                var f = SaleCalculator.Offer(o.Price, debt, s.BrokerRate, consent?.MinPrice);
                return new
                {
                    code = o.Code, price = o.Price, payment = SaleService.PaymentLabel(o.PaymentMethod), conditions = o.Conditions ?? "بلا شروط",
                    transferDays = o.ProposedTransferDays, validUntil = o.ValidUntil, expired = o.ValidUntil < today,
                    netAfterCommission = f.NetAfterCommission, estimatedToYou = f.OwnerSurplus, shortfall = f.Shortfall, meetsYourMinimum = f.MeetsMinimum,
                    certainty = SaleService.CertaintyLabel(o.Certainty), status = o.Status.ToString(), statusLabel = SaleService.OfferStatusLabel(o.Status),
                    canAccept = o.Status == BuyerOfferStatus.SharedWithOwner && o.ValidUntil >= today && f.MeetsMinimum && s.Status == SaleStatus.Active,
                };
            }),
        });
    }

    private static async Task<(Case Case, VoluntarySale Sale, BuyerOffer Offer)> LoadOfferAsync(RahoonDbContext db, RequestContext rc, SaleService sales, string offerCode, bool track)
    {
        var c = await OwnCaseAsync(db, rc, track);
        var s = await sales.RequireOpenAsync(c.Id, track);
        var q = db.Set<BuyerOffer>().Where(o => o.SaleId == s.Id && o.Code == offerCode && o.SharedWithOwnerAt != null);
        if (!track) q = q.AsNoTracking();
        var o = await q.FirstOrDefaultAsync() ?? throw new NotFoundException();
        return (c, s, o);
    }

    private static async Task<IResult> AcceptOtp(string offerCode, RahoonDbContext db, RequestContext rc, OtpService otp, PiiProtector pii, SaleService sales)
    {
        var (c, _, o) = await LoadOfferAsync(db, rc, sales, offerCode, track: false);
        if (o.Status != BuyerOfferStatus.SharedWithOwner) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        return await IssueOtpAsync(db, rc, otp, pii, c, $"sale:offer:{o.Id}");
    }

    private static async Task<IResult> Accept(string offerCode, OwnerOfferAcceptBody req, HttpContext http, RahoonDbContext db, RequestContext rc, IClock clock,
        OtpService otp, SaleService sales, AuditLog audit)
    {
        if (!req.Acknowledged) Validate.Throw("acknowledged", "يجب تأكيد الموافقة على العرض.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s, o) = await LoadOfferAsync(db, rc, sales, offerCode, track: true);
        if (s.Status != SaleStatus.Active || o.Status != BuyerOfferStatus.SharedWithOwner) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        if (o.ValidUntil < clock.TodayRiyadh) throw new ConflictException("offer_expired", "انتهت صلاحية هذا العرض.");
        if (await sales.OwnerAcceptedAnyAsync(s.Id)) throw new ConflictException("offer_already_accepted", "سبق أن وافقتِ على عرض آخر.");
        var consent = await sales.RequireSignedConsentAsync(s);
        if (o.Price < consent.MinPrice) throw new DomainException("below_minimum", $"العرض أقل من الحد الأدنى الذي حددتِه ({consent.MinPrice:N0}). قبوله يتطلب موافقة جديدة بنطاق مختلف.");
        await VerifyOtpAsync(otp, rc, req.Code, $"sale:offer:{o.Id}");

        var now = clock.UtcNow;
        var party = await db.Parties.AsNoTracking().FirstAsync(p => p.Id == rc.OwnerPartyId);
        var record = new ConsentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, Kind = "sale_offer_acceptance", AcceptedAt = now, Channel = "portal",
            OtpDestinationMasked = party.PhoneMasked, OtpVerifiedAt = now, Device = Device(http), IpMasked = rc.IpMasked,
            Acknowledgements = ["sale_offer_terms"], TextHash = Hash($"{c.Reference}|{SaleTexts.OfferAcceptanceTextVersion}|{o.Code}|{o.Price}|{o.PaymentMethod}|{o.Conditions}"),
        };
        db.ConsentRecords.Add(record);
        o.Status = BuyerOfferStatus.OwnerAccepted;
        o.OwnerDecisionAt = now;
        o.OwnerConsentRecordId = record.Id;
        var tracked = await sales.ConsentAsync(s.Id, track: true);
        if (tracked is not null) { tracked.Status = SaleConsentStatus.Fulfilled; tracked.FulfilledAt = now; }
        await sales.NotifyManagerAsync(c, $"وافقت المالكة على العرض {o.Code}", $"{o.Price:N0} ر.س · بانتظار طلب اعتماد المصرف", $"/cases/{c.Reference}/sale/offers", "ok");
        await audit.RecordAsync(new AuditEntry("owner.sale_offer_accepted", $"موافقة المالك على عرض الشراء {o.Code}", c.Id, c.Reference,
            Detail: $"{o.Price:N2} ر.س · رمز تحقق إلى {party.PhoneMasked} · ليس توقيعاً مرخّصاً", Evidence: [record.TextHash], OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = o.Status.ToString(), message = "سجّلنا موافقتك على العرض. يراجعه المصرف للاعتماد، ونطلعك على الخطوات التالية." });
    }

    private static async Task<IResult> Decline(string offerCode, OwnerOfferDeclineBody req, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s, o) = await LoadOfferAsync(db, rc, sales, offerCode, track: true);
        if (o.Status != BuyerOfferStatus.SharedWithOwner) throw new ConflictException("offer_closed", "لم يعد هذا العرض قائماً.");
        o.Status = BuyerOfferStatus.OwnerDeclined;
        o.OwnerDecisionAt = clock.UtcNow;
        o.OwnerDeclineReason = req.Reason?.Trim();
        await sales.NotifyManagerAsync(c, $"لم توافق المالكة على العرض {o.Code}", req.Reason, $"/cases/{c.Reference}/sale/offers", "warn");
        await audit.RecordAsync(new AuditEntry("owner.sale_offer_declined", $"المالك لم يوافق على عرض الشراء {o.Code}", c.Id, c.Reference, Reason: req.Reason, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = o.Status.ToString() });
    }
}
