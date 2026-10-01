using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Market.Discovery;

namespace Rahoon.Api.Modules.Market;

public sealed record ContactInput(string? Name, string? Phone, string? Topic, string? Message, bool Consent);
public sealed record CalcTermsInput(DeveloperTerms? Developer, FinancierTerms? Financier, decimal? SellerCosts, decimal? BuyerCostsNow, decimal? BuyerCostsLater, bool NeedsNewFinancing,
    List<FeeItem>? Fees = null, DateOnly? AsOf = null, Dictionary<string, string>? States = null, MarketReference? Reference = null);
public sealed record CalcCapacityInput(decimal? AvailableNow, decimal? InstallmentComfort, string? InstallmentFrequency, decimal? MaxPrice, int? CommitMonths, int? MaxTermMonths = null);
/// <summary>A dated market reference with its source, for a comparison that is never presented as a saving or a return.</summary>
public sealed record MarketReference(decimal? Value, DateOnly? Date, string? Source, string? Description);

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
        // The list, the map and the comparison are Discovery.DiscoveryEndpoints (one query spine).
        g.MapGet("/opportunities/{reference}", Detail);
        g.MapGet("/photos/{id:guid}", PublicPhoto);
        g.MapPost("/calc/opportunities/{reference}/fit", FitOne).RequireRateLimiting("auth");
        g.MapGet("/sitemap", Sitemap);
    }

    private static IResult Catalog(IConfiguration config)
    {
        var policy = CommissionPolicy.From(config);
        return Results.Ok(new
        {
            propertyTypes = FieldCatalog.PropertyTypes, obligationModes = FieldCatalog.ObligationModes, obligationKinds = FieldCatalog.ObligationKinds,
            frequencies = FieldCatalog.Frequencies, fields = FieldCatalog.Fields, documents = FieldCatalog.Documents, cities = FieldCatalog.Cities,
            unknown = FieldCatalog.Unknown,
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

    private static readonly string[] Payers = ["buyer", "seller", "split"];
    private static readonly string[] FigureStateValues = [FigureStates.Verified, FigureStates.Declared, FigureStates.Estimated];

    private static IResult CalcTerms(CalcTermsInput req, IConfiguration config, IClock clock)
    {
        var bad = new[] { req.SellerCosts, req.BuyerCostsNow, req.BuyerCostsLater, req.Developer?.PaidApproved, req.Developer?.RemainingBalance, req.Developer?.Arrears,
                          req.Developer?.Installment, req.Developer?.ExtraPayment, req.Financier?.SalePrice, req.Financier?.PayoffAmount, req.Financier?.Arrears }
            .Concat((req.Fees ?? []).Select(f => f.Amount))
            .Any(x => x is < 0) || req.Developer?.Reduction < 0;
        if (bad) Validate.Throw("amounts", "لا تُقبل مبالغ سالبة.");
        var v = new Validator();
        v.Require((req.Fees ?? []).Count <= 10, "fees", "عشرة رسوم على الأكثر.");
        foreach (var (f, i) in (req.Fees ?? []).Select((f, i) => (f, i)))
        {
            v.Require(f.Label?.Trim() is { Length: >= 2 and <= 80 }, $"fees[{i}].label", "اكتب اسم الرسم.");
            v.Require(Payers.Contains(f.Payer), $"fees[{i}].payer", "اختر من يتحمل الرسم.");
            v.Require(f.Timing is "now" or "later", $"fees[{i}].timing", "اختر موعد الرسم.");
            v.Require(f.Payer != "split" || f.BuyerSharePercent is >= 0 and <= 100, $"fees[{i}].buyerSharePercent", "حصة المشتري بين 0 و100%.");
        }
        v.Require((req.States ?? []).Values.All(FigureStateValues.Contains), "states", "حالة الرقم غير معروفة.");
        v.Require(req.AsOf is null || req.AsOf <= clock.TodayRiyadh, "asOf", "تاريخ الأرقام في المستقبل.");
        v.ThrowIfInvalid();
        var input = new TermsInput
        {
            Developer = req.Developer, Financier = req.Financier, SellerCosts = req.SellerCosts, BuyerCostsNow = req.BuyerCostsNow, BuyerCostsLater = req.BuyerCostsLater,
            NeedsNewFinancing = req.NeedsNewFinancing, Fees = req.Fees?.Select(f => f with { Label = f.Label.Trim() }).ToList(), AsOf = req.AsOf,
            States = req.States ?? new(),
        };
        var result = MarketCalculator.Compute(input, CommissionPolicy.From(config));
        return Results.Ok(new { result, comparison = req.Reference is null ? null : CompareWithReference(req.Reference, result, clock) });
    }

    /// <summary>
    /// The buyer's all-in total against a dated, sourced market reference. Only when that total is complete; worded as a difference,
    /// never a saving or a return. Rahoon's commission is outside the totals until its policy is approved.
    /// </summary>
    public static object CompareWithReference(MarketReference reference, TermsResult result, IClock clock)
    {
        var v = new Validator();
        v.Require(reference.Value is > 0, "reference.value", "أدخل قيمة المرجع.");
        v.Require(reference.Date is not null && reference.Date <= clock.TodayRiyadh, "reference.date", "أدخل تاريخ المرجع (ليس في المستقبل).");
        v.Require(reference.Source?.Trim() is { Length: >= 2 and <= 120 }, "reference.source", "اذكر مصدر المرجع.");
        v.ThrowIfInvalid();
        var source = reference.Source!.Trim();
        var age = clock.TodayRiyadh.DayNumber - reference.Date!.Value.DayNumber;
        var notes = new List<string> { "فرق تقديري بين أرقام أدخلتها ومرجع ذكرته؛ ليس توفيرًا مضمونًا ولا عائدًا متوقعًا." };
        if (age > 365) notes.Add("المرجع أقدم من سنة؛ قد لا يعكس السوق الآن.");
        if (!result.Commission.PolicyApproved) notes.Add("لا يشمل الإجمالي عمولة رهون لأن سياستها لم تُعتمد بعد.");
        if (result.BuyerTotal is not { } total)
            return new { comparable = false, reason = "إجمالي التزام المشتري غير مكتمل، فلا تصح المقارنة بالمرجع.", reference = new { reference.Value, reference.Date, source }, notes };
        var diff = reference.Value!.Value - total;
        var amount = Math.Abs(diff).ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        var text = diff switch
        {
            > 0 => $"إجمالي الالتزام أقل من المرجع بفرق {amount} ر.س.",
            < 0 => $"إجمالي الالتزام أعلى من المرجع بفرق {amount} ر.س.",
            _ => "إجمالي الالتزام يساوي المرجع.",
        };
        return new { comparable = true, buyerTotal = total, difference = diff, text, reference = new { reference.Value, reference.Date, source, reference.Description }, stale = age > 365, notes };
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
        var cap = new CapacityProfile(req.AvailableNow, req.InstallmentComfort, req.InstallmentFrequency ?? "monthly", req.MaxPrice, req.MaxTermMonths);
        int fitting = 0, incomplete = 0, published = 0;
        if (cap.Any)
        {
            // The same strict rules as the list and the map (Discovery.DiscoveryQuery), so the counts match a search with these figures.
            using var _ = rc.BeginSystemScope();
            var all = DiscoveryQuery.Published(db);
            var (passing, unknown) = DiscoveryQuery.Budget(all, cap);
            published = await all.CountAsync();
            fitting = await passing.CountAsync();
            incomplete = await unknown.CountAsync();
        }
        return Results.Ok(new
        {
            monthlyEquivalent = monthly, availableNow = req.AvailableNow, capacityTotal, annualComfort = monthly is { } mm ? mm * 12 : (decimal?)null,
            notes = new[]
            {
                "هذه أرقام تصرح بها أنت، وليست موافقة تمويل.",
                "القسط ربع السنوي أو السنوي يُحوّل إلى مكافئ شهري للمقارنة فقط؛ وتُحسب الدفعات السنوية ضمن التزامك في السنة.",
            },
            published = new { fitting, incomplete, comparable = published - incomplete, total = published },
        });
    }

    private static async Task<IResult> Detail(string reference, RahoonDbContext db, RequestContext rc)
    {
        var saved = false;
        object? myInterest = null;
        Guid oppId;
        using (rc.BeginSystemScope())
            oppId = await db.Opportunities.Where(o => o.Reference == reference && o.Status == OpportunityStatus.Published).Select(o => o.Id).FirstOrDefaultAsync();
        if (oppId == Guid.Empty) throw new NotFoundException();
        var buyer = await Matching.OwnLiveRequestAsync(db, rc);
        if (rc.IsIndividual)
        {
            saved = await db.SavedOpportunities.AnyAsync(s => s.ApplicantUserId == rc.UserId && s.OpportunityId == oppId && s.RemovedAt == null);
            var i = await db.Interests.Where(x => x.ApplicantUserId == rc.UserId && x.OpportunityId == oppId && x.Status != InterestStatus.Withdrawn).FirstOrDefaultAsync();
            if (i is not null) myInterest = new { i.Reference, status = i.Status, statusLabel = InterestFlow.Labels[i.Status] };
        }
        using var _ = rc.BeginSystemScope();
        var opp = await db.Opportunities.FirstAsync(o => o.Id == oppId);
        var terms = await db.OpportunityTerms.FirstAsync(t => t.Id == opp.PublishedTermsId);
        var approvals = (await DiscoveryEndpoints.PublicApprovalsAsync(db, [opp.SaleRequestId])).GetValueOrDefault(opp.SaleRequestId) ?? [];
        var cap = buyer is null ? null : Matching.Capacity(buyer);
        object? fit = cap is { Any: true }
            ? new
            {
                fit = Affordability.Classify(cap, terms), hasBuyerRequest = true, revision = buyer!.PreferencesRevision,
                match = Matching.Explain(opp, Matching.WithProfile(new SearchCriteria(), buyer), Matching.Preferences(buyer)),
            }
            : null;
        return Results.Ok(OpportunityProjection.Detail(opp, terms, saved, approvals, fit, myInterest,
            new { nextPayments = Affordability.NextPayments(terms), caveats = Affordability.Caveats(terms) }));
    }

    /// <summary>The opportunity calculator: the visitor's own figures against the published terms (same rules as search).</summary>
    private static async Task<IResult> FitOne(string reference, CalcCapacityInput req, RahoonDbContext db, RequestContext rc)
    {
        new Validator()
            .Require(req.AvailableNow is null or >= 0, "availableNow", "أدخل مبلغًا صحيحًا.")
            .Require(req.InstallmentComfort is null or >= 0, "installmentComfort", "أدخل مبلغًا صحيحًا.")
            .Require(req.MaxPrice is null or >= 0, "maxPrice", "أدخل مبلغًا صحيحًا.")
            .Require(req.InstallmentFrequency is null or "monthly" or "quarterly" or "semiannual" or "annual", "installmentFrequency", "اختر دورية القسط.")
            .ThrowIfInvalid();
        using var _ = rc.BeginSystemScope();
        var t = await DiscoveryQuery.Published(db).Where(x => x.O.Reference == reference).Select(x => x.T).FirstOrDefaultAsync() ?? throw new NotFoundException();
        var fit = Affordability.Classify(new CapacityProfile(req.AvailableNow, req.InstallmentComfort, req.InstallmentFrequency ?? "monthly", req.MaxPrice, req.MaxTermMonths), t);
        return Results.Ok(new { fit, leftAfterNow = fit.CashLeftAfterNow, monthlyEquivalent = t.InstallmentMonthlyEquivalent, annualCommitment = fit.AnnualCommitment, comfortAnnual = fit.ComfortAnnual });
    }

    /// <summary>A listing photo is public only while it is approved and shown in a published opportunity. Private documents are never served here.</summary>
    private static async Task<IResult> PublicPhoto(Guid id, RahoonDbContext db, RequestContext rc, FileStore files, HttpContext http)
    {
        ListingPhoto? photo;
        using (rc.BeginSystemScope())
        {
            var shown = await db.Opportunities.AnyAsync(o => o.Status == OpportunityStatus.Published && o.PhotoIds.Contains(id));
            photo = shown ? await db.ListingPhotos.FirstOrDefaultAsync(p => p.Id == id && p.RemovedAt == null && p.ReviewStatus == FileReviewStatus.Accepted) : null;
        }
        if (photo is null) throw new NotFoundException();
        var read = await files.ReadAsync(photo.FileId) ?? throw new NotFoundException();
        // Revalidated on every use (ETag = content hash): once the opportunity is withdrawn or the photo removed, the next request is a 404.
        var etag = $"\"{read.File.Sha256}\"";
        http.Response.Headers.CacheControl = "public, no-cache";
        http.Response.Headers.ETag = etag;
        if (http.Request.Headers.IfNoneMatch.ToString() == etag)
        {
            http.Response.Headers.Remove("X-Robots-Tag");
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }
        http.Response.Headers.Remove("X-Robots-Tag");
        return Results.Bytes(read.Content, read.File.ContentType);
    }

    private static async Task<IResult> Sitemap(RahoonDbContext db, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var rows = await db.Opportunities.Where(o => o.Status == OpportunityStatus.Published).OrderByDescending(o => o.PublishedAt)
            .Select(o => new { o.Reference, o.PublishedAt }).Take(5000).ToListAsync();
        return Results.Ok(rows);
    }
}
