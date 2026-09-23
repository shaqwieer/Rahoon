using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Ecosystem;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Sale;

public sealed record SaleDecisionBody(string Reason, decimal? OtherFeesEstimate, string? ExpectedStatus);
public sealed record PrepItemBody(string Status, string? Memo, DateOnly? ScheduledOn, string? Responsible);
public sealed record ListingBody(decimal? AskingPrice, string? AreaLabel, int? EvacuationDays, string? VisitTerms, string? OccupancyNote, int? ApprovedPhotoCount);
public sealed record ListingReviewBody(string? Note);
public sealed record BrokerAssignBody(Guid ProviderOrganizationId);
public sealed record BuyerOfferBody(decimal Price, string PaymentMethod, string? ProofOfFunds, string? Conditions, int ProposedTransferDays, DateOnly ValidUntil, string? Certainty, bool BuyerNdaConfirmed);
public sealed record OfferApprovalRequestBody(string Recommendation, bool Attested);
public sealed record TrackingBody(string Status, DateOnly? ActualDate, DateOnly? ExpectedDate, string? Reference, string? Memo);
public sealed record SaleCompleteBody(string? Reason, string? ExpectedStatus);

/// <summary>
/// Lender side of the voluntary sale (L27–L33). Every route loads the case through <see cref="CaseAccess"/>;
/// status changes go through the transition engine; money figures come from <see cref="SaleCalculator"/>.
/// </summary>
public static class SaleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}/sale").RequireOrg(OrganizationKind.Lender).RequirePermission(P.CaseView);
        g.MapGet("", Overview);
        g.MapGet("/decision", DecisionView);
        g.MapPost("/decision", SubmitDecision).RequirePermission(P.SaleManage).Idempotent();
        g.MapGet("/file", FileView);
        g.MapPut("/prep-items/{key}", UpdatePrepItem).RequirePermission(P.SaleManage).Idempotent();
        g.MapGet("/listing", ListingView);
        g.MapPut("/listing", UpdateListing).RequirePermission(P.SaleManage).Idempotent();
        g.MapPost("/listing/compliance-review", ReviewListing).RequirePermission(P.TemplatePublish).Idempotent();
        g.MapGet("/broker-candidates", BrokerCandidates);
        g.MapPost("/broker-assignment", AssignBroker).RequirePermission(P.SaleManage).Idempotent();
        g.MapGet("/offers", OffersComparison);
        g.MapPost("/offers", AddOffer).RequirePermission(P.SaleManage).Idempotent();
        g.MapPost("/offers/share-with-owner", ShareWithOwner).RequirePermission(P.SaleManage).Idempotent();
        g.MapPost("/offers/{code}/approval-request", RequestOfferApproval).RequirePermission(P.SaleManage).Idempotent();
        g.MapGet("/tracking", Tracking);
        g.MapPut("/tracking/{key}", UpdateTracking).RequirePermission(P.SaleManage).Idempotent();
        g.MapPost("/complete", Complete).RequirePermission(P.SaleManage).Idempotent();
    }

    private static async Task<(Case Case, VoluntarySale Sale)> LoadAsync(string reference, CaseAccess access, SaleService sales, bool track = false)
    {
        var c = await access.GetAsync(reference, track);
        var s = await sales.LatestAsync(c.Id, track) ?? throw new DomainException("no_sale", "لا يوجد مسار بيع طوعي لهذه الحالة.", StatusCodes.Status404NotFound);
        return (c, s);
    }

    private static string Fmt(DateTimeOffset? t) => t is { } v ? v.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd HH:mm") : "—";

    // ───────── Aggregate ─────────

    private static async Task<IResult> Overview(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, SaleService sales)
    {
        var c = await access.GetAsync(reference, track: false);
        var s = await sales.LatestAsync(c.Id);
        if (s is null) return Results.Ok(new { hasSale = false, tab = (object?)null });
        var offers = await db.Set<BuyerOffer>().AsNoTracking().Where(o => o.SaleId == s.Id).ToListAsync();
        var consent = await sales.ConsentAsync(s.Id);
        var stage = SaleService.Stage(s, offers);
        return Results.Ok(new
        {
            hasSale = true, tab = new { key = "sale", label = "البيع الطوعي" },
            status = s.Status.ToString(), statusLabel = SaleService.StatusLabel(s.Status), buyerReference = s.BuyerReference,
            stage, stageNames = SaleService.StageNames,
            route = stage switch { 0 => "decision", 1 or 2 => "file", 3 => "listing", 4 or 5 => "offers", _ => "tracking" },
            consent = consent is null ? null : new { consent.MinPrice, consent.MandateEnd, status = consent.Status.ToString() },
            offers = offers.Count, canManage = rc.Has(P.SaleManage),
        });
    }

    // ───────── L27 — decision ─────────

    private static async Task<IResult> DecisionView(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, SaleService sales)
    {
        var c = await access.GetAsync(reference, track: false);
        var s = await sales.LatestAsync(c.Id);
        var checks = await sales.DecisionPrerequisitesAsync(c, s);
        var valuation = await sales.ValidValuationAsync(c.Id);
        var debt = await db.DebtSnapshots.AsNoTracking().Where(d => d.CaseId == c.Id && d.IsCurrent).OrderByDescending(d => d.AsOf).FirstOrDefaultAsync();
        var rate = s?.BrokerRate ?? 0.02m;
        var fees = s?.OtherFeesEstimate ?? 8_000m;
        SaleEstimate? est = valuation is null || debt is null ? null : SaleCalculator.Estimate(valuation.MarketValue, debt.Total, rate, fees);
        var pending = s?.DecisionApprovalRequestId is { } rid ? await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(a => a.Id == rid) : null;
        var approver = await sales.ResolveApproverAsync(c.OrganizationId, [rc.UserId], seniorOnly: false);
        var blocked = checks.Where(x => !x.Ok).Select(x => $"{x.Title}: {x.Memo}").ToList();
        if (s is not null && s.Status != SaleStatus.Requested) blocked.Add($"حالة المسار: {SaleService.StatusLabel(s.Status)}");
        return Results.Ok(new
        {
            request = s is null ? null : new
            {
                title = "طلب المالكة", quote = s.RequestText,
                meta = $"عبر البوابة · {Fmt(s.RequestedAt)} · موثق في السجل", proposedMinPrice = s.ProposedMinPrice,
            },
            prerequisites = checks.Select(x => new { ok = x.Ok, title = x.Title, memo = x.Memo, link = x.Link })
                .Append(new { ok = false, title = "موافقة المالكة المكتوبة بنطاق البيع", memo = "تُطلب بعد اعتماد القرار", link = (string?)null }),
            estimate = est is null ? null : new
            {
                title = "التقدير المالي (قبل العروض)",
                lines = new object[]
                {
                    new { key = "market_value", label = "القيمة السوقية (التقييم)", amount = est.MarketValue, source = $"تقييم {valuation!.ReportDate:yyyy-MM-dd}", assumption = false },
                    new { key = "outstanding", label = "المديونية القائمة", amount = est.OutstandingDebt, source = $"{debt!.Source} · {debt.AsOf.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}", assumption = false },
                    new { key = "commission", label = $"عمولة وساطة تقديرية {rate * 100:0.##}%", amount = est.BrokerCommission, source = "اتفاقية الوسيط (افتراض)", assumption = true },
                    new { key = "other_fees", label = "رسوم أخرى تقديرية", amount = est.OtherFees, source = "افتراض", assumption = true },
                    new { key = "surplus", label = est.ExpectedShortfall > 0 ? "العجز المتوقع" : "الفائض المتوقع للمالكة", amount = est.ExpectedShortfall > 0 ? est.ExpectedShortfall : est.ExpectedSurplus, source = "تقديري قبل العروض", assumption = true, highlight = true },
                }.Concat(s?.ProposedMinPrice is { } mp ? [new { key = "owner_minimum", label = "الحد الأدنى المقترح من المالكة", amount = mp, source = "يغطي المديونية والتكاليف", assumption = false }] : Array.Empty<object>()).ToArray(),
                note = $"التقدير مبني على تقييم {valuation.ReportDate:yyyy-MM-dd}. الأرقام النهائية تعتمد على العرض المقبول.",
            },
            review = new
            {
                title = "مراجعة قرار فتح مسار البيع",
                effects = new[]
                {
                    "تنتقل الحالة إلى «بيع طوعي»؛ تُعلَّق العروض الودية القائمة.",
                    "يُطلب من المالكة توقيع موافقة صريحة بنطاق البيع وحدّها الأدنى.",
                    "لا يُكلّف وسيط ولا يُعرض العقار قبل الموافقة.",
                    "يمكن للمالكة الانسحاب حتى قبول عرض شراء.",
                },
                approver = approver is null ? null : $"يعتمده: {approver.Value.Name} + مراجعة القانونية",
            },
            pending = pending is null ? null : new { pending.Id, status = pending.Status.ToString(), submittedAt = pending.SubmittedAt, reason = pending.SubmitterNote },
            canSubmit = rc.Has(P.SaleManage) && blocked.Count == 0, blockers = blocked,
        });
    }

    private static async Task<IResult> SubmitDecision(string reference, SaleDecisionBody req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, SaleService sales, AuditLog audit, Notifier notifier)
    {
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 10, "reason", "السبب إلزامي (10 أحرف على الأقل).")
            .Require(req.OtherFeesEstimate is null or >= 0, "otherFeesEstimate", "الرسوم لا تكون سالبة.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (req.ExpectedStatus is { } es && CaseStatusInfo.Key(c.Status) != es)
            throw new ConflictException("stale_state", "تغيّرت حالة الحالة منذ فتحها. حدّث الصفحة لرؤية الوضع الحالي.");
        if (s.Status != SaleStatus.Requested) throw new ConflictException("decision_exists", "القرار مُرسل للاعتماد أو المسار ليس بانتظار قرار.");
        var failures = (await sales.DecisionPrerequisitesAsync(c, s)).Where(x => !x.Ok).Select(x => $"{x.Title}: {x.Memo}").ToList();
        if (!CaseStatusInfo.SolutionStates.Contains(c.Status)) failures.Add($"لا يُفتح مسار البيع والحالة «{CaseStatusInfo.Of(c.Status).LabelAr}».");
        if (failures.Count > 0)
        {
            await audit.RecordBlockedAsync(new AuditEntry("sale.decision_blocked", "محاولة إرسال قرار البيع الطوعي محجوبة", c.Id, c.Reference,
                Detail: "المانع: " + string.Join(" · ", failures), OrganizationId: c.OrganizationId));
            throw new DomainException("guard_failed", "لا يمكن إرسال قرار البيع الطوعي الآن.", StatusCodes.Status422UnprocessableEntity, failures);
        }
        var approver = await sales.ResolveApproverAsync(c.OrganizationId, [rc.UserId], seniorOnly: false)
            ?? throw new DomainException("no_approver", "لا يوجد معتمد متاح لطلبات البيع (غير مُعِدّ الطلب).");

        var valuation = await sales.ValidValuationAsync(c.Id);
        var debt = await sales.DebtAsync(c, s);
        if (req.OtherFeesEstimate is { } fees) s.OtherFeesEstimate = fees;
        s.ValuationReportId = valuation!.Id;
        s.ValuationAmount = valuation.MarketValue;
        s.ValuationDate = valuation.ReportDate;
        s.ValuerName = valuation.ValuerName;
        s.OutstandingDebt = debt;
        var est = SaleCalculator.Estimate(valuation.MarketValue, debt, s.BrokerRate, s.OtherFeesEstimate);
        var now = clock.UtcNow;
        var reason = req.Reason.Trim();
        var request = new ApprovalRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.Sale, SubjectId = s.Id, SubjectVersionNo = 1,
            Title = "اعتماد فتح مسار البيع الطوعي", PreparedByUserId = rc.UserId, SubmittedByUserId = rc.UserId, SubmittedAt = now,
            SubmitterNote = reason, SubmitterAttested = true, AssignedApproverUserId = approver.UserId, RequiredTier = approver.Tier,
            Amount = valuation.MarketValue, DueOn = BusinessDays.Add(clock.TodayRiyadh, 3),
            Evidence = ["طلب المالك عبر البوابة (سجل موافقة + رمز تحقق)", $"تقييم {valuation.ReportDate:yyyy-MM-dd} · {valuation.MarketValue:N0}", "مراجعة الرهن والقيود", $"الفائض المتوقع {est.ExpectedSurplus:N0} (تقديري)"],
        };
        db.ApprovalRequests.Add(request);
        s.Status = SaleStatus.PendingDecision;
        s.DecisionReason = reason;
        s.DecisionPreparedByUserId = rc.UserId;
        s.DecisionSubmittedAt = now;
        s.DecisionApprovalRequestId = request.Id;
        notifier.Notify(approver.UserId, c.OrganizationId, "approval", $"طلب اعتماد فتح مسار البيع الطوعي · {c.Reference}", reason, $"/approvals/sale/{request.Id}", c.Id);
        notifier.Task(c.OrganizationId, c.Id, "اعتماد قرار البيع الطوعي", approver.UserId, request.DueOn, "sale_approval", $"/approvals/sale/{request.Id}", rc.UserId);
        await audit.RecordAsync(new AuditEntry("sale.decision_submitted", "إرسال قرار فتح مسار البيع الطوعي للاعتماد", c.Id, c.Reference, Reason: reason,
            Detail: $"يعتمده: {approver.Name} · الفائض المتوقع {est.ExpectedSurplus:N2} (تقديري)", Evidence: request.Evidence, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { approvalRequestId = request.Id, approver = approver.Name, status = s.Status.ToString(), expectedSurplus = est.ExpectedSurplus });
    }

    // ───────── L28 + L29 — consent & preparation ─────────

    private static async Task<IResult> FileView(string reference, CaseAccess access, RahoonDbContext db, SaleService sales)
    {
        var (c, s) = await LoadAsync(reference, access, sales);
        var consent = await sales.ConsentAsync(s.Id);
        var party = await db.Parties.AsNoTracking().Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.DisplayName).FirstOrDefaultAsync();
        var items = await db.Set<SalePrepItem>().AsNoTracking().Where(i => i.SaleId == s.Id).OrderBy(i => i.SortOrder).ToListAsync();
        var locked = consent is null || s.Status == SaleStatus.AwaitingConsent;
        return Results.Ok(new
        {
            consent = consent is null
                ? new { state = "awaiting", title = "موافقة المالكة الصريحة", text = "بانتظار الموافقة — يحجب التجهيز والعرض وتكليف الوسيط.", scope = (object?)null }
                : new
                {
                    state = consent.Status.ToString(), title = "موافقة المالكة الصريحة",
                    text = $"وقّعت {party} داخل البوابة {Fmt(consent.SignedAt)} · رمز تحقق",
                    scope = (object?)new
                    {
                        minPrice = consent.MinPrice, mandate = $"{consent.MandateDays} يوماً حتى {consent.MandateEnd:yyyy-MM-dd}", mandateEnd = consent.MandateEnd,
                        visits = "بموعد مسبق، " + string.Join(" و", consent.VisitDays.Select(SaleTexts.VisitDayLabel)) + (consent.VisitWindow is null ? "" : " " + consent.VisitWindow),
                        withdrawal = consent.Status == SaleConsentStatus.Signed ? "متاح حتى قبول عرض" : consent.Status == SaleConsentStatus.Fulfilled ? "انتهى بقبول عرض" : "—",
                        textVersion = consent.TextVersion, textHash = consent.TextHash,
                    },
                },
            occupancy = new { title = "الإشغال والانتقال", text = s.OccupancyNote ?? $"مهلة إخلاء {s.EvacuationDays} يوماً بعد نقل الملكية، مذكورة في شروط العرض للمشترين." },
            photoNote = "التصوير بموافقة المالكة فقط، بلا أشخاص أو متعلقات شخصية أو أرقام الوحدة.",
            preparation = new
            {
                locked, lockedReason = locked ? "بانتظار موافقة المالكة — يحجب التجهيز" : null,
                counter = $"{items.Count(i => i.Status == PrepItemStatus.Done)} من {items.Count}",
                items = items.Select(i => new
                {
                    i.Key, i.Title, status = i.Status.ToString(), i.Memo, i.ScheduledOn, responsible = SaleService.ResponsibleLabel(i.Responsible),
                    icon = i.Status switch { PrepItemStatus.Done => "check_circle", PrepItemStatus.Scheduled => "schedule", _ => "radio_button_unchecked" },
                }),
            },
        });
    }

    private static async Task<IResult> UpdatePrepItem(string reference, string key, PrepItemBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, SaleService sales, AuditLog audit)
    {
        if (!Enum.TryParse<PrepItemStatus>(req.Status, true, out var status) || !Enum.IsDefined(status)) Validate.Throw("status", "اختر الحالة.");
        if (key == "listing_review") throw new ConflictException("compliance_item", "هذا البند يُستكمل بمراجعة الامتثال لملخص العرض.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "التجهيز متاح بعد موافقة المالكة وحتى قبول عرض.");
        await sales.RequireSignedConsentAsync(s);
        var item = await db.Set<SalePrepItem>().FirstOrDefaultAsync(i => i.SaleId == s.Id && i.Key == key) ?? throw new NotFoundException();
        if (status == PrepItemStatus.Scheduled && req.ScheduledOn is null) Validate.Throw("scheduledOn", "حدد الموعد.");
        item.Status = status;
        item.Memo = req.Memo?.Trim() ?? item.Memo;
        item.ScheduledOn = req.ScheduledOn ?? item.ScheduledOn;
        if (!string.IsNullOrWhiteSpace(req.Responsible)) item.Responsible = req.Responsible;
        item.UpdatedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("sale.prep_item", $"تحديث بند التجهيز «{item.Title}»", c.Id, c.Reference, Detail: SaleService.ResponsibleLabel(item.Responsible) + " · " + status, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { item.Key, status = item.Status.ToString() });
    }

    // ───────── L30 — controlled listing & disclosure ─────────

    internal static async Task<ListingSource> ListingSourceAsync(RahoonDbContext db, Case c, VoluntarySale s, SaleService sales)
    {
        var owner = await db.Parties.AsNoTracking().Where(p => p.CaseId == c.Id && p.IsPrimary).Select(p => p.FullName).FirstOrDefaultAsync() ?? "";
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var p = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var consent = await sales.ConsentAsync(s.Id);
        return new ListingSource(owner, c.Reference, org, s.BuyerReference, (p?.Type ?? "عقار").Split('·')[0].Trim(),
            p?.City ?? c.City ?? "", p?.District, s.AreaLabel ?? p?.City ?? c.City ?? "", p?.ShortLabel, s.ApprovedPhotoCount,
            s.AskingPrice, consent?.MinPrice, await sales.DebtAsync(c, s), c.ArrearsInstallments, s.EvacuationDays, s.VisitTerms,
            p?.LandAreaM2, p?.BuiltAreaM2, p?.YearBuilt);
    }

    private static async Task<IResult> ListingView(string reference, CaseAccess access, RahoonDbContext db, SaleService sales)
    {
        var (c, s) = await LoadAsync(reference, access, sales);
        var src = await ListingSourceAsync(db, c, s, sales);
        var reviewer = s.ListingReviewedByUserId is { } r ? await db.Users.Where(u => u.Id == r).Select(u => u.FullName).FirstOrDefaultAsync() : null;
        return Results.Ok(new
        {
            heading = "العرض المضبوط والإفصاح",
            sub = "ليس إعلاناً عاماً. يُشارك الملخص مع الوسيط المكلّف، ويعرضه على مشترين مؤهلين بعد إقرار السرية.",
            matrix = DisclosurePolicy.MatrixView(),
            listing = new
            {
                status = s.ListingStatus.ToString(), s.AskingPrice, s.AreaLabel, s.EvacuationDays, s.VisitTerms, s.ApprovedPhotoCount,
                complianceReview = s.ListingReviewedAt is null ? null : new { by = reviewer, at = s.ListingReviewedAt },
            },
            buyerPreview = DisclosurePolicy.Project(src, DisclosureAudience.QualifiedBuyer),
            brokerView = DisclosurePolicy.Project(src, DisclosureAudience.Broker),
            photoNote = "صور العقار المعتمدة — بلا أشخاص",
        });
    }

    private static async Task<IResult> UpdateListing(string reference, ListingBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, SaleService sales, AuditLog audit)
    {
        new Validator()
            .Require(req.AskingPrice is null or > 0, "askingPrice", "السعر المطلوب أكبر من صفر.")
            .Require(req.EvacuationDays is null or (>= 0 and <= 365), "evacuationDays", "بين 0 و365 يوماً.")
            .Require(req.ApprovedPhotoCount is null or (>= 0 and <= 50), "approvedPhotoCount", "بين 0 و50.")
            .Require((req.AreaLabel?.Length ?? 0) <= 80, "areaLabel", "حتى 80 حرفاً.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "الملخص يُعدّ بعد موافقة المالكة وحتى قبول عرض.");
        await sales.RequireSignedConsentAsync(s);
        s.AskingPrice = req.AskingPrice ?? s.AskingPrice;
        s.AreaLabel = string.IsNullOrWhiteSpace(req.AreaLabel) ? s.AreaLabel : req.AreaLabel.Trim();
        s.EvacuationDays = req.EvacuationDays ?? s.EvacuationDays;
        s.VisitTerms = req.VisitTerms?.Trim() ?? s.VisitTerms;
        s.OccupancyNote = req.OccupancyNote?.Trim() ?? s.OccupancyNote;
        s.ApprovedPhotoCount = req.ApprovedPhotoCount ?? s.ApprovedPhotoCount;
        s.ListingPreparedByUserId = rc.UserId;
        // Any change to the summary requires a fresh compliance review before buyers see it.
        if (s.ListingStatus == ListingStatus.ComplianceReviewed)
        {
            s.ListingStatus = ListingStatus.Draft;
            s.ListingReviewedAt = null;
            s.ListingReviewedByUserId = null;
            var item = await db.Set<SalePrepItem>().FirstOrDefaultAsync(i => i.SaleId == s.Id && i.Key == "listing_review");
            if (item is not null) item.Status = PrepItemStatus.NotStarted;
        }
        await audit.RecordAsync(new AuditEntry("sale.listing_updated", "تحديث ملخص العرض المضبوط", c.Id, c.Reference, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = s.ListingStatus.ToString() });
    }

    /// <summary>Compliance reviews the buyer-facing summary before it is shared (reviewer ≠ the person who prepared it).</summary>
    private static async Task<IResult> ReviewListing(string reference, ListingReviewBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "لا يوجد ملخص عرض قيد المراجعة.");
        await sales.RequireSignedConsentAsync(s);
        if (s.ListingPreparedByUserId == rc.UserId) throw new ForbiddenException("مراجعة الملخص لغير من أعدّه (فصل المهام).");
        if (s.AskingPrice is null) throw new ConflictException("listing_incomplete", "حدد السعر المطلوب قبل المراجعة.");
        s.ListingStatus = ListingStatus.ComplianceReviewed;
        s.ListingReviewedByUserId = rc.UserId;
        s.ListingReviewedAt = clock.UtcNow;
        var item = await db.Set<SalePrepItem>().FirstOrDefaultAsync(i => i.SaleId == s.Id && i.Key == "listing_review");
        if (item is not null) { item.Status = PrepItemStatus.Done; item.Memo = $"روجع {clock.TodayRiyadh:yyyy-MM-dd}"; item.UpdatedByUserId = rc.UserId; }
        await audit.RecordAsync(new AuditEntry("sale.listing_reviewed", "مراجعة الامتثال لملخص العرض", c.Id, c.Reference, Reason: req.Note, Detail: "لا يكشف هوية المالك أو حدّه الأدنى أو المديونية", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = s.ListingStatus.ToString() });
    }

    // ───────── L31 — broker assignment ─────────

    private static async Task<IResult> BrokerCandidates(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, ProviderDirectory directory, SaleService sales)
    {
        var (c, s) = await LoadAsync(reference, access, sales);
        var rows = await directory.InstitutionDirectoryAsync(c.OrganizationId, ProviderType.Broker);
        var consent = await sales.ConsentAsync(s.Id);
        return Results.Ok(new
        {
            sub = "من دليل مقدمي الخدمة المعتمدين لدى المصرف",
            note = "الوسيط يرى ملف البيع فقط، ويُسحب وصوله عند انتهاء التفويض أو قبول عرض.",
            assigned = s.BrokerOrganizationId,
            items = rows.Select(r => new
            {
                id = r.ProviderOrganizationId, name = r.Name, license = r.LicenseText, licenseState = r.LicenseState, licenseTone = r.LicenseTone,
                completedDeals = r.Performance.Delivered, avgDays = r.Performance.AvgDays, commission = r.CommissionRate,
                selectable = r.Assignable, disabledReason = r.Assignable ? null : "موقوف ولا يُكلّف",
                warning = r.LicenseState == "expiring_soon" ? "الترخيص قريب الانتهاء" : null,
                expiresBeforeMandate = consent is not null && r.LicenseExpires is { } e && e < consent.MandateEnd,
            }),
        });
    }

    private static async Task<IResult> AssignBroker(string reference, BrokerAssignBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock,
        ProviderDirectory directory, SaleService sales, AuditLog audit, Notifier notifier)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "تكليف الوسيط متاح بعد موافقة المالكة وحتى قبول عرض.");
        var consent = await sales.RequireSignedConsentAsync(s);
        if (s.BrokerAssignmentId is not null) throw new ConflictException("broker_assigned", "كُلّف وسيط لهذا البيع مسبقاً.");
        var row = (await directory.InstitutionDirectoryAsync(c.OrganizationId, ProviderType.Broker)).FirstOrDefault(r => r.ProviderOrganizationId == req.ProviderOrganizationId)
                  ?? throw new DomainException("not_in_directory", "الوسيط ليس ضمن دليل مقدمي الخدمة المعتمدين لدى المنشأة.");
        if (!row.Assignable) throw new DomainException("provider_suspended", "الوسيط موقوف ولا يُكلّف (الترخيص منتهٍ أو التسجيل غير مقبول).");

        var now = clock.UtcNow;
        var src = await ListingSourceAsync(db, c, s, sales);
        var accessEnds = new DateTimeOffset(consent.MandateEnd.ToDateTime(new TimeOnly(23, 59)), TimeSpan.FromHours(3)).ToUniversalTime();
        var assignment = new ProviderAssignment
        {
            OrganizationId = c.OrganizationId, Reference = await sales.NextAssignmentReferenceAsync(clock.TodayRiyadh.Year), CaseId = c.Id,
            ProviderOrganizationId = req.ProviderOrganizationId, Type = AssignmentType.Brokerage, Title = $"وساطة بيع طوعي {s.BuyerReference}",
            PropertyLabel = $"{src.PropertyType} · {src.AreaLabel}", Status = AssignmentStatus.InProgress, DueOn = consent.MandateEnd,
            Scope = ["sale_file"], FeesLabel = $"عمولة {(row.CommissionRate ?? s.BrokerRate) * 100:0.##}% من سعر البيع", CreatedByUserId = rc.UserId,
            AccessExpiresAt = accessEnds,
        };
        db.Assignments.Add(assignment);
        s.BrokerAssignmentId = assignment.Id;
        s.BrokerOrganizationId = req.ProviderOrganizationId;
        if (row.CommissionRate is { } rate) s.BrokerRate = rate;
        var brokerUsers = await db.Memberships.IgnoreQueryFilters().Where(m => m.OrganizationId == req.ProviderOrganizationId && m.Status == MembershipStatus.Active).Select(m => m.UserId).ToListAsync();
        foreach (var u in brokerUsers)
            notifier.Notify(u, req.ProviderOrganizationId, "assignment", $"تكليف وساطة جديد {s.BuyerReference}", "ملف بيع مضبوط — ليس إعلاناً عاماً", $"/broker/sales/{s.BuyerReference}");
        await audit.RecordAsync(new AuditEntry("sale.broker_assigned", $"تكليف {row.Name} بوساطة البيع", c.Id, c.Reference,
            Detail: $"{assignment.Reference} · نطاق: ملف البيع فقط · الوصول حتى {consent.MandateEnd:yyyy-MM-dd} · {assignment.FeesLabel}" + (row.LicenseState == "expiring_soon" ? " · تحذير: الترخيص قريب الانتهاء" : ""),
            OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { assignment = assignment.Reference, broker = row.Name, accessExpiresAt = assignment.AccessExpiresAt, commissionRate = s.BrokerRate });
    }

    // ───────── L32 — buyer offers & comparison ─────────

    internal static BuyerOffer NewOffer(VoluntarySale s, BuyerOfferBody req, string side, Guid userId, DateTimeOffset now, string code)
    {
        if (!Enum.TryParse<BuyerPaymentMethod>(req.PaymentMethod, true, out var method) || !Enum.IsDefined(method)) Validate.Throw("paymentMethod", "اختر طريقة الدفع.");
        var certainty = OfferCertainty.Medium;
        if (req.Certainty is { Length: > 0 } cs && (!Enum.TryParse(cs, true, out certainty) || !Enum.IsDefined(certainty))) Validate.Throw("certainty", "اختر درجة اليقين.");
        return new BuyerOffer
        {
            OrganizationId = s.OrganizationId, SaleId = s.Id, CaseId = s.CaseId, Code = code, Price = SaleCalculator.Money(req.Price), PaymentMethod = method,
            ProofOfFunds = req.ProofOfFunds?.Trim(), Conditions = req.Conditions?.Trim(), ProposedTransferDays = req.ProposedTransferDays, ValidUntil = req.ValidUntil,
            Certainty = certainty, BuyerNdaConfirmed = req.BuyerNdaConfirmed, EnteredBySide = side, EnteredByUserId = userId, ReceivedAt = now,
        };
    }

    internal static void ValidateOffer(BuyerOfferBody req, DateOnly today) => new Validator()
        .Require(req.Price > 0, "price", "أدخل سعر العرض.")
        .Require(req.ProposedTransferDays is >= 1 and <= 365, "proposedTransferDays", "مدة الإفراغ بين 1 و365 يوماً.")
        .Require(req.ValidUntil >= today, "validUntil", "صلاحية العرض لا تكون في الماضي.")
        .Require(req.BuyerNdaConfirmed, "buyerNdaConfirmed", "العروض من مشترين مؤهلين بعد إقرار السرية فقط.")
        .Require((req.Conditions?.Length ?? 0) <= 500 && (req.ProofOfFunds?.Length ?? 0) <= 200, "conditions", "النص طويل.")
        .ThrowIfInvalid();

    internal static async Task<string> NextOfferCodeAsync(RahoonDbContext db, Guid saleId) =>
        $"OF-{await db.Set<BuyerOffer>().CountAsync(o => o.SaleId == saleId) + 1:D2}";

    private static async Task<IResult> AddOffer(string reference, BuyerOfferBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales, AuditLog audit)
    {
        ValidateOffer(req, clock.TodayRiyadh);
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "العروض تُسجل بعد موافقة المالكة وحتى قبول عرض.");
        await sales.RequireSignedConsentAsync(s);
        var offer = NewOffer(s, req, "lender", rc.UserId, clock.UtcNow, await NextOfferCodeAsync(db, s.Id));
        db.Set<BuyerOffer>().Add(offer);
        await audit.RecordAsync(new AuditEntry("sale.offer_received", $"تسجيل عرض شراء {offer.Code}", c.Id, c.Reference, Detail: $"{offer.Price:N2} · {SaleService.PaymentLabel(offer.PaymentMethod)} · أدخله المصرف", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { code = offer.Code, status = offer.Status.ToString() });
    }

    private static async Task<IResult> OffersComparison(string reference, CaseAccess access, RahoonDbContext db, IClock clock, SaleService sales)
    {
        var (c, s) = await LoadAsync(reference, access, sales);
        var consent = await sales.ConsentAsync(s.Id);
        var debt = await sales.DebtAsync(c, s);
        var today = clock.TodayRiyadh;
        var offers = await db.Set<BuyerOffer>().AsNoTracking().Where(o => o.SaleId == s.Id).OrderBy(o => o.Code).ToListAsync();
        var valid = offers.Where(o => o.ValidUntil >= today && o.Status is not (BuyerOfferStatus.Withdrawn or BuyerOfferStatus.Superseded or BuyerOfferStatus.Rejected or BuyerOfferStatus.OwnerDeclined)).ToList();
        return Results.Ok(new
        {
            brokerRate = s.BrokerRate, outstandingDebt = debt, ownerMinimum = consent?.MinPrice,
            bestValidUntil = valid.Count == 0 ? (DateOnly?)null : valid.Max(o => o.ValidUntil),
            note = "أسماء المشترين مخفية عن المالكة؛ تعرض لها الأسعار والشروط فقط. القرار النهائي يتطلب موافقتها واعتماد المصرف.",
            offers = offers.Select(o =>
            {
                var f = SaleCalculator.Offer(o.Price, debt, s.BrokerRate, consent?.MinPrice);
                return new
                {
                    code = o.Code, buyer = $"{o.BuyerLabel} · {(o.EnteredBySide == "broker" ? "عبر الوسيط" : "عبر المصرف")}", price = o.Price,
                    payment = SaleService.PaymentLabel(o.PaymentMethod), paymentMethod = o.PaymentMethod.ToString(), proofOfFunds = o.ProofOfFunds ?? "—",
                    conditions = string.IsNullOrWhiteSpace(o.Conditions) ? "بلا شروط" : o.Conditions, transferDays = o.ProposedTransferDays, o.ValidUntil,
                    netAfterCommission = f.NetAfterCommission, commission = f.BrokerCommission, lenderRecovery = f.LenderRecovery,
                    ownerSurplus = f.OwnerSurplus, shortfall = f.Shortfall, meetsMinimum = f.MeetsMinimum,
                    certainty = SaleService.CertaintyLabel(o.Certainty), certaintyTone = o.Certainty switch { OfferCertainty.High => "ok", OfferCertainty.Low => "err", _ => "neutral" },
                    status = o.Status.ToString(), statusLabel = SaleService.OfferStatusLabel(o.Status), expired = o.ValidUntil < today,
                    sharedWithOwner = o.SharedWithOwnerAt != null,
                    canRequestApproval = o.Status == BuyerOfferStatus.OwnerAccepted && o.ValidUntil >= today && f.MeetsMinimum,
                    approvalBlockedReason = o.Status != BuyerOfferStatus.OwnerAccepted ? "بانتظار موافقة المالكة على هذا العرض (توافق قبل اعتماد المصرف)."
                        : o.ValidUntil < today ? "انتهت صلاحية العرض." : !f.MeetsMinimum ? "العرض أقل من الحد الأدنى للمالكة." : null,
                };
            }),
        });
    }

    private static async Task<IResult> ShareWithOwner(string reference, CaseAccess access, RahoonDbContext db, IClock clock, SaleService sales, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.Active) throw new ConflictException("sale_not_active", "لا توجد عروض للمشاركة.");
        var today = clock.TodayRiyadh;
        var offers = await db.Set<BuyerOffer>().Where(o => o.SaleId == s.Id && o.Status == BuyerOfferStatus.Received && o.ValidUntil >= today).ToListAsync();
        if (offers.Count == 0) throw new ConflictException("no_valid_offers", "لا يوجد عرض صالح جديد لمشاركته مع المالكة.");
        foreach (var o in offers) { o.Status = BuyerOfferStatus.SharedWithOwner; o.SharedWithOwnerAt = clock.UtcNow; }
        await sales.NotifyOwnerAsync(c, offers.Count == 1 ? "وصل عرض شراء" : $"وصلت {offers.Count} عروض شراء", "راجع المقارنة في صفحة البيع.", "/owner/sale/offers");
        await audit.RecordAsync(new AuditEntry("sale.offers_shared", "إرسال مقارنة العروض للمالكة", c.Id, c.Reference,
            Detail: string.Join(" · ", offers.Select(o => o.Code)) + " · بلا أسماء المشترين", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { shared = offers.Select(o => o.Code) });
    }

    /// <summary>Maker side of the offer approval: after the owner accepted the offer, submit the bank approval request.</summary>
    private static async Task<IResult> RequestOfferApproval(string reference, string code, OfferApprovalRequestBody req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, SaleService sales, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(req.Attested, "attested", "أقرّ بمراجعة الأثر على المديونية والفائض.")
            .Require(!string.IsNullOrWhiteSpace(req.Recommendation) && req.Recommendation.Trim().Length >= 10, "recommendation", "التوصية إلزامية (10 أحرف على الأقل).").ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        var o = await db.Set<BuyerOffer>().FirstOrDefaultAsync(x => x.SaleId == s.Id && x.Code == code) ?? throw new NotFoundException();
        var consent = await sales.ConsentAsync(s.Id) ?? throw new ConflictException("consent_required", "لا توجد موافقة من المالكة.");
        var failures = new List<string>();
        if (s.Status != SaleStatus.Active) failures.Add("مسار البيع ليس في مرحلة العروض.");
        if (o.Status != BuyerOfferStatus.OwnerAccepted) failures.Add("المالكة لم توافق على هذا العرض بعد (توافق قبل اعتماد المصرف).");
        if (o.ValidUntil < clock.TodayRiyadh) failures.Add("انتهت صلاحية العرض.");
        if (o.Price < consent.MinPrice) failures.Add("العرض أقل من الحد الأدنى للمالكة.");
        if (failures.Count > 0) throw new DomainException("guard_failed", "لا يمكن طلب اعتماد العرض الآن.", StatusCodes.Status422UnprocessableEntity, failures);

        var debt = await sales.DebtAsync(c, s);
        var f = SaleCalculator.Offer(o.Price, debt, s.BrokerRate, consent.MinPrice);
        var approver = await sales.ResolveApproverAsync(c.OrganizationId, [rc.UserId], seniorOnly: f.Shortfall > 0)
            ?? throw new DomainException("no_approver", "لا يوجد معتمد متاح غير مُعِدّ الطلب.");
        var now = clock.UtcNow;
        var request = new ApprovalRequest
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.Sale, SubjectId = o.Id, SubjectVersionNo = 1,
            Title = $"اعتماد عرض الشراء {o.Code}", PreparedByUserId = rc.UserId, SubmittedByUserId = rc.UserId, SubmittedAt = now,
            SubmitterNote = req.Recommendation.Trim(), SubmitterAttested = true, AssignedApproverUserId = approver.UserId, RequiredTier = approver.Tier,
            Amount = o.Price, DueOn = BusinessDays.Add(clock.TodayRiyadh, 2),
            Evidence = [$"موافقة المالكة على {o.Code} (رمز تحقق)", $"سداد المديونية {f.LenderRecovery:N2}", f.Shortfall > 0 ? $"عجز {f.Shortfall:N2}" : $"فائض للمالكة {f.OwnerSurplus:N2} (تقديري)", $"عمولة الوسيط {f.BrokerCommission:N2}"],
        };
        db.ApprovalRequests.Add(request);
        o.Status = BuyerOfferStatus.PendingApproval;
        o.Recommendation = req.Recommendation.Trim();
        o.ApprovalRequestId = request.Id;
        notifier.Notify(approver.UserId, c.OrganizationId, "approval", $"طلب اعتماد عرض الشراء {o.Code} · {c.Reference}", req.Recommendation.Trim(), $"/approvals/sale/{request.Id}", c.Id);
        await audit.RecordAsync(new AuditEntry("sale.offer_approval_requested", $"طلب اعتماد عرض الشراء {o.Code}", c.Id, c.Reference, Reason: req.Recommendation.Trim(),
            Detail: $"يعتمده: {approver.Name}", Evidence: request.Evidence, OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { approvalRequestId = request.Id, approver = approver.Name, figures = f });
    }

    // ───────── L33 — tracking & completion ─────────

    private static async Task<IResult> Tracking(string reference, CaseAccess access, RahoonDbContext db, SaleService sales)
    {
        var (c, s) = await LoadAsync(reference, access, sales);
        var steps = await db.Set<SaleTrackingStep>().AsNoTracking().Where(x => x.SaleId == s.Id).OrderBy(x => x.SortOrder).ToListAsync();
        var offer = s.AcceptedOfferId is { } oid ? await db.Set<BuyerOffer>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == oid) : null;
        object? approved = null, distribution = null;
        if (offer is not null)
        {
            var debt = await sales.DebtAsync(c, s);
            var f = SaleCalculator.Offer(offer.Price, debt, s.BrokerRate, null);
            var approval = offer.ApprovalRequestId is { } aid ? await db.ApprovalRequests.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid) : null;
            var approverName = approval?.DecidedByUserId is { } du ? await db.Users.Where(u => u.Id == du).Select(u => u.FullName).FirstOrDefaultAsync() : null;
            approved = new
            {
                title = $"العرض المعتمد {offer.Code}", summary = $"{offer.Price:N0} ر.س · {SaleService.PaymentLabel(offer.PaymentMethod)}",
                meta = $"اعتمده {approverName} {approval?.DecidedAt?.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · وافقت المالكة {offer.OwnerDecisionAt?.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
            };
            distribution = new
            {
                title = "التوزيع المتوقع (تقديري)",
                lines = new[]
                {
                    new { label = "سداد المديونية", amount = f.LenderRecovery },
                    new { label = $"عمولة الوسيط {s.BrokerRate * 100:0.##}%", amount = f.BrokerCommission },
                    new { label = f.Shortfall > 0 ? "العجز المتبقي على المالكة" : "المتبقي للمالكة", amount = f.Shortfall > 0 ? f.Shortfall : f.OwnerSurplus },
                },
                note = "يُنفَّذ عبر القنوات النظامية خارج المنصة؛ رهون تسجّل المراجع فقط.",
            };
        }
        return Results.Ok(new
        {
            steps = steps.Select(x => new
            {
                x.Key, x.Title, x.Memo, status = x.Status.ToString(),
                icon = x.Status switch { TrackingStatus.Done => "check_circle", TrackingStatus.Pending => "schedule", _ => "radio_button_unchecked" },
                source = x.Source == TrackingSource.Internal ? "داخلي" : "خارجي — يدوي", sourceKey = x.Source.ToString(),
                date = x.ActualDate?.ToString("yyyy-MM-dd") ?? (x.ExpectedDate is { } e ? $"متوقع {e:yyyy-MM-dd}" : "—"), x.Reference,
            }),
            approvedOffer = approved, distribution,
            externalNote = "الحالات الخارجية (نقل الملكية، فك الرهن) تُدخل يدوياً بمصدرها ووقتها — لا تكامل مع أنظمة رسمية في هذه المرحلة.",
            canComplete = s.Status == SaleStatus.OfferApproved && steps.Any(x => x.Key == "payment_received" && x.Status == TrackingStatus.Done) && steps.Any(x => x.Key == "lien_release" && x.Status == TrackingStatus.Done),
        });
    }

    private static async Task<IResult> UpdateTracking(string reference, string key, TrackingBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales, AuditLog audit)
    {
        if (!Enum.TryParse<TrackingStatus>(req.Status, true, out var status) || !Enum.IsDefined(status)) Validate.Throw("status", "اختر الحالة.");
        if (key is "owner_offer_consent" or "bank_approval" or "reconciliation") throw new ConflictException("system_step", "تُسجَّل هذه الخطوة تلقائياً من المنصة.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.OfferApproved) throw new ConflictException("sale_not_executing", "التتبع متاح بعد اعتماد عرض الشراء.");
        var step = await db.Set<SaleTrackingStep>().FirstOrDefaultAsync(x => x.SaleId == s.Id && x.Key == key) ?? throw new NotFoundException();
        var v = new Validator();
        if (status == TrackingStatus.Done)
        {
            v.Require(req.ActualDate is { } d && d <= clock.TodayRiyadh, "actualDate", "تاريخ التنفيذ الفعلي إلزامي ولا يكون في المستقبل.");
            if (step.Source == TrackingSource.ExternalManual && key is "payment_received" or "lien_release" or "surplus_transfer")
                v.Require(!string.IsNullOrWhiteSpace(req.Reference), "reference", "مرجع الجهة الخارجية إلزامي (يُحفظ كما ورد).");
        }
        if (status == TrackingStatus.Pending) v.Require(req.ExpectedDate is not null, "expectedDate", "حدد التاريخ المتوقع.");
        v.ThrowIfInvalid();
        step.Status = status;
        step.ActualDate = status == TrackingStatus.Done ? req.ActualDate : null;
        step.ExpectedDate = req.ExpectedDate ?? step.ExpectedDate;
        step.Reference = req.Reference?.Trim() ?? step.Reference;
        step.Memo = key == "debt_letter" && status == TrackingStatus.Done ? $"صالح 30 يوماً حتى {req.ActualDate!.Value.AddDays(30):yyyy-MM-dd}" : req.Memo?.Trim() ?? step.Memo;
        step.EnteredByUserId = rc.UserId;
        step.EnteredAt = clock.UtcNow;
        await audit.RecordAsync(new AuditEntry("sale.tracking", $"تتبع البيع: {step.Title} — {status}", c.Id, c.Reference,
            Detail: $"{(step.Source == TrackingSource.Internal ? "داخلي" : "خارجي — يدوي")}" + (step.Reference is null ? "" : $" · المرجع {step.Reference}"), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { step.Key, status = step.Status.ToString() });
    }

    /// <summary>
    /// Price received + mortgage released → «بانتظار التسوية المالية». The sale figures are placed on a draft
    /// reconciliation (basis voluntary_sale) prepared by the caller; approval stays a separate maker-checker step.
    /// </summary>
    private static async Task<IResult> Complete(string reference, SaleCompleteBody req, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock,
        CaseWorkflow workflow, SaleService sales, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var (c, s) = await LoadAsync(reference, access, sales, track: true);
        if (s.Status != SaleStatus.OfferApproved || s.AcceptedOfferId is null) throw new ConflictException("sale_not_executing", "لا يوجد عرض معتمد قيد التنفيذ.");
        var steps = await db.Set<SaleTrackingStep>().Where(x => x.SaleId == s.Id).ToListAsync();
        var missing = new List<string>();
        foreach (var k in new[] { "payment_received", "lien_release" })
        {
            var st = steps.FirstOrDefault(x => x.Key == k);
            if (st is not { Status: TrackingStatus.Done } || string.IsNullOrWhiteSpace(st.Reference)) missing.Add($"«{st?.Title ?? k}» لم تُسجَّل بمرجعها");
        }
        CaseStatus? expected = req.ExpectedStatus is { } es ? CaseStatusInfo.Parse(es) : null;
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "استلام الثمن وفك الرهن" : req.Reason.Trim();
        // Ordering rule: transition first (a refusal is audited in its own transaction), then audit.
        await workflow.TransitionAsync(c, "sale_completed", reason, expected, evidence: [$"sale:{s.BuyerReference}"], extraGuardFailures: missing);

        var offer = await db.Set<BuyerOffer>().FirstAsync(o => o.Id == s.AcceptedOfferId);
        var debt = await sales.DebtAsync(c, s);
        var f = SaleCalculator.Offer(offer.Price, debt, s.BrokerRate, null);
        var payment = steps.First(x => x.Key == "payment_received");
        var now = clock.UtcNow;
        s.Status = SaleStatus.Completed;
        s.ClosedAt = now;
        s.CloseReason = "completed";
        var recon = steps.FirstOrDefault(x => x.Key == "reconciliation");
        if (recon is not null) { recon.Status = TrackingStatus.Pending; recon.EnteredByUserId = rc.UserId; recon.EnteredAt = now; }

        Reconciliation? draft = null;
        if (!await db.Reconciliations.AnyAsync(r => r.CaseId == c.Id && r.Status != ReconciliationStatus.Returned))
        {
            draft = new Reconciliation
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, Basis = "voluntary_sale", ExpectedAmount = debt, ReceivedAmount = f.LenderRecovery,
                WaivedAmount = 0, Difference = debt - f.LenderRecovery, PreparedByUserId = rc.UserId,
                DifferenceExplanation = f.Shortfall > 0 ? "عجز بعد البيع — يتطلب قراراً موثقاً" : null,
            };
            draft.Lines.AddRange(
            [
                new ReconciliationLine { Label = $"ثمن البيع {offer.Code}", Amount = offer.Price, Reference = payment.Reference, Kind = "receipt" },
                new ReconciliationLine { Label = $"عمولة الوسيط {s.BrokerRate * 100:0.##}%", Amount = f.BrokerCommission, Kind = "cost" },
                new ReconciliationLine { Label = "سداد المديونية", Amount = f.LenderRecovery, Reference = payment.Reference, Kind = "lender_share" },
                new ReconciliationLine { Label = "الفائض للمالك", Amount = f.OwnerSurplus, Reference = steps.FirstOrDefault(x => x.Key == "surplus_transfer")?.Reference, Kind = "surplus", MatchStatus = "pending" },
            ]);
            db.Reconciliations.Add(draft);
        }
        await audit.RecordAsync(new AuditEntry("sale.completed", "اكتمال البيع الطوعي — للتسوية المالية", c.Id, c.Reference, Reason: reason,
            Detail: $"الثمن {offer.Price:N2} · سداد المديونية {f.LenderRecovery:N2} · العمولة {f.BrokerCommission:N2} · للمالك {f.OwnerSurplus:N2}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            caseStatus = CaseStatusInfo.Key(c.Status), saleStatus = s.Status.ToString(),
            figures = new { salePrice = offer.Price, debtPayoff = f.LenderRecovery, brokerCommission = f.BrokerCommission, ownerSurplus = f.OwnerSurplus, shortfall = f.Shortfall },
            reconciliationId = draft?.Id,
        });
    }
}
