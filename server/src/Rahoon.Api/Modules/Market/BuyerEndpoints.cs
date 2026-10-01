using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Market;

public sealed record BuyerSave(
    Guid? ClientDraftId, decimal? AvailableNow, decimal? InstallmentComfort, string? InstallmentFrequency, decimal? MaxPrice, string? PurchaseMode,
    List<string>? Cities, string? AreasText, List<string>? PropertyTypes, decimal? AreaMin, decimal? AreaMax, int? BedroomsMin, string? Readiness,
    string? DeliveryBy, string? ContactName, Guid? PreferredFinancierId = null);
public sealed record BuyerSubmit(string? ContactName, bool AcceptDeclarations);
public sealed record InterestInput(string? Message, string? ContactPreference, string? ContactName);

/// <summary>
/// The buyer side for a signed-in person (docs/product/product-definition.md §5): one live buyer request (capacity and
/// preferences), suggestions with the reason they fit, interests tied to an opportunity and its terms version, saved
/// opportunities, and the account summary with in-app notifications. The same account can also sell.
/// </summary>
public static class BuyerEndpoints
{
    public const string DeclarationsVersion = "buyer-declarations-2026-10";
    private static readonly string[] Frequencies = ["monthly", "quarterly", "semiannual", "annual"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/market").RequireIndividual();
        g.MapGet("/me", Me);
        g.MapPost("/me/notifications/read", ReadNotifications).Idempotent();

        g.MapPost("/buyer-requests", Create).Idempotent();
        g.MapGet("/buyer-requests/mine", Mine);
        g.MapPut("/buyer-requests/{reference}", Save);
        g.MapPost("/buyer-requests/{reference}/submit", Submit).Idempotent();
        g.MapPost("/buyer-requests/{reference}/resubmit", Resubmit).Idempotent();
        g.MapPost("/buyer-requests/{reference}/withdraw", Withdraw).Idempotent();

        g.MapPost("/opportunities/{reference}/interest", RegisterInterest).Idempotent();
        g.MapPost("/opportunities/{reference}/save", SaveOpportunity).Idempotent();
        g.MapPost("/opportunities/{reference}/unsave", UnsaveOpportunity).Idempotent();
        g.MapGet("/my/interests", MyInterests);
        g.MapPost("/interests/{reference}/withdraw", WithdrawInterest).Idempotent();
        g.MapGet("/my/saved", MySaved);
    }

    public static readonly Dictionary<string, string> FinanceLabels = new()
    {
        ["none"] = "لا توجد موافقة تمويل مسجلة", ["pre_approval"] = "موافقة مبدئية من جهة تمويل", ["approved"] = "موافقة تمويل من جهة تمويل",
    };

