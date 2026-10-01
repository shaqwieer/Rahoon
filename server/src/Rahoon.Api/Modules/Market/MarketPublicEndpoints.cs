using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Market;

public sealed record ContactInput(string? Name, string? Phone, string? Topic, string? Message, bool Consent);
public sealed record CalcTermsInput(DeveloperTerms? Developer, FinancierTerms? Financier, decimal? SellerCosts, decimal? BuyerCostsNow, decimal? BuyerCostsLater, bool NeedsNewFinancing);
public sealed record CalcCapacityInput(decimal? AvailableNow, decimal? InstallmentComfort, string? InstallmentFrequency, decimal? MaxPrice, int? CommitMonths);

/// <summary>
/// Open to visitors: the field catalog, the contact form, the calculators (same engine as the team) and the published
/// opportunities. Reads run in a system scope but only ever project published, public data.
/// </summary>
public static class MarketPublicEndpoints
{
    public const int MaxPageSize = 24;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/market");
        g.MapGet("/catalog", Catalog);
        g.MapPost("/contact", Contact).RequireRateLimiting("auth").Idempotent();
        g.MapPost("/calc/terms", CalcTerms).RequireRateLimiting("auth");
        g.MapPost("/calc/capacity", CalcCapacity).RequireRateLimiting("auth");
        g.MapGet("/opportunities", Search);
        g.MapGet("/opportunities/{reference}", Detail);
        g.MapGet("/photos/{id:guid}", PublicPhoto);
        g.MapPost("/calc/opportunities/{reference}/fit", FitOne).RequireRateLimiting("auth");
        g.MapGet("/sitemap", Sitemap);
    }

    private static async Task<IResult> Catalog(RahoonDbContext db, RequestContext rc, IConfiguration config)
    {
        using var _ = rc.BeginSystemScope();
        var parties = await db.ObligationParties.Where(p => p.Active).OrderBy(p => p.Kind).ThenBy(p => p.SortOrder).ThenBy(p => p.NameAr)
            .Select(p => new { p.Id, p.Kind, name = p.NameAr, p.IsDemo }).ToListAsync();
        var policy = CommissionPolicy.From(config);
        return Results.Ok(new
        {
            propertyTypes = FieldCatalog.PropertyTypes, obligationModes = FieldCatalog.ObligationModes, obligationKinds = FieldCatalog.ObligationKinds,
            frequencies = FieldCatalog.Frequencies, fields = FieldCatalog.Fields, documents = FieldCatalog.Documents, cities = FieldCatalog.Cities,
            unknown = FieldCatalog.Unknown, parties,
            commission = new { policy.Approved, text = policy.Approved && policy.Rate is { } rate ? $"{rate * 100:0.##}%" : "تُحدد وفق السياسة والعقد المعتمدين (لم تُعتمد بعد)" },
        });
    }

    private static async Task<IResult> Contact(ContactInput req, RahoonDbContext db, RequestContext rc, MarketService market, PiiProtector pii, IClock clock)
    {
        var phone = Identity.PhoneAuthEndpoints.NormalizeMobile(req.Phone);
        var name = Identity.PhoneAuthEndpoints.CleanName(req.Name);
        var message = req.Message?.Trim() ?? "";
        new Validator()
            .Require(name is not null, "name", "اكتب اسمك.")
            .Require(phone is not null, "phone", "أدخل رقم جوال سعودي صحيحًا يبدأ بـ 05.")
            .Require(req.Topic is "sell" or "buy" or "request" or "other", "topic", "اختر الموضوع.")
            .Require(message.Length is >= 10 and <= 2000, "message", "اكتب رسالتك في 10 أحرف على الأقل.")
            .Require(req.Consent, "consent", "للإرسال، وافق على تواصل الفريق معك.")
            .ThrowIfInvalid();
        var org = await market.OperatorOrgIdAsync();
        using var _ = rc.BeginSystemScope();
        var c = new ContactMessage
        {
            OrganizationId = org, Reference = await market.NextReferenceAsync("CM"), Name = name!, PhoneEnc = pii.Protect(phone!), PhoneMasked = Mask.Phone(phone!),
            Topic = req.Topic!, Message = message, UserId = rc.IsAuthenticated ? rc.UserId : null,
        };
        db.ContactMessages.Add(c);
        await db.SaveChangesAsync();
        return Results.Ok(new { c.Reference });
    }

    private static IResult CalcTerms(CalcTermsInput req, IConfiguration config)
    {
        var bad = new[] { req.SellerCosts, req.BuyerCostsNow, req.BuyerCostsLater, req.Developer?.PaidApproved, req.Developer?.RemainingBalance, req.Developer?.Arrears,
                          req.Developer?.Installment, req.Developer?.ExtraPayment, req.Financier?.SalePrice, req.Financier?.PayoffAmount, req.Financier?.Arrears }
            .Any(x => x is < 0) || req.Developer?.Reduction < 0;
        if (bad) Validate.Throw("amounts", "لا تُقبل مبالغ سالبة.");
        var input = new TermsInput
        {
            Developer = req.Developer, Financier = req.Financier, SellerCosts = req.SellerCosts, BuyerCostsNow = req.BuyerCostsNow, BuyerCostsLater = req.BuyerCostsLater,
            NeedsNewFinancing = req.NeedsNewFinancing,
        };
        return Results.Ok(MarketCalculator.Compute(input, CommissionPolicy.From(config)));
    }

    /// <summary>Capacity from the buyer's own figures, and the real number of published opportunities that fit them (never invented).</summary>
    private static async Task<IResult> CalcCapacity(CalcCapacityInput req, RahoonDbContext db, RequestContext rc)
    {
        new Validator()
            .Require(req.AvailableNow is null or >= 0, "availableNow", "أدخل مبلغًا صحيحًا.")
            .Require(req.InstallmentComfort is null or >= 0, "installmentComfort", "أدخل مبلغًا صحيحًا.")
            .Require(req.InstallmentFrequency is null or "monthly" or "quarterly" or "semiannual" or "annual", "installmentFrequency", "اختر دورية القسط.")
            .Require(req.CommitMonths is null or (>= 1 and <= 360), "commitMonths", "المدة بين شهر و360 شهرًا.")
            .ThrowIfInvalid();
        var monthly = MarketCalculator.MonthlyEquivalent(req.InstallmentComfort, req.InstallmentFrequency ?? "monthly");
        decimal? capacityTotal = req.AvailableNow is { } a && monthly is { } m && req.CommitMonths is { } months ? a + m * months : null;
        int fitting = 0, comparable = 0;
        if (req.AvailableNow is not null)
        {
            using var _ = rc.BeginSystemScope();
            var rows = await db.Opportunities.Where(o => o.Status == OpportunityStatus.Published)
                .Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => t).ToListAsync();
            var cap = new CapacityInput(req.AvailableNow, req.InstallmentComfort, req.InstallmentFrequency, req.MaxPrice);
            foreach (var t in rows)
            {
                var fit = MarketCalculator.Fit(cap, t.DueNow, t.PurchaseTotal, t.InstallmentMonthlyEquivalent, t.LargestExtraPayment, t.NeedsNewFinancing);
                if (fit.Comparable) comparable++;
                if (fit.Fits) fitting++;
            }
        }
        return Results.Ok(new
        {
            monthlyEquivalent = monthly, availableNow = req.AvailableNow, capacityTotal,
            notes = new[]
            {
                "هذه أرقام تصرح بها أنت، وليست موافقة تمويل.",
                "القسط ربع السنوي أو السنوي يُحوّل إلى مكافئ شهري للمقارنة فقط؛ تُعرض الأقساط الفعلية ومواعيدها في كل فرصة.",
            },
            published = new { fitting, comparable },
        });
    }

    // ── Search ──

    public sealed record SearchQuery(
        string? City, string? Types, decimal? MaxNow, decimal? MaxInstallment, string? Freq, decimal? MaxTotal, string? Readiness, decimal? MinArea,
        int? Bedrooms, string? Track, string? Sort, int? Page, int? PageSize);

    private static async Task<IResult> Search([AsParameters] SearchQuery q, RahoonDbContext db, RequestContext rc)
    {
        var page = Math.Max(1, q.Page ?? 1);
        var size = Math.Clamp(q.PageSize ?? 12, 1, MaxPageSize);
        var cities = Split(q.City).Where(c => FieldCatalog.City(c) is not null).ToList();
        var types = Split(q.Types).Where(t => FieldCatalog.PropertyTypes.Any(p => p.Value == t)).ToList();
        var comfortMonthly = MarketCalculator.MonthlyEquivalent(q.MaxInstallment, q.Freq ?? "monthly");

        List<Guid> savedIds = [];
        if (rc.IsIndividual) savedIds = await db.SavedOpportunities.Where(s => s.ApplicantUserId == rc.UserId && s.RemovedAt == null).Select(s => s.OpportunityId).ToListAsync();

        using var _ = rc.BeginSystemScope();
        var baseQuery = db.Opportunities.Where(o => o.Status == OpportunityStatus.Published)
            .Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => new { o, t });
        if (cities.Count > 0) baseQuery = baseQuery.Where(x => cities.Contains(x.o.City));
        if (types.Count > 0) baseQuery = baseQuery.Where(x => types.Contains(x.o.PropertyType));
        if (q.Readiness is "ready" or "under_construction") baseQuery = baseQuery.Where(x => x.o.Readiness == q.Readiness);
        if (q.MinArea is { } minArea) baseQuery = baseQuery.Where(x => x.o.Area != null && x.o.Area >= minArea);
        if (q.Bedrooms is { } beds) baseQuery = baseQuery.Where(x => x.o.Bedrooms != null && x.o.Bedrooms >= beds);
        if (q.Track is "developer" or "financier" or "mixed") baseQuery = baseQuery.Where(x => x.o.Track == q.Track);

        // Budget filters: an unknown figure never passes as 0. Those excluded only because a figure is missing are counted.
        var budget = baseQuery;
        var incomplete = 0;
        if (q.MaxNow is { } maxNow)
        {
            incomplete += await baseQuery.CountAsync(x => x.t.DueNow == null);
            budget = budget.Where(x => x.t.DueNow != null && x.t.DueNow <= maxNow);
        }
        if (q.MaxTotal is { } maxTotal)
        {
            incomplete += await budget.CountAsync(x => x.t.PurchaseTotal == null);
            budget = budget.Where(x => x.t.PurchaseTotal != null && x.t.PurchaseTotal <= maxTotal);
        }
        if (comfortMonthly is { } cm)
        {
            // No future installments (e.g. a bank-financed property bought outright) passes; an unknown installment with a future balance doesn't.
            incomplete += await budget.CountAsync(x => x.t.InstallmentMonthlyEquivalent == null && x.t.FutureBalance != 0);
            budget = budget.Where(x => (x.t.InstallmentMonthlyEquivalent != null && x.t.InstallmentMonthlyEquivalent <= cm)
                                       || (x.t.InstallmentMonthlyEquivalent == null && x.t.FutureBalance == 0));
        }

        var total = await budget.CountAsync();
        var sort = q.Sort ?? (q.MaxNow is not null ? "fit" : "newest");
        var ordered = sort switch
        {
            "now" => budget.OrderBy(x => x.t.DueNow == null).ThenBy(x => x.t.DueNow).ThenByDescending(x => x.o.PublishedAt),
            "price" => budget.OrderBy(x => x.t.PurchaseTotal == null).ThenBy(x => x.t.PurchaseTotal).ThenByDescending(x => x.o.PublishedAt),
            // «الأنسب لقدرتك»: the most room left after the amount due now, then the smallest installment.
            "fit" when q.MaxNow is not null => budget.OrderBy(x => x.t.DueNow).ThenBy(x => x.t.InstallmentMonthlyEquivalent ?? 0).ThenByDescending(x => x.o.PublishedAt),
            _ => budget.OrderByDescending(x => x.o.PublishedAt),
        };
        var rows = await ordered.Skip((page - 1) * size).Take(size).ToListAsync();
        var cap = new CapacityInput(q.MaxNow, q.MaxInstallment, q.Freq ?? "monthly", q.MaxTotal);
        var items = rows.Select(x => new
        {
            card = OpportunityProjection.Card(x.o, x.t, savedIds.Contains(x.o.Id)),
            fit = q.MaxNow is null && q.MaxInstallment is null && q.MaxTotal is null ? null
                : MarketCalculator.Fit(cap, x.t.DueNow, x.t.PurchaseTotal, x.t.InstallmentMonthlyEquivalent, x.t.LargestExtraPayment, x.t.NeedsNewFinancing),
        });
        return Results.Ok(new { items, total, page, pageSize = size, pages = (int)Math.Ceiling(total / (double)size), excludedIncomplete = incomplete, sort });
    }

    private static IEnumerable<string> Split(string? csv) => (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();

    private static async Task<IResult> Detail(string reference, RahoonDbContext db, RequestContext rc)
    {
        Guid? myBuyer = null;
        CapacityInput? cap = null;
        var saved = false;
        object? myInterest = null;
        Guid oppId;
        using (rc.BeginSystemScope())
            oppId = await db.Opportunities.Where(o => o.Reference == reference && o.Status == OpportunityStatus.Published).Select(o => o.Id).FirstOrDefaultAsync();
        if (oppId == Guid.Empty) throw new NotFoundException();
        if (rc.IsIndividual)
        {
            var b = await db.BuyerRequests.Where(x => x.ApplicantUserId == rc.UserId && x.Status != BuyerRequestStatus.Withdrawn && x.Status != BuyerRequestStatus.Rejected)
                .FirstOrDefaultAsync();
            if (b is not null) { myBuyer = b.Id; cap = new CapacityInput(b.AvailableNow, b.InstallmentComfort, b.InstallmentFrequency, b.MaxPrice); }
            saved = await db.SavedOpportunities.AnyAsync(s => s.ApplicantUserId == rc.UserId && s.OpportunityId == oppId && s.RemovedAt == null);
            var i = await db.Interests.Where(x => x.ApplicantUserId == rc.UserId && x.OpportunityId == oppId && x.Status != InterestStatus.Withdrawn).FirstOrDefaultAsync();
            if (i is not null) myInterest = new { i.Reference, status = i.Status, statusLabel = InterestFlow.Labels[i.Status] };
        }
        using var _ = rc.BeginSystemScope();
        var opp = await db.Opportunities.FirstAsync(o => o.Id == oppId);
        var terms = await db.OpportunityTerms.FirstAsync(t => t.Id == opp.PublishedTermsId);
        var obligations = await db.SaleObligations.Include(o => o.Party).Where(o => o.SaleRequestId == opp.SaleRequestId && o.RemovedAt == null).ToListAsync();
        var approvals = await db.ExternalApprovals.Where(a => a.SaleRequestId == opp.SaleRequestId).OrderBy(a => a.RecordedAt).ToListAsync();
        // Public-safe approval summary: the party's own decision on a transfer, separate from Rahoon's review. No documents.
        var approvalSummary = obligations.Select(o =>
        {
            var a = approvals.LastOrDefault(x => x.ObligationId == o.Id);
            var status = a?.Status ?? ExternalApprovalStatus.NotRequested;
            return (object)new
            {
                party = o.Kind == "developer" ? "موافقة المطور على النقل" : "موافقة جهة التمويل",
                status, statusLabel = ExternalApprovalLabels.Labels[status], conditions = status == ExternalApprovalStatus.Conditional ? a?.Conditions : null,
                expiresOn = a?.ExpiresOn,
            };
        }).ToList();
        var fit = cap is null ? null : MarketCalculator.Fit(cap, terms.DueNow, terms.PurchaseTotal, terms.InstallmentMonthlyEquivalent, terms.LargestExtraPayment, terms.NeedsNewFinancing);
        return Results.Ok(OpportunityProjection.Detail(opp, terms, saved, approvalSummary, fit is null ? null : new { fit, hasBuyerRequest = myBuyer is not null }, myInterest));
    }

    /// <summary>The opportunity calculator: the visitor's own figures against the published terms (same rules as search).</summary>
    private static async Task<IResult> FitOne(string reference, CalcCapacityInput req, RahoonDbContext db, RequestContext rc)
    {
        new Validator()
            .Require(req.AvailableNow is null or >= 0, "availableNow", "أدخل مبلغًا صحيحًا.")
            .Require(req.InstallmentComfort is null or >= 0, "installmentComfort", "أدخل مبلغًا صحيحًا.")
            .ThrowIfInvalid();
        using var _ = rc.BeginSystemScope();
        var t = await db.Opportunities.Where(o => o.Reference == reference && o.Status == OpportunityStatus.Published)
            .Join(db.OpportunityTerms, o => o.PublishedTermsId, x => x.Id, (o, x) => x).FirstOrDefaultAsync() ?? throw new NotFoundException();
        var fit = MarketCalculator.Fit(new CapacityInput(req.AvailableNow, req.InstallmentComfort, req.InstallmentFrequency ?? "monthly", req.MaxPrice),
            t.DueNow, t.PurchaseTotal, t.InstallmentMonthlyEquivalent, t.LargestExtraPayment, t.NeedsNewFinancing);
        decimal? left = req.AvailableNow is { } a && t.DueNow is { } d ? a - d : null;
        return Results.Ok(new { fit, leftAfterNow = left, monthlyEquivalent = t.InstallmentMonthlyEquivalent });
    }

    /// <summary>A listing photo is public only while it is approved and shown in a published opportunity. Private documents are never served here.</summary>
    private static async Task<IResult> PublicPhoto(Guid id, RahoonDbContext db, RequestContext rc, IDocumentStorage storage, HttpContext http)
    {
        ListingPhoto? photo;
        using (rc.BeginSystemScope())
        {
            var shown = await db.Opportunities.AnyAsync(o => o.Status == OpportunityStatus.Published && o.PhotoIds.Contains(id));
            photo = shown ? await db.ListingPhotos.FirstOrDefaultAsync(p => p.Id == id && p.RemovedAt == null && p.ReviewStatus == FileReviewStatus.Accepted) : null;
        }
        if (photo is null) throw new NotFoundException();
        http.Response.Headers.CacheControl = "public, max-age=600";
        http.Response.Headers.Remove("X-Robots-Tag");
        return Results.Stream(await storage.OpenReadAsync(photo.StorageKey), photo.ContentType);
    }

    private static async Task<IResult> Sitemap(RahoonDbContext db, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var rows = await db.Opportunities.Where(o => o.Status == OpportunityStatus.Published).OrderByDescending(o => o.PublishedAt)
            .Select(o => new { o.Reference, o.PublishedAt }).Take(5000).ToListAsync();
        return Results.Ok(rows);
    }
}
