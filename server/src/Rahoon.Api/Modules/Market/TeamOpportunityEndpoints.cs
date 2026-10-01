using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Market;

public sealed record ContentForm(
    string? Title, string? Description, decimal? Area, int? Bedrooms, int? Bathrooms, string? Readiness, string? DeliveryMonth,
    Dictionary<string, string?>? Specs, List<string>? Features, List<Guid>? PhotoIds, string? LocationPrecision);

public sealed record TermsForm(
    DeveloperTerms? Developer, FinancierTerms? Financier, decimal? SellerCosts, decimal? BuyerCostsNow, decimal? BuyerCostsLater, bool NeedsNewFinancing,
    Dictionary<string, string>? States, string? TransferConditions, string? VerificationScope, DateOnly? VerifiedOn);

public sealed record ChecklistInput(List<string>? Items);

/// <summary>
/// Opportunity preparation and publication by the team, and interest follow-up. Approving a sale request never publishes:
/// the team prepares the opportunity, the owner confirms its summary (a terms version), then a member with market.publish
/// publishes it once every condition is met. A changed price, balance or transfer condition is a new version.
/// </summary>
public static class TeamOpportunityEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/team/market").RequireOrg(OrganizationKind.Operator).RequirePermission(P.MarketView);
        g.MapPost("/sale-requests/{reference}/opportunity", Create).Idempotent();
        g.MapGet("/opportunities", List);
        g.MapGet("/opportunities/{reference}", Get);
        g.MapPost("/opportunities/{reference}/assign", Assign).Idempotent();
        g.MapPut("/opportunities/{reference}/content", SaveContent);
        g.MapPut("/opportunities/{reference}/terms", SaveTerms);
        g.MapPost("/opportunities/{reference}/send-to-owner", SendToOwner).Idempotent();
        g.MapPost("/opportunities/{reference}/checklist", SaveChecklist).Idempotent();
        g.MapPost("/opportunities/{reference}/publish", Publish).Idempotent();
        g.MapPost("/opportunities/{reference}/pause", Pause).Idempotent();
        g.MapPost("/opportunities/{reference}/resume", Resume).Idempotent();
        g.MapPost("/opportunities/{reference}/withdraw", Withdraw).Idempotent();

        g.MapGet("/interests", ListInterests);
        g.MapGet("/interests/{reference}", GetInterest);
        g.MapGet("/interests/{reference}/contact", InterestContact).RequirePermission(P.MarketFollow);
        g.MapPost("/interests/{reference}/assign", AssignInterest).RequirePermission(P.MarketFollow).Idempotent();
        g.MapPost("/interests/{reference}/follow-up", FollowUp).RequirePermission(P.MarketFollow).Idempotent();
        g.MapPost("/interests/{reference}/close", CloseInterest).RequirePermission(P.MarketFollow).Idempotent();
        g.MapPost("/interests/{reference}/note", NoteInterest).RequirePermission(P.MarketFollow).Idempotent();
    }

    private static void Need(RequestContext rc, string p) => TeamMarketEndpoints.Need(rc, p);

    // ── Create from an approved sale request ──

    private static async Task<IResult> Create(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock, IConfiguration config)
    {
        Need(rc, P.MarketPrepare);
        var f = await TeamMarketEndpoints.LoadSaleAsync(db, rc, reference, P.MarketPrepare);
        var r = f.Request;
        SaleRequestFlow.Ensure(r.Status, SaleRequestStatus.ApprovedForListing);
        if (f.Opportunities.FirstOrDefault(o => o.Status != OpportunityStatus.Withdrawn) is { } existing)
            return Results.Ok(new { existing.Reference, created = false });

        var now = clock.UtcNow;
        var precision = r.LocationDisplayWish == "exact" ? "exact" : "approximate";
        var (plat, plng) = OpportunityProjection.PublicPoint(r.Latitude, r.Longitude, precision);
        var track = r.ObligationMode == "multiple" ? "mixed" : r.ObligationMode ?? "developer";
        var specKeys = FieldCatalog.PropertyFields(r.PropertyType, r.Answers)
            .Where(x => x.Key is not ("area" or "bedrooms" or "bathrooms" or "readiness" or "delivery_month" or "description" or "features") && r.Answers.ContainsKey(x.Key))
            .Select(x => x.Key);
        var photos = f.Photos.Where(p => p.ReviewStatus != FileReviewStatus.Rejected).OrderByDescending(p => p.IsCover).ThenBy(p => p.SortOrder).Select(p => p.Id).ToList();
        var opp = new Opportunity
        {
            OrganizationId = r.OrganizationId, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Reference = await market.NextReferenceAsync("OP"),
            StatusChangedAt = now, Title = DefaultTitle(r), Description = r.Answers.GetValueOrDefault("description"), PropertyType = r.PropertyType!,
            City = r.City!, District = r.District, Project = r.Project, Track = track,
            Area = FieldCatalog.Num(r.Answers, "area"), Bedrooms = (int?)FieldCatalog.Num(r.Answers, "bedrooms"), Bathrooms = (int?)FieldCatalog.Num(r.Answers, "bathrooms"),
            Readiness = r.PropertyType == "land" ? null : r.Answers.GetValueOrDefault("readiness"),
            DeliveryMonth = r.Answers.GetValueOrDefault("delivery_month") is { } dm && dm != FieldCatalog.Unknown ? dm : null,
            Specs = specKeys.ToDictionary(k => k, k => r.Answers[k]),
            Features = (r.Answers.GetValueOrDefault("features") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            ExactLatitude = r.Latitude, ExactLongitude = r.Longitude, LocationPrecision = precision, PublicLatitude = plat, PublicLongitude = plng,
            PhotoIds = photos, PreparedByUserId = rc.UserId, PreparedByLabel = rc.UserName, AssignedToUserId = rc.UserId, AssignedToLabel = rc.UserName,
        };
        db.Opportunities.Add(opp);
        // Version 1 from the answers and the verified figures; costs stay unknown until the team enters them (never assumed 0).
        var terms = NewTerms(opp, 1, f.PreliminaryInput(), null, null, null, rc, CommissionPolicy.From(config));
        db.OpportunityTerms.Add(terms);
        opp.DraftTermsId = terms.Id;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "created", "بدأ الفريق إعداد الفرصة", visible: true,
            body: "سنرسل لك ملخص الفرصة والأرقام لتؤكده قبل أي نشر.");
        market.Event(r.OrganizationId, "sale_request", r.Id, r.ApplicantUserId, "opportunity_created", $"أُنشئت الفرصة {opp.Reference} من هذا الطلب", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { opp.Reference, created = true });
    }

    private static string DefaultTitle(SaleRequest r)
    {
        var type = FieldCatalog.Label(FieldCatalog.PropertyTypes, r.PropertyType);
        var rooms = FieldCatalog.Num(r.Answers, "bedrooms") is { } b && r.PropertyType is "apartment" or "duplex" or "villa" or "townhouse" ? $" {b:0} غرف" : "";
        var place = string.Join("، ", new[] { r.District is { Length: > 0 } d ? $"حي {d}" : null, FieldCatalog.City(r.City)?.Label }.Where(x => x is not null));
        return $"{type}{rooms} في {place}";
    }

    internal static OpportunityTerms NewTerms(Opportunity opp, int versionNo, TermsInput input, string? transfer, string? scope, DateOnly? verifiedOn,
        RequestContext rc, CommissionPolicy policy)
    {
        var t = new OpportunityTerms
        {
            OrganizationId = opp.OrganizationId, OpportunityId = opp.Id, ApplicantUserId = opp.ApplicantUserId, VersionNo = versionNo, Track = opp.Track,
            InputJson = "{}", ResultJson = "{}", PreparedByUserId = rc.UserId, PreparedByLabel = rc.UserName,
        };
        ApplyTerms(t, input, transfer, scope, verifiedOn, policy);
        return t;
    }

    private static void ApplyTerms(OpportunityTerms t, TermsInput input, string? transfer, string? scope, DateOnly? verifiedOn, CommissionPolicy policy)
    {
        var result = MarketCalculator.Compute(input, policy);
        t.InputJson = JsonSerializer.Serialize(input, JsonOptions.Web);
        t.ResultJson = JsonSerializer.Serialize(result, JsonOptions.Web);
        t.DueNow = result.DueNow;
        t.PurchaseTotal = result.BuyerTotal;
        t.FutureBalance = result.FutureBalance;
        t.Installment = result.Installment;
        t.InstallmentFrequency = result.InstallmentFrequency;
        t.InstallmentMonthlyEquivalent = result.InstallmentMonthlyEquivalent;
        t.LargestExtraPayment = result.LargestExtraPayment;
        t.RemainingMonths = result.RemainingMonths;
        t.NeedsNewFinancing = result.NeedsNewFinancing;
        t.Complete = result.Complete;
        t.TransferConditions = transfer;
        t.VerificationScope = scope;
        t.VerifiedOn = verifiedOn;
    }

    // ── Read ──

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, string? status)
    {
        var query = db.Opportunities.Scoped(db, rc);
        if (status is { Length: > 0 } && Enum.TryParse<OpportunityStatus>(status, true, out var st)) query = query.Where(o => o.Status == st);
        var rows = await query.OrderByDescending(o => o.StatusChangedAt).Take(200).ToListAsync();
        var ids = rows.Select(o => o.Id).ToList();
        var interestCounts = await db.Interests.Where(i => ids.Contains(i.OpportunityId) && i.Status != InterestStatus.Withdrawn)
            .GroupBy(i => i.OpportunityId).Select(g => new { g.Key, n = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.n);
        return Results.Ok(rows.Select(o => new
        {
            o.Reference, o.Title, status = o.Status, statusLabel = OpportunityFlow.Labels[o.Status], cityLabel = FieldCatalog.City(o.City)?.Label, o.District,
            propertyTypeLabel = FieldCatalog.Label(FieldCatalog.PropertyTypes, o.PropertyType), o.PublishedAt, o.StatusChangedAt, assignedTo = o.AssignedToLabel,
            interests = interestCounts.GetValueOrDefault(o.Id),
        }));
    }

    /// <summary>An opportunity the caller may see (404 otherwise) and act on with <paramref name="permission"/> (403 otherwise).</summary>
    private static async Task<(Opportunity Opp, SaleRequestFile File, List<OpportunityTerms> Terms)> LoadAsync(RahoonDbContext db, RequestContext rc, string reference, string permission)
    {
        var opp = await db.Opportunities.FirstOrDefaultAsync(o => o.Reference == reference) ?? throw new NotFoundException();
        var sr = await db.SaleRequests.Where(r => r.Id == opp.SaleRequestId).Select(r => r.Reference).FirstAsync();
        var f = await SaleRequestFile.LoadAsync(db, sr) ?? throw new NotFoundException();
        TeamScope.Need(rc, permission, TeamScope.Assignees(opp, f.Request));
        var terms = await db.OpportunityTerms.Where(t => t.OpportunityId == opp.Id).OrderByDescending(t => t.VersionNo).ToListAsync();
        return (opp, f, terms);
    }

    internal static List<string> PublishBlockers(Opportunity opp, SaleRequestFile f, List<OpportunityTerms> terms)
    {
        var blockers = new List<string>();
        var draft = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId);
        var republish = opp.Status is OpportunityStatus.Published or OpportunityStatus.Paused;
        if (opp.Status is not (OpportunityStatus.ReadyToPublish or OpportunityStatus.Published or OpportunityStatus.Paused))
            blockers.Add($"الفرصة في حالة «{OpportunityFlow.Labels[opp.Status]}»؛ يلزم تأكيد المالك أولًا.");
        if (draft is null || draft.Status != TermsStatus.OwnerConfirmed)
            blockers.Add(republish ? "لا توجد نسخة جديدة أكدها المالك لإعادة النشر." : "لم يؤكد المالك ملخص الفرصة بعد.");
        foreach (var (key, label) in OpportunityFlow.Checklist.Where(c => !opp.Checklist.Contains(c.Key)))
            blockers.Add($"قائمة التحقق: {label}.");
        var photos = f.Photos.ToDictionary(p => p.Id);
        if (opp.PhotoIds.Count == 0) blockers.Add("أضف صورة غلاف وصورة واحدة مناسبة على الأقل.");
        else if (opp.PhotoIds.Any(id => !photos.TryGetValue(id, out var p) || p.ReviewStatus != FileReviewStatus.Accepted))
            blockers.Add("كل الصور المختارة يجب أن تكون مقبولة من الفريق.");
        if (opp.PublicLatitude is null) blockers.Add("حدد موقع العقار قبل النشر.");
        if (string.IsNullOrWhiteSpace(opp.Description)) blockers.Add("أضف وصفًا للفرصة.");
        if (draft is not null && OpportunityProjection.Result(draft).Gap) blockers.Add("الأرقام فيها فجوة تحتاج مراجعة قبل النشر.");
        return blockers;
    }

    private static async Task<IResult> Get(string reference, RahoonDbContext db, RequestContext rc, IConfiguration config)
    {
        var (opp, f, terms) = await LoadAsync(db, rc, reference, P.MarketView);
        var events = await db.MarketEvents.Where(e => e.SubjectId == opp.Id).OrderByDescending(e => e.At).ToListAsync();
        var interests = await db.Interests.Where(i => i.OpportunityId == opp.Id).OrderByDescending(i => i.CreatedAt).ToListAsync();
        var draft = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId);
        var published = terms.FirstOrDefault(t => t.Id == opp.PublishedTermsId);
        var latest = draft ?? published ?? terms.FirstOrDefault();
        var blockers = PublishBlockers(opp, f, terms);
        var s = opp.Status;
        var who = TeamScope.Assignees(opp, f.Request);
        return Results.Ok(new
        {
            opp.Reference, status = s, statusLabel = OpportunityFlow.Labels[s], saleRequest = f.Request.Reference,
            content = OpportunityProjection.Content(opp),
            editable = new { opp.Title, opp.Description, opp.Area, opp.Bedrooms, opp.Bathrooms, opp.Readiness, opp.DeliveryMonth, opp.Specs, opp.Features, opp.PhotoIds, opp.LocationPrecision },
            location = new
            {
                exact = new { lat = opp.ExactLatitude, lng = opp.ExactLongitude }, @public = new { lat = opp.PublicLatitude, lng = opp.PublicLongitude },
                precision = opp.LocationPrecision, ownerWish = f.Request.LocationDisplayWish,
            },
            availablePhotos = f.Photos.Select(f.PhotoDto),
            draftTerms = draft is null ? null : OpportunityProjection.Terms(draft),
            draftInput = (latest is null ? null : OpportunityProjection.Input(latest)),
            publishedTerms = published is null ? null : OpportunityProjection.Terms(published),
            versions = terms.Select(t => new { t.Id, t.VersionNo, status = t.Status, t.PreparedByLabel, t.SentToOwnerAt, t.OwnerDecidedAt, t.OwnerNote, current = t.Id == opp.DraftTermsId, published = t.Id == opp.PublishedTermsId }),
            checklist = OpportunityFlow.Checklist.Select(c => new { key = c.Key, label = c.Label, done = opp.Checklist.Contains(c.Key) }),
            blockers,
            interests = interests.Select(i => new { i.Reference, status = i.Status, statusLabel = InterestFlow.Labels[i.Status], i.CreatedAt, i.ContactName }),
            events = events.Select(SaleRequestFile.EventDto),
            assignedTo = opp.AssignedToUserId is null ? null : new { id = opp.AssignedToUserId, label = opp.AssignedToLabel },
            obligations = f.Obligations.Select(o => new
            {
                o.Id, o.Kind, partyName = o.PartyDisplayName,
                approval = f.Approvals.LastOrDefault(a => a.ObligationId == o.Id) is { } a ? new { status = a.Status, statusLabel = ExternalApprovalLabels.Labels[a.Status], a.Conditions } : null,
            }),
            publicUrl = s == OpportunityStatus.Published ? $"/opportunities/{opp.Reference}" : null,
            actions = new
            {
                edit = rc.CanOn(P.MarketPrepare, who) && s is not (OpportunityStatus.Withdrawn or OpportunityStatus.Completed),
                sendToOwner = rc.CanOn(P.MarketPrepare, who) && s is OpportunityStatus.Preparing or OpportunityStatus.Published or OpportunityStatus.Paused
                              && draft is { Status: TermsStatus.Draft },
                publish = rc.CanOn(P.MarketPublish, who) && blockers.Count == 0,
                pause = rc.CanOn(P.MarketPublish, who) && s == OpportunityStatus.Published,
                resume = rc.CanOn(P.MarketPublish, who) && s == OpportunityStatus.Paused && opp.PublishedTermsId is not null,
                withdraw = rc.CanOn(P.MarketPublish, who) && s is not (OpportunityStatus.Withdrawn or OpportunityStatus.Completed),
                checklist = rc.CanOn(P.MarketReview, who) || rc.CanOn(P.MarketPublish, who),
                assignOthers = rc.Has(P.MarketAssign),
            },
            commissionPolicy = CommissionPolicy.From(config),
        });
    }

    private static async Task<IResult> Assign(string reference, AssignInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        Need(rc, P.MarketPrepare);
        var (opp, _, _) = await LoadAsync(db, rc, reference, P.MarketPrepare);
        var (id, label) = await TeamMarketEndpoints.AssigneeAsync(db, rc, req.UserId);
        opp.AssignedToUserId = id;
        opp.AssignedToLabel = label;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "assigned", $"أُسندت الفرصة إلى {label}", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = new { id, label } });
    }

    // ── Edit ──

    /// <summary>
    /// Before publication, an edit sends the summary back to «قيد الإعداد» (the owner confirms again). After publication,
    /// content edits apply now and are logged; figure and transfer-term changes go through a new terms version.
    /// </summary>
    private static void ResetConfirmation(Opportunity opp, List<OpportunityTerms> terms, RequestContext rc, CommissionPolicy policy, RahoonDbContext db)
    {
        if (opp.Status is not (OpportunityStatus.AwaitingOwnerConfirmation or OpportunityStatus.ReadyToPublish)) return;
        var current = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId);
        if (current is not null && current.Status is TermsStatus.SentToOwner or TermsStatus.OwnerConfirmed)
        {
            current.Status = TermsStatus.Superseded;
            var copy = NewTerms(opp, terms.Max(t => t.VersionNo) + 1, OpportunityProjection.Input(current), current.TransferConditions, current.VerificationScope, current.VerifiedOn, rc, policy);
            db.OpportunityTerms.Add(copy);
            terms.Insert(0, copy);
            opp.DraftTermsId = copy.Id;
        }
        opp.Status = OpportunityStatus.Preparing;
    }

    private static async Task<IResult> SaveContent(string reference, ContentForm req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock, IConfiguration config)
    {
        Need(rc, P.MarketPrepare);
        var (opp, f, terms) = await LoadAsync(db, rc, reference, P.MarketPrepare);
        OpportunityFlow.Ensure(opp.Status, OpportunityStatus.Preparing, OpportunityStatus.AwaitingOwnerConfirmation, OpportunityStatus.ReadyToPublish,
            OpportunityStatus.Published, OpportunityStatus.Paused);
        var v = new Validator();
        var title = req.Title?.Trim() ?? opp.Title;
        v.Require(title.Length is >= 5 and <= 160, "title", "العنوان بين 5 و160 حرفًا.");
        var description = req.Description is null ? opp.Description : req.Description.Trim();
        v.Require(description is null || description.Length <= 3000, "description", "الوصف أطول من المسموح.");
        v.Require(req.Area is null or (>= 10 and <= 1_000_000), "area", "المساحة غير منطقية.");
        v.Require(req.Bedrooms is null or (>= 0 and <= 20), "bedrooms", "عدد الغرف غير منطقي.");
        v.Require(req.Bathrooms is null or (>= 0 and <= 20), "bathrooms", "عدد دورات المياه غير منطقي.");
        v.Require(req.Readiness is null or "ready" or "under_construction", "readiness", "اختر حالة العقار.");
        v.Require(req.DeliveryMonth is null || System.Text.RegularExpressions.Regex.IsMatch(req.DeliveryMonth, @"^(19|20|21)\d{2}-(0[1-9]|1[0-2])$"), "deliveryMonth", "اختر شهر التسليم.");
        v.Require(req.LocationPrecision is null or "exact" or "approximate", "locationPrecision", "اختر دقة الموقع.");
        v.Require(!(req.LocationPrecision == "exact" && f.Request.LocationDisplayWish != "exact"), "locationPrecision",
            "لم يوافق المالك على عرض الموقع الدقيق؛ يُعرض الموقع التقريبي فقط.");
        var photoIds = req.PhotoIds?.Distinct().ToList();
        if (photoIds is not null)
            v.Require(photoIds.All(id => f.Photos.Any(p => p.Id == id && p.ReviewStatus != FileReviewStatus.Rejected)), "photoIds", "اختر من صور الطلب غير المرفوضة.");
        v.ThrowIfInvalid();

        var before = JsonSerializer.Serialize(new { opp.Title, opp.Description, opp.Area, opp.Bedrooms, opp.Bathrooms, opp.Readiness, opp.DeliveryMonth, opp.PhotoIds, opp.LocationPrecision, opp.Features });
        opp.Title = title;
        opp.Description = string.IsNullOrEmpty(description) ? null : description;
        if (req.Area is not null) opp.Area = req.Area;
        if (req.Bedrooms is not null) opp.Bedrooms = opp.PropertyType is "land" or "office" or "shop" ? null : req.Bedrooms;
        if (req.Bathrooms is not null) opp.Bathrooms = opp.PropertyType == "land" ? null : req.Bathrooms;
        if (req.Readiness is not null) opp.Readiness = opp.PropertyType == "land" ? null : req.Readiness;
        if (req.DeliveryMonth is not null) opp.DeliveryMonth = opp.Readiness == "under_construction" ? req.DeliveryMonth : null;
        if (req.Specs is not null)
        {
            var applied = FieldCatalog.Apply("property", req.Specs, opp.PropertyType, null);
            if (applied.Errors.Count > 0) throw new ValidationFailedException(applied.Errors.ToDictionary(k => k.Key, k => new[] { k.Value }));
            opp.Specs = applied.Answers.Where(kv => kv.Key is not ("area" or "bedrooms" or "bathrooms" or "readiness" or "delivery_month" or "description" or "features"))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        if (req.Features is not null)
        {
            var allowed = FieldCatalog.Options(FieldCatalog.ByKey["features"], opp.PropertyType).Select(o => o.Value).ToHashSet();
            opp.Features = req.Features.Where(allowed.Contains).Distinct().ToList();
        }
        if (photoIds is not null) opp.PhotoIds = photoIds;
        if (req.LocationPrecision is not null)
        {
            opp.LocationPrecision = req.LocationPrecision;
            (opp.PublicLatitude, opp.PublicLongitude) = OpportunityProjection.PublicPoint(opp.ExactLatitude, opp.ExactLongitude, opp.LocationPrecision);
        }
        var after = JsonSerializer.Serialize(new { opp.Title, opp.Description, opp.Area, opp.Bedrooms, opp.Bathrooms, opp.Readiness, opp.DeliveryMonth, opp.PhotoIds, opp.LocationPrecision, opp.Features });
        if (before != after)
        {
            var wasStatus = opp.Status;
            ResetConfirmation(opp, terms, rc, CommissionPolicy.From(config), db);
            opp.StatusChangedAt = clock.UtcNow;
            market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "content_edited",
                opp.Status != wasStatus ? "عدّل الفريق ملخص الفرصة؛ سنرسله لك للتأكيد من جديد" : "عدّل الفريق محتوى الفرصة", visible: opp.Status != wasStatus,
                from: wasStatus.ToString(), to: opp.Status.ToString());
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { saved = true, status = opp.Status });
    }

    private static readonly HashSet<string> StateValues = [FigureStates.Verified, FigureStates.Declared, FigureStates.Estimated];

    private static async Task<IResult> SaveTerms(string reference, TermsForm req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock, IConfiguration config)
    {
        Need(rc, P.MarketPrepare);
        var (opp, f, terms) = await LoadAsync(db, rc, reference, P.MarketPrepare);
        OpportunityFlow.Ensure(opp.Status, OpportunityStatus.Preparing, OpportunityStatus.AwaitingOwnerConfirmation, OpportunityStatus.ReadyToPublish,
            OpportunityStatus.Published, OpportunityStatus.Paused);
        var v = new Validator();
        v.Require(opp.Track != "developer" || (req.Developer is not null && req.Financier is null), "track", "هذه فرصة التزام لدى مطور.");
        v.Require(opp.Track != "financier" || (req.Financier is not null && req.Developer is null), "track", "هذه فرصة عقار مموّل.");
        v.Require(opp.Track != "mixed" || (req.Developer is not null && req.Financier is not null), "track", "أدخل جزء المطور وجزء جهة التمويل منفصلين.");
        foreach (var (name, value) in new (string, decimal?)[]
                 {
                     ("sellerCosts", req.SellerCosts), ("buyerCostsNow", req.BuyerCostsNow), ("buyerCostsLater", req.BuyerCostsLater),
                     ("developer.paidApproved", req.Developer?.PaidApproved), ("developer.remainingBalance", req.Developer?.RemainingBalance),
                     ("developer.arrears", req.Developer?.Arrears), ("developer.reduction", req.Developer?.Reduction), ("developer.installment", req.Developer?.Installment),
                     ("developer.extraPayment", req.Developer?.ExtraPayment), ("financier.salePrice", req.Financier?.SalePrice),
                     ("financier.payoffAmount", req.Financier?.PayoffAmount), ("financier.arrears", req.Financier?.Arrears),
                 })
            v.Require(value is null or >= 0, name, "لا تُقبل مبالغ سالبة.");
        v.Require(req.Developer?.ArrearsPayer is null or "buyer" or "seller", "developer.arrearsPayer", "حدد من يتحمل المتأخرات.");
        v.Require(req.Developer?.InstallmentFrequency is null or "monthly" or "quarterly" or "semiannual" or "annual", "developer.installmentFrequency", "اختر دورية القسط.");
        v.Require((req.States ?? []).Values.All(StateValues.Contains), "states", "حالة الرقم غير معروفة.");
        v.Require(req.TransferConditions is null || req.TransferConditions.Length <= 2000, "transferConditions", "شروط النقل أطول من المسموح.");
        v.Require(req.VerifiedOn is null || req.VerifiedOn <= clock.TodayRiyadh, "verifiedOn", "تاريخ التحقق لا يكون في المستقبل.");
        v.ThrowIfInvalid();

        var input = new TermsInput
        {
            Developer = req.Developer, Financier = req.Financier, SellerCosts = req.SellerCosts, BuyerCostsNow = req.BuyerCostsNow, BuyerCostsLater = req.BuyerCostsLater,
            NeedsNewFinancing = req.NeedsNewFinancing, States = req.States ?? new(),
        };
        var policy = CommissionPolicy.From(config);
        var draft = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId && t.Status == TermsStatus.Draft);
        if (draft is null)
        {
            // Never edit a version the owner saw or confirmed: start a new one.
            ResetConfirmation(opp, terms, rc, policy, db);
            draft = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId && t.Status == TermsStatus.Draft);
            if (draft is null)
            {
                draft = NewTerms(opp, (terms.Count == 0 ? 0 : terms.Max(t => t.VersionNo)) + 1, input, req.TransferConditions?.Trim(), req.VerificationScope?.Trim(), req.VerifiedOn, rc, policy);
                db.OpportunityTerms.Add(draft);
                opp.DraftTermsId = draft.Id;
            }
        }
        ApplyTerms(draft, input, req.TransferConditions?.Trim(), req.VerificationScope?.Trim(), req.VerifiedOn, policy);
        draft.PreparedByUserId = rc.UserId;
        draft.PreparedByLabel = rc.UserName;
        opp.StatusChangedAt = clock.UtcNow;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "terms_edited", $"حدّث الفريق أرقام الإصدار {draft.VersionNo}", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { terms = OpportunityProjection.Terms(draft) });
    }

    private static async Task<IResult> SendToOwner(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketPrepare);
        var (opp, _, terms) = await LoadAsync(db, rc, reference, P.MarketPrepare);
        OpportunityFlow.Ensure(opp.Status, OpportunityStatus.Preparing, OpportunityStatus.Published, OpportunityStatus.Paused);
        var draft = terms.FirstOrDefault(t => t.Id == opp.DraftTermsId && t.Status == TermsStatus.Draft)
                    ?? throw new ConflictException("no_draft", "لا توجد نسخة جديدة لإرسالها. عدّل الأرقام أو المحتوى أولًا.");
        if (string.IsNullOrWhiteSpace(opp.Description)) Validate.Throw("description", "أضف وصفًا قبل إرسال الملخص للمالك.");
        var now = clock.UtcNow;
        draft.Status = TermsStatus.SentToOwner;
        draft.SentToOwnerAt = now;
        var from = opp.Status;
        if (opp.Status == OpportunityStatus.Preparing) opp.Status = OpportunityStatus.AwaitingOwnerConfirmation;
        opp.StatusChangedAt = now;
        var e = market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "sent_to_owner", $"ملخص الفرصة بانتظار تأكيدك (الإصدار {draft.VersionNo})", visible: true,
            body: "راجع الأرقام وشروط النقل ودقة الموقع والصور، ثم أكد الملخص أو اطلب تعديله. لن تُنشر الفرصة قبل تأكيدك.",
            from: from.ToString(), to: opp.Status.ToString());
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(opp.ApplicantUserId, e.Id, $"رهون: ملخص فرصتك {opp.Reference} بانتظار تأكيدك في حسابك.");
        return Results.Ok(new { status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }

    private static async Task<IResult> SaveChecklist(string reference, ChecklistInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        if (!rc.Has(P.MarketReview) && !rc.Has(P.MarketPublish)) throw new ForbiddenException();
        var (opp, f, _) = await LoadAsync(db, rc, reference, P.MarketView);
        var who = TeamScope.Assignees(opp, f.Request);
        if (!rc.CanOn(P.MarketReview, who) && !rc.CanOn(P.MarketPublish, who)) throw new ForbiddenException();
        var allowed = OpportunityFlow.Checklist.Select(c => c.Key).ToHashSet();
        opp.Checklist = (req.Items ?? []).Where(allowed.Contains).Distinct().ToList();
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "checklist", "حدّث الفريق قائمة التحقق قبل النشر", visible: false, data: new { opp.Checklist });
        await db.SaveChangesAsync();
        return Results.Ok(new { checklist = opp.Checklist });
    }

    private static async Task<IResult> Publish(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketPublish);
        var (opp, f, terms) = await LoadAsync(db, rc, reference, P.MarketPublish);
        var blockers = PublishBlockers(opp, f, terms);
        if (blockers.Count > 0) throw new DomainException("publish_blocked", "لا يمكن النشر بعد.", StatusCodes.Status409Conflict, blockers);
        var now = clock.UtcNow;
        var draft = terms.First(t => t.Id == opp.DraftTermsId);
        if (terms.FirstOrDefault(t => t.Id == opp.PublishedTermsId) is { } old) old.Status = TermsStatus.Superseded;
        opp.PublishedTermsId = draft.Id;
        opp.DraftTermsId = null;
        var from = opp.Status;
        var first = opp.FirstPublishedAt is null;
        opp.Status = OpportunityStatus.Published;
        opp.StatusChangedAt = now;
        opp.PublishedAt = now;
        opp.FirstPublishedAt ??= now;
        opp.PublishedByUserId = rc.UserId;
        opp.PauseReason = null;
        var e = market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "published",
            first ? "نُشرت فرصتك للمشترين" : $"نُشرت النسخة المحدثة من فرصتك (الإصدار {draft.VersionNo})", visible: true,
            body: "يتابع الفريق كل اهتمام يصل. الاهتمام لا يعني حجز العقار.", from: from.ToString(), to: nameof(OpportunityStatus.Published));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(opp.ApplicantUserId, e.Id, $"رهون: نُشرت فرصتك {opp.Reference}.");
        return Results.Ok(new { status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status], publicUrl = $"/opportunities/{opp.Reference}" });
    }

    private static async Task<IResult> Pause(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketPublish);
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب الإيقاف.");
        var (opp, _, _) = await LoadAsync(db, rc, reference, P.MarketPublish);
        OpportunityFlow.Ensure(opp.Status, OpportunityStatus.Published);
        opp.Status = OpportunityStatus.Paused;
        opp.StatusChangedAt = clock.UtcNow;
        opp.PauseReason = reason[..Math.Min(reason.Length, 500)];
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "paused", "أُوقف عرض الفرصة مؤقتًا", visible: true, reason: opp.PauseReason,
            from: nameof(OpportunityStatus.Published), to: nameof(OpportunityStatus.Paused));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }

    private static async Task<IResult> Resume(string reference, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketPublish);
        var (opp, _, _) = await LoadAsync(db, rc, reference, P.MarketPublish);
        OpportunityFlow.Ensure(opp.Status, OpportunityStatus.Paused);
        if (opp.PublishedTermsId is null) throw new ConflictException("never_published", "لم تُنشر هذه الفرصة من قبل.");
        opp.Status = OpportunityStatus.Published;
        opp.StatusChangedAt = clock.UtcNow;
        opp.PauseReason = null;
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "resumed", "عادت الفرصة للعرض", visible: true,
            from: nameof(OpportunityStatus.Paused), to: nameof(OpportunityStatus.Published));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }

    private static async Task<IResult> Withdraw(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        Need(rc, P.MarketPublish);
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب السحب.");
        var (opp, _, _) = await LoadAsync(db, rc, reference, P.MarketPublish);
        if (opp.Status is OpportunityStatus.Withdrawn or OpportunityStatus.Completed) throw new ConflictException("invalid_status", "الفرصة مغلقة.");
        var from = opp.Status;
        opp.Status = OpportunityStatus.Withdrawn;
        opp.StatusChangedAt = clock.UtcNow;
        opp.WithdrawReason = reason[..Math.Min(reason.Length, 500)];
        market.Event(opp.OrganizationId, "opportunity", opp.Id, opp.ApplicantUserId, "withdrawn", "سُحبت الفرصة", visible: true, reason: opp.WithdrawReason,
            from: from.ToString(), to: nameof(OpportunityStatus.Withdrawn));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] });
    }

    // ── Interests ──

    private static async Task<IResult> ListInterests(RahoonDbContext db, RequestContext rc, string? status, string? assigned)
    {
        var query = db.Interests.Scoped(db, rc);
        if (status is { Length: > 0 } && Enum.TryParse<InterestStatus>(status, true, out var st)) query = query.Where(i => i.Status == st);
        if (assigned == "me") query = query.Where(i => i.AssignedToUserId == rc.UserId);
        else if (assigned == "none") query = query.Where(i => i.AssignedToUserId == null);
        var rows = await query.OrderByDescending(i => i.CreatedAt).Take(200)
            .Join(db.Opportunities, i => i.OpportunityId, o => o.Id, (i, o) => new { i, o.Reference, o.Title, o.Status })
            .ToListAsync();
        return Results.Ok(rows.Select(x => new
        {
            x.i.Reference, status = x.i.Status, statusLabel = InterestFlow.Labels[x.i.Status], opportunity = x.Reference, opportunityTitle = x.Title,
            opportunityStatusLabel = OpportunityFlow.Labels[x.Status], buyerName = x.i.ContactName, x.i.CreatedAt, assignedTo = x.i.AssignedToLabel,
            hasBuyerRequest = x.i.BuyerRequestId is not null,
        }));
    }

    private static async Task<IResult> GetInterest(string reference, RahoonDbContext db, RequestContext rc)
    {
        var (i, opp) = await LoadInterestAsync(db, rc, reference, P.MarketView);
        var who = TeamScope.Assignees(i, opp);
        var terms = await db.OpportunityTerms.FirstAsync(t => t.Id == i.TermsId);
        var buyer = i.BuyerRequestId is { } bid ? await db.BuyerRequests.FirstOrDefaultAsync(b => b.Id == bid) : null;
        var events = await db.MarketEvents.Where(e => e.SubjectId == i.Id).OrderByDescending(e => e.At).ToListAsync();
        string? phoneMasked;
        using (rc.BeginSystemScope())
            phoneMasked = await db.IndividualProfiles.Where(p => p.UserId == i.ApplicantUserId).Select(p => p.PhoneMasked).FirstOrDefaultAsync();
        var r = OpportunityProjection.Result(terms);
        var fit = buyer is null ? null : MarketCalculator.Fit(new CapacityInput(buyer.AvailableNow, buyer.InstallmentComfort, buyer.InstallmentFrequency, buyer.MaxPrice),
            terms.DueNow, terms.PurchaseTotal, terms.InstallmentMonthlyEquivalent, terms.LargestExtraPayment, terms.NeedsNewFinancing);
        return Results.Ok(new
        {
            i.Reference, status = i.Status, statusLabel = InterestFlow.Labels[i.Status], i.Message, i.ContactPreference, buyerName = i.ContactName, phoneMasked,
            i.CreatedAt, assignedTo = i.AssignedToUserId is null ? null : new { id = i.AssignedToUserId, label = i.AssignedToLabel }, i.CloseReason,
            opportunity = new { opp.Reference, opp.Title, status = opp.Status, statusLabel = OpportunityFlow.Labels[opp.Status] },
            terms = new { terms.VersionNo, current = terms.Id == opp.PublishedTermsId, r.DueNow, r.BuyerTotal, r.Quality },
            buyerRequest = buyer is null ? null : BuyerEndpoints.BuyerDto(buyer, null),
            fit,
            events = events.Select(SaleRequestFile.EventDto),
            actions = new { follow = rc.CanOn(P.MarketFollow, who), assignOthers = rc.Has(P.MarketAssign) },
            notice = "طلب الاهتمام لا يحجز العقار ولا يعد عرضًا ملزمًا. أي عرض أو حجز يأتي في مرحلة لاحقة بشروطه.",
        });
    }

    private static async Task<IResult> InterestContact(string reference, RahoonDbContext db, RequestContext rc, PiiProtector pii, AuditLog audit)
    {
        var (i, _) = await LoadInterestAsync(db, rc, reference, P.MarketFollow);
        string phone;
        using (rc.BeginSystemScope())
            phone = pii.Unprotect(await db.IndividualProfiles.Where(p => p.UserId == i.ApplicantUserId).Select(p => p.PhoneEnc).FirstAsync());
        await audit.RecordAsync(new AuditEntry("market.contact_reveal", "إظهار جوال المشتري المهتم", SubjectType: "interest", SubjectReference: reference));
        await db.SaveChangesAsync();
        return Results.Ok(new { phone });
    }

    /// <summary>An interest the caller may see (404 otherwise) and act on with <paramref name="permission"/> (403 otherwise).</summary>
    private static async Task<(Interest Interest, Opportunity Opportunity)> LoadInterestAsync(RahoonDbContext db, RequestContext rc, string reference, string permission)
    {
        var i = await db.Interests.FirstOrDefaultAsync(x => x.Reference == reference) ?? throw new NotFoundException();
        var opp = await db.Opportunities.FirstAsync(o => o.Id == i.OpportunityId);
        TeamScope.Need(rc, permission, TeamScope.Assignees(i, opp));
        return (i, opp);
    }

    private static async Task<IResult> AssignInterest(string reference, AssignInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        var (i, _) = await LoadInterestAsync(db, rc, reference, P.MarketFollow);
        var (id, label) = await TeamMarketEndpoints.AssigneeAsync(db, rc, req.UserId);
        i.AssignedToUserId = id;
        i.AssignedToLabel = label;
        market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "assigned", $"أُسند الاهتمام إلى {label}", visible: false);
        await db.SaveChangesAsync();
        return Results.Ok(new { assignedTo = new { id, label } });
    }

    private static async Task<IResult> FollowUp(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var (i, _) = await LoadInterestAsync(db, rc, reference, P.MarketFollow);
        if (i.Status != InterestStatus.Received) throw new ConflictException("invalid_status", $"الاهتمام في حالة «{InterestFlow.Labels[i.Status]}».");
        i.Status = InterestStatus.InFollowUp;
        i.StatusChangedAt = clock.UtcNow;
        if (i.AssignedToUserId is null) { i.AssignedToUserId = rc.UserId; i.AssignedToLabel = rc.UserName; }
        var e = market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "follow_up", "بدأ فريق رهون متابعة اهتمامك", visible: true,
            body: req.Note?.Trim() is { Length: > 0 } n ? n[..Math.Min(n.Length, 1000)] : "سيتواصل معك الفريق لشرح الأرقام والخطوات التالية.",
            from: nameof(InterestStatus.Received), to: nameof(InterestStatus.InFollowUp));
        await db.SaveChangesAsync();
        await market.NotifyBySmsAsync(i.ApplicantUserId, e.Id, $"رهون: بدأ الفريق متابعة اهتمامك {i.Reference}.");
        return Results.Ok(new { status = i.Status, statusLabel = InterestFlow.Labels[i.Status] });
    }

    private static async Task<IResult> CloseInterest(string reference, DecisionInput req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        var reason = req.Reason?.Trim() ?? "";
        if (reason.Length < 5) Validate.Throw("reason", "اكتب سبب الإغلاق؛ سيظهر للمشتري.");
        var (i, _) = await LoadInterestAsync(db, rc, reference, P.MarketFollow);
        if (i.Status is InterestStatus.Closed or InterestStatus.Withdrawn) throw new ConflictException("invalid_status", "الاهتمام مغلق.");
        var from = i.Status;
        i.Status = InterestStatus.Closed;
        i.StatusChangedAt = clock.UtcNow;
        i.CloseReason = reason[..Math.Min(reason.Length, 500)];
        market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "closed", "أُغلق طلب الاهتمام", visible: true, reason: i.CloseReason,
            from: from.ToString(), to: nameof(InterestStatus.Closed));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = i.Status, statusLabel = InterestFlow.Labels[i.Status] });
    }

    private static async Task<IResult> NoteInterest(string reference, NoteInput req, RahoonDbContext db, RequestContext rc, MarketService market)
    {
        var note = req.Note?.Trim() ?? "";
        if (note.Length < 2) Validate.Throw("note", "اكتب الملاحظة.");
        var (i, _) = await LoadInterestAsync(db, rc, reference, P.MarketFollow);
        market.Event(i.OrganizationId, "interest", i.Id, i.ApplicantUserId, "note", "ملاحظة داخلية", visible: false, body: note[..Math.Min(note.Length, 2000)]);
        await db.SaveChangesAsync();
        return Results.Ok(new { ok = true });
    }
}