    public static object BuyerDto(BuyerRequest r, CompletionRequest? open) => new
    {
        r.Reference, status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status], nextStep = BuyerRequestFlow.NextStep(r.Status),
        editable = r.Status is not (BuyerRequestStatus.Rejected or BuyerRequestStatus.Withdrawn),
        r.AvailableNow, r.InstallmentComfort, r.InstallmentFrequency, r.MaxPrice, r.PurchaseMode, r.PreferredFinancierId, r.PreferredFinancierName, r.Cities, r.AreasText, r.PropertyTypes, r.AreaMin, r.AreaMax,
        r.BedroomsMin, r.Readiness, r.DeliveryBy, r.ContactName, r.SubmittedAt, r.CreatedAt, r.UpdatedAt, r.DecisionReason, isDemo = r.IsDemo,
        installmentMonthlyEquivalent = MarketCalculator.MonthlyEquivalent(r.InstallmentComfort, r.InstallmentFrequency),
        capacity = new
        {
            declared = new { r.AvailableNow, r.InstallmentComfort, r.InstallmentFrequency, r.MaxPrice },
            reviewed = r.CapacityReviewedAt is null ? null : new { amount = r.ReviewedAvailableNow, note = r.CapacityReviewNote, at = r.CapacityReviewedAt, by = r.CapacityReviewedByLabel },
            financeApproval = new { status = r.FinanceApprovalStatus, statusLabel = FinanceLabels.GetValueOrDefault(r.FinanceApprovalStatus, r.FinanceApprovalStatus), source = r.FinanceApprovalSource, date = r.FinanceApprovalDate, amount = r.FinanceApprovalAmount },
        },
        openCompletion = open is null ? null : new { open.Note, open.RequestedAt, open.Items },
    };

    // ── Account summary ──

    private static async Task<IResult> Me(RahoonDbContext db, RequestContext rc)
    {
        var sale = await db.SaleRequests.Where(r => r.ApplicantUserId == rc.UserId).OrderByDescending(r => r.UpdatedAt).ToListAsync();
        var saleIds = sale.Select(s => s.Id).ToList();
        var opps = await db.Opportunities.Where(o => saleIds.Contains(o.SaleRequestId) && o.Status != OpportunityStatus.Withdrawn).ToListAsync();
        var buyer = await db.BuyerRequests.Where(r => r.ApplicantUserId == rc.UserId).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
        var interests = await db.Interests.CountAsync(i => i.ApplicantUserId == rc.UserId && i.Status != InterestStatus.Withdrawn);
        var saved = await db.SavedOpportunities.CountAsync(s => s.ApplicantUserId == rc.UserId && s.RemovedAt == null);
        var events = await db.MarketEvents.Where(e => e.ApplicantUserId == rc.UserId && e.VisibleToApplicant).OrderByDescending(e => e.At).Take(30).ToListAsync();
        var unread = await db.MarketEvents.CountAsync(e => e.ApplicantUserId == rc.UserId && e.VisibleToApplicant && e.ReadAt == null);
        var saleRef = sale.ToDictionary(s => s.Id, s => s.Reference);
        var oppToSale = opps.ToDictionary(o => o.Id, o => saleRef.GetValueOrDefault(o.SaleRequestId));
        var interestRefs = await db.Interests.Where(i => i.ApplicantUserId == rc.UserId).ToDictionaryAsync(i => i.Id, i => i.Reference);
        string Link(MarketEvent e) => e.SubjectType switch
        {
            "sale_request" => saleRef.TryGetValue(e.SubjectId, out var r) ? $"/account/sell/{r}" : "/account",
            "opportunity" => oppToSale.TryGetValue(e.SubjectId, out var s) && s is not null ? $"/account/sell/{s}" : "/account",
            "buyer_request" => "/account/buy",
            "interest" => interestRefs.ContainsKey(e.SubjectId) ? "/account/interests" : "/account",
            _ => "/account",
        };
        return Results.Ok(new
        {
            saleRequests = sale.Select(s =>
            {
                var opp = opps.LastOrDefault(o => o.SaleRequestId == s.Id);
                return new
                {
                    s.Reference, status = s.Status, statusLabel = SaleRequestFlow.Labels[s.Status], nextStep = SaleRequestFlow.NextStep(s.Status),
                    propertyTypeLabel = s.PropertyType is null ? null : FieldCatalog.Label(FieldCatalog.PropertyTypes, s.PropertyType),
                    cityLabel = FieldCatalog.City(s.City)?.Label, s.District, s.UpdatedAt, s.SubmittedAt,
                    opportunity = opp is null ? null : new { opp.Reference, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status], awaitingYou = opp.Status == OpportunityStatus.AwaitingOwnerConfirmation },
                };
            }),
            buyerRequest = buyer is null ? null : new { buyer.Reference, status = buyer.Status, statusLabel = BuyerRequestFlow.Labels[buyer.Status], nextStep = BuyerRequestFlow.NextStep(buyer.Status) },
            interests, saved, unread,
            notifications = events.Select(e => new { e.Id, e.Title, e.Body, e.Reason, e.At, read = e.ReadAt is not null, link = Link(e) }),
        });
    }

    private static async Task<IResult> ReadNotifications(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var now = clock.UtcNow;
        var rows = await db.MarketEvents.Where(e => e.ApplicantUserId == rc.UserId && e.VisibleToApplicant && e.ReadAt == null).ToListAsync();
        foreach (var e in rows) e.ReadAt = now;
        await db.SaveChangesAsync();
        return Results.Ok(new { read = rows.Count });
    }

    // ── Buyer request ──

    private static Validator ValidateSave(BuyerSave req)
    {
        var v = new Validator();
        v.Require(req.AvailableNow is null or (>= 0 and <= 1_000_000_000), "availableNow", "أدخل المبلغ المتاح الآن بالريال.");
        v.Require(req.InstallmentComfort is null or (>= 0 and <= 10_000_000), "installmentComfort", "أدخل قيمة القسط المريح بالريال.");
        v.Require(req.InstallmentFrequency is null || Frequencies.Contains(req.InstallmentFrequency), "installmentFrequency", "اختر دورية القسط.");
        v.Require(req.MaxPrice is null or (>= 0 and <= 1_000_000_000), "maxPrice", "أدخل الحد الأقصى بالريال.");
        v.Require(req.PurchaseMode is null or "cash" or "external_finance" or "undecided", "purchaseMode", "اختر طريقة الشراء.");
        v.Require((req.Cities ?? []).All(c => FieldCatalog.City(c) is not null), "cities", "اختر المدن من القائمة.");
        v.Require((req.PropertyTypes ?? []).All(t => FieldCatalog.PropertyTypes.Any(p => p.Value == t)), "propertyTypes", "اختر الأنواع من القائمة.");
        v.Require(req.AreaMin is null or (>= 0 and <= 1_000_000), "areaMin", "المساحة غير منطقية.");
        v.Require(req.AreaMax is null or (>= 0 and <= 1_000_000), "areaMax", "المساحة غير منطقية.");
        v.Require(req.AreaMin is null || req.AreaMax is null || req.AreaMin <= req.AreaMax, "areaMax", "الحد الأعلى للمساحة أقل من الحد الأدنى.");
        v.Require(req.BedroomsMin is null or (>= 0 and <= 20), "bedroomsMin", "عدد الغرف غير منطقي.");
        v.Require(req.Readiness is null or "ready" or "under_construction" or "any", "readiness", "اختر حالة العقار.");
        v.Require(req.DeliveryBy is null || System.Text.RegularExpressions.Regex.IsMatch(req.DeliveryBy, @"^(20|21)\d{2}-(0[1-9]|1[0-2])$"), "deliveryBy", "اختر الشهر والسنة.");
        v.Require(req.AreasText is null || req.AreasText.Length <= 500, "areasText", "النص أطول من المسموح.");
        return v;
    }

    /// <summary>
    /// The preferred bank or finance company (optional, only with external finance). A new choice must be an active directory
    /// bank or finance company; the name is recorded with it so later directory edits never rewrite the request.
    /// </summary>
    private static async Task ApplyFinancierAsync(RahoonDbContext db, BuyerRequest r, BuyerSave req)
    {
        if (req.PurchaseMode != "external_finance" || req.PreferredFinancierId is not { } id)
        {
            r.PreferredFinancierId = null;
            r.PreferredFinancierName = null;
            return;
        }
        if (id == r.PreferredFinancierId) return;
        var org = await db.DirectoryOrganizations.FirstOrDefaultAsync(d => d.Id == id && d.Active
            && (d.Types.Contains(OrgDirectory.OrgTypes.Bank) || d.Types.Contains(OrgDirectory.OrgTypes.FinanceCompany)));
        if (org is null) Validate.Throw("preferredFinancierId", "اختر جهة التمويل من الدليل.");
        r.PreferredFinancierId = org!.Id;
        r.PreferredFinancierName = org.NameAr;
    }

    private static bool Apply(BuyerRequest r, BuyerSave req)
    {
        var capacityChanged = r.AvailableNow != req.AvailableNow || r.InstallmentComfort != req.InstallmentComfort || r.InstallmentFrequency != req.InstallmentFrequency
                              || r.MaxPrice != req.MaxPrice || r.PurchaseMode != req.PurchaseMode;
        r.AvailableNow = req.AvailableNow;
        r.InstallmentComfort = req.InstallmentComfort;
        r.InstallmentFrequency = req.InstallmentComfort is null ? null : req.InstallmentFrequency ?? "monthly";
        r.MaxPrice = req.MaxPrice;
        r.PurchaseMode = req.PurchaseMode;
        r.Cities = (req.Cities ?? []).Distinct().ToList();
        r.AreasText = req.AreasText?.Trim() is { Length: > 0 } a ? a : null;
        r.PropertyTypes = (req.PropertyTypes ?? []).Distinct().ToList();
        r.AreaMin = req.AreaMin;
        r.AreaMax = req.AreaMax;
        // Bedrooms only make sense when a residential type is chosen.
        r.BedroomsMin = r.PropertyTypes.Count == 0 || r.PropertyTypes.Any(t => t is "apartment" or "duplex" or "villa" or "townhouse") ? req.BedroomsMin : null;
        r.Readiness = req.Readiness;
        r.DeliveryBy = req.Readiness == "ready" ? null : req.DeliveryBy;
        if (req.ContactName is not null) r.ContactName = Identity.PhoneAuthEndpoints.CleanName(req.ContactName) ?? r.ContactName;
        return capacityChanged;
    }

    private static async Task<IResult> Create(BuyerSave req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        if (req.ClientDraftId is not { } draftId || draftId == Guid.Empty) Validate.Throw("clientDraftId", "معرّف المسودة مطلوب.");
        ValidateSave(req).ThrowIfInvalid();
        // One live buyer request per person: a second start returns the live one.
        var live = await db.BuyerRequests.FirstOrDefaultAsync(r => r.ApplicantUserId == rc.UserId
            && r.Status != BuyerRequestStatus.Withdrawn && r.Status != BuyerRequestStatus.Rejected);
        if (live is not null) return Results.Ok(new { live.Reference, created = false, request = BuyerDto(live, null) });
        var org = await market.OperatorOrgIdAsync();
        var r = new BuyerRequest
        {
            OrganizationId = org, ApplicantUserId = rc.UserId, ClientDraftId = req.ClientDraftId, Reference = await market.NextReferenceAsync("BR"), StatusChangedAt = clock.UtcNow,
        };
        await ApplyFinancierAsync(db, r, req);
        Apply(r, req);
        db.BuyerRequests.Add(r);
        market.Event(org, "buyer_request", r.Id, rc.UserId, "created", "بدأت طلب شراء", visible: false);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var existing = await db.BuyerRequests.FirstAsync(x => x.ApplicantUserId == rc.UserId && x.Status != BuyerRequestStatus.Withdrawn && x.Status != BuyerRequestStatus.Rejected);
            return Results.Ok(new { existing.Reference, created = false, request = BuyerDto(existing, null) });
        }
        return Results.Ok(new { r.Reference, created = true, request = BuyerDto(r, null) });
    }

    private static async Task<BuyerRequest> LoadOwnAsync(RahoonDbContext db, RequestContext rc, string reference) =>
        await db.BuyerRequests.FirstOrDefaultAsync(r => r.Reference == reference && r.ApplicantUserId == rc.UserId) ?? throw new NotFoundException();

    private static async Task<IResult> Save(string reference, BuyerSave req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var r = await LoadOwnAsync(db, rc, reference);
        if (r.Status is BuyerRequestStatus.Rejected or BuyerRequestStatus.Withdrawn) throw new ConflictException("locked", "هذا الطلب مغلق. ابدأ طلبًا جديدًا.");
        ValidateSave(req).ThrowIfInvalid();
        await ApplyFinancierAsync(db, r, req);
        var capacityChanged = Apply(r, req);
        if (r.Status == BuyerRequestStatus.ApprovedForMatching && capacityChanged)
        {
            // Changed declared capacity is reviewed again; preferences alone don't reopen the review.
            r.Status = BuyerRequestStatus.Submitted;
            r.StatusChangedAt = clock.UtcNow;
            market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "capacity_changed", "عدّلت قدرتك الشرائية؛ سيراجعها الفريق من جديد", visible: true,
                from: nameof(BuyerRequestStatus.ApprovedForMatching), to: nameof(BuyerRequestStatus.Submitted));
        }
        else if (r.Status != BuyerRequestStatus.Draft)
            market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "edited", "عدّلت تفضيلات الشراء", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { saved = true, request = BuyerDto(r, null) });
    }

    private static async Task<IResult> Submit(string reference, BuyerSubmit req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var r = await LoadOwnAsync(db, rc, reference);
        if (r.Status != BuyerRequestStatus.Draft)
            return Results.Ok(new { r.Reference, status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status], nextStep = BuyerRequestFlow.NextStep(r.Status), alreadySubmitted = true });
        var name = Identity.PhoneAuthEndpoints.CleanName(req.ContactName ?? r.ContactName);
        new Validator()
            .Require(r.AvailableNow is not null, "availableNow", "أدخل المبلغ المتاح لديك الآن.")
            .Require(r.PurchaseMode is not null, "purchaseMode", "اختر: شراء نقدي أم بتمويل من جهة خارجية.")
            .Require(r.InstallmentComfort is null || r.InstallmentFrequency is not null, "installmentFrequency", "اختر دورية القسط.")
            .Require(r.Cities.Count > 0, "cities", "اختر مدينة واحدة على الأقل.")
            .Require(r.PropertyTypes.Count > 0, "propertyTypes", "اختر نوع عقار واحدًا على الأقل.")
            .Require(name is not null, "contactName", "اكتب اسمك.")
            .Require(req.AcceptDeclarations, "acceptDeclarations", "للإرسال، وافق على معالجة طلبك والتواصل معك بشأنه.")
            .ThrowIfInvalid();
        var now = clock.UtcNow;
        r.ContactName = name;
        r.DeclarationsAcceptedAt = now;
        r.DeclarationsVersion = DeclarationsVersion;
        r.Status = BuyerRequestStatus.Submitted;
        r.StatusChangedAt = now;
        r.SubmittedAt = now;
        await SaleRequestEndpoints.RenamePlaceholderAsync(db, rc, name!);
        var e = market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "submitted", "استلمنا طلب الشراء", visible: true,
            body: BuyerRequestFlow.NextStep(BuyerRequestStatus.Submitted), from: nameof(BuyerRequestStatus.Draft), to: nameof(BuyerRequestStatus.Submitted));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(r.ApplicantUserId, e.Id, $"رهون: استلمنا طلب الشراء {r.Reference}. تابع الفرص المقترحة من حسابك.");
        return Results.Ok(new { r.Reference, status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status], nextStep = BuyerRequestFlow.NextStep(r.Status), alreadySubmitted = false });
    }

    private static async Task<IResult> Resubmit(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var r = await LoadOwnAsync(db, rc, reference);
        BuyerRequestFlow.Ensure(r.Status, BuyerRequestStatus.NeedsCompletion);
        var now = clock.UtcNow;
        foreach (var c in await db.CompletionRequests.Where(c => c.SubjectId == r.Id && c.AnsweredAt == null).ToListAsync()) c.AnsweredAt = now;
        r.Status = BuyerRequestStatus.UnderReview;
        r.StatusChangedAt = now;
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "resubmitted", "أرسلت ملف الشراء للمراجعة بعد الاستكمال", visible: true,
            body: req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 1000)] : null,
            from: nameof(BuyerRequestStatus.NeedsCompletion), to: nameof(BuyerRequestStatus.UnderReview));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    private static async Task<IResult> Withdraw(string reference, ReasonInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var r = await LoadOwnAsync(db, rc, reference);
        if (r.Status is BuyerRequestStatus.Rejected or BuyerRequestStatus.Withdrawn) throw new ConflictException("invalid_status", "الطلب مغلق.");
        var from = r.Status;
        r.Status = BuyerRequestStatus.Withdrawn;
        r.StatusChangedAt = clock.UtcNow;
        r.WithdrawReason = req.Reason?.Trim() is { Length: > 0 } reason ? reason[..Math.Min(reason.Length, 500)] : null;
        market.Event(r.OrganizationId, "buyer_request", r.Id, r.ApplicantUserId, "withdrawn", "سحبت طلب الشراء", visible: true, reason: r.WithdrawReason,
            from: from.ToString(), to: nameof(BuyerRequestStatus.Withdrawn));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = r.Status, statusLabel = BuyerRequestFlow.Labels[r.Status] });
    }

    /// <summary>The live buyer request and published opportunities matching its preferences, with why each fits and its limits.</summary>
    private static async Task<IResult> Mine(RahoonDbContext db, RequestContext rc)
    {
        var r = await db.BuyerRequests.Where(x => x.ApplicantUserId == rc.UserId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (r is null) return Results.Ok(new { request = (object?)null, suggestions = Array.Empty<object>() });
        var open = await db.CompletionRequests.Where(c => c.SubjectId == r.Id && c.AnsweredAt == null).OrderByDescending(c => c.RequestedAt).FirstOrDefaultAsync();
        var events = await db.MarketEvents.Where(e => e.SubjectId == r.Id && e.VisibleToApplicant).OrderByDescending(e => e.At).ToListAsync();
        object[] suggestions = [];
        if (r.Status is not (BuyerRequestStatus.Withdrawn or BuyerRequestStatus.Rejected or BuyerRequestStatus.Draft))
        {
            using var _ = rc.BeginSystemScope();
            var query = db.Opportunities.Where(o => o.Status == OpportunityStatus.Published);
            if (r.Cities.Count > 0) query = query.Where(o => r.Cities.Contains(o.City));
            if (r.PropertyTypes.Count > 0) query = query.Where(o => r.PropertyTypes.Contains(o.PropertyType));
            var rows = await query.Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => new { o, t }).Take(60).ToListAsync();
            var saved = await db.SavedOpportunities.Where(s => s.ApplicantUserId == rc.UserId && s.RemovedAt == null).Select(s => s.OpportunityId).ToListAsync();
            var cap = new CapacityInput(r.AvailableNow, r.InstallmentComfort, r.InstallmentFrequency, r.MaxPrice);
            suggestions = rows
                .Select(x => new { x.o, x.t, fit = MarketCalculator.Fit(cap, x.t.DueNow, x.t.PurchaseTotal, x.t.InstallmentMonthlyEquivalent, x.t.LargestExtraPayment, x.t.NeedsNewFinancing) })
                .Where(x => x.fit.Comparable)
                .OrderByDescending(x => x.fit.Fits).ThenBy(x => x.t.DueNow)
                .Take(12)
                .Select(x => (object)new { card = OpportunityProjection.Card(x.o, x.t, saved.Contains(x.o.Id)), fit = x.fit })
                .ToArray();
        }
        return Results.Ok(new { request = BuyerDto(r, open), events = events.Select(SaleRequestFile.EventDto), suggestions });
    }

    // ── Interest and saved ──

    private static async Task<(Opportunity Opp, OpportunityTerms Terms)> PublishedAsync(RahoonDbContext db, RequestContext rc, string reference)
    {
        using var _ = rc.BeginSystemScope();
        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Reference == reference && o.Status == OpportunityStatus.Published) ?? throw new NotFoundException();
        var terms = await db.OpportunityTerms.FirstAsync(t => t.Id == opp.PublishedTermsId);
        return (opp, terms);
    }

    private static async Task<IResult> RegisterInterest(string reference, InterestInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var (opp, terms) = await PublishedAsync(db, rc, reference);
        if (opp.ApplicantUserId == rc.UserId) throw new ConflictException("own_opportunity", "هذه فرصتك أنت؛ لا يمكن تسجيل اهتمام بها.");
        // A second click returns the same open interest: no duplicates, no reservation, no change to the opportunity.
        var existing = await db.Interests.FirstOrDefaultAsync(i => i.ApplicantUserId == rc.UserId && i.OpportunityId == opp.Id && i.Status != InterestStatus.Withdrawn);
        if (existing is not null) return Results.Ok(new { existing.Reference, created = false, status = existing.Status, statusLabel = InterestFlow.Labels[existing.Status] });
        var v = new Validator();
        v.Require(req.Message is null || req.Message.Length <= 1000, "message", "الرسالة أطول من المسموح.");
        v.Require(req.ContactPreference is null or "call" or "whatsapp" or "any", "contactPreference", "اختر طريقة التواصل.");
        v.ThrowIfInvalid();
        var buyer = await db.BuyerRequests.Where(b => b.ApplicantUserId == rc.UserId && b.Status != BuyerRequestStatus.Withdrawn && b.Status != BuyerRequestStatus.Rejected)
            .Select(b => (Guid?)b.Id).FirstOrDefaultAsync();
        string name;
        using (rc.BeginSystemScope()) name = await db.Users.Where(u => u.Id == rc.UserId).Select(u => u.FullName).FirstAsync();
        var contactName = Identity.PhoneAuthEndpoints.CleanName(req.ContactName) ?? (name == "عميل رهون" ? null : name);
        if (contactName is null) Validate.Throw("contactName", "اكتب اسمك ليعرف الفريق كيف يخاطبك.");
        await SaleRequestEndpoints.RenamePlaceholderAsync(db, rc, contactName!);
        var now = clock.UtcNow;
        var i = new Interest
        {
            OrganizationId = opp.OrganizationId, ApplicantUserId = rc.UserId, Reference = await market.NextReferenceAsync("IN"), OpportunityId = opp.Id, TermsId = terms.Id,
            BuyerRequestId = buyer, Message = req.Message?.Trim() is { Length: > 0 } m ? m : null, ContactPreference = req.ContactPreference ?? "any",
            ContactName = contactName, StatusChangedAt = now,
        };
        db.Interests.Add(i);
        market.Event(i.OrganizationId, "interest", i.Id, rc.UserId, "created", $"أرسلت اهتمامك بالفرصة {opp.Reference}", visible: true,
            body: "وصل اهتمامك لفريق رهون وسيتواصل معك. الاهتمام لا يحجز العقار ولا يعد عرضًا ملزمًا.", data: new { terms = terms.VersionNo });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await db.Interests.FirstAsync(x => x.ApplicantUserId == rc.UserId && x.OpportunityId == opp.Id && x.Status != InterestStatus.Withdrawn);
            return Results.Ok(new { existing.Reference, created = false, status = existing.Status, statusLabel = InterestFlow.Labels[existing.Status] });
        }
        return Results.Ok(new { i.Reference, created = true, status = i.Status, statusLabel = InterestFlow.Labels[i.Status] });
    }

    private static async Task<IResult> MyInterests(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.Interests.Where(i => i.ApplicantUserId == rc.UserId).OrderByDescending(i => i.CreatedAt).ToListAsync();
        var oppIds = rows.Select(r => r.OpportunityId).ToList();
        List<Opportunity> opps;
        using (rc.BeginSystemScope()) opps = await db.Opportunities.Where(o => oppIds.Contains(o.Id)).ToListAsync();
        var events = await db.MarketEvents.Where(e => e.ApplicantUserId == rc.UserId && e.SubjectType == "interest" && e.VisibleToApplicant).OrderByDescending(e => e.At).ToListAsync();
        return Results.Ok(rows.Select(i =>
        {
            var o = opps.First(x => x.Id == i.OpportunityId);
            return new
            {
                i.Reference, status = i.Status, statusLabel = InterestFlow.Labels[i.Status], i.CreatedAt, i.Message, i.CloseReason,
                opportunity = new
                {
                    o.Reference, o.Title, status = o.Status, statusLabel = OpportunityFlow.Labels[o.Status],
                    available = o.Status == OpportunityStatus.Published,
                    // Figures may have changed since: the interest keeps the version it was made on.
                    termsChanged = o.PublishedTermsId != i.TermsId,
                },
                events = events.Where(e => e.SubjectId == i.Id).Select(SaleRequestFile.EventDto),
            };
        }));
    }

    private static async Task<IResult> WithdrawInterest(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var i = await db.Interests.FirstOrDefaultAsync(x => x.Reference == reference && x.ApplicantUserId == rc.UserId) ?? throw new NotFoundException();
        if (i.Status is InterestStatus.Closed or InterestStatus.Withdrawn) throw new ConflictException("invalid_status", "الاهتمام مغلق.");
        var from = i.Status;
        i.Status = InterestStatus.Withdrawn;
        i.StatusChangedAt = clock.UtcNow;
        market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "withdrawn", "سحبت اهتمامك", visible: true, from: from.ToString(), to: nameof(InterestStatus.Withdrawn));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = i.Status, statusLabel = InterestFlow.Labels[i.Status] });
    }

    private static async Task<IResult> SaveOpportunity(string reference, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        var (opp, _) = await PublishedAsync(db, rc, reference);
        var row = await db.SavedOpportunities.FirstOrDefaultAsync(s => s.ApplicantUserId == rc.UserId && s.OpportunityId == opp.Id);
        if (row is null) db.SavedOpportunities.Add(new SavedOpportunity { OrganizationId = opp.OrganizationId, ApplicantUserId = rc.UserId, OpportunityId = opp.Id });
        else row.RemovedAt = null;
        await db.SaveChangesAsync();
        return Results.Ok(new { saved = true });
    }

    private static async Task<IResult> UnsaveOpportunity(string reference, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        Guid oppId;
        using (rc.BeginSystemScope()) oppId = await db.Opportunities.Where(o => o.Reference == reference).Select(o => o.Id).FirstOrDefaultAsync();
        var row = await db.SavedOpportunities.FirstOrDefaultAsync(s => s.ApplicantUserId == rc.UserId && s.OpportunityId == oppId);
        if (row is not null && row.RemovedAt is null) { row.RemovedAt = clock.UtcNow; await db.SaveChangesAsync(); }
        return Results.Ok(new { saved = false });
    }

    private static async Task<IResult> MySaved(RahoonDbContext db, RequestContext rc)
    {
        var ids = await db.SavedOpportunities.Where(s => s.ApplicantUserId == rc.UserId && s.RemovedAt == null).OrderByDescending(s => s.UpdatedAt).Select(s => s.OpportunityId).ToListAsync();
        using var _ = rc.BeginSystemScope();
        var rows = await db.Opportunities.Where(o => ids.Contains(o.Id))
            .Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => new { o, t }).ToListAsync();
        return Results.Ok(rows.Select(x => new
        {
            card = OpportunityProjection.Card(x.o, x.t, true),
            available = x.o.Status == OpportunityStatus.Published,
            statusLabel = OpportunityFlow.Labels[x.o.Status],
        }));
    }
}
