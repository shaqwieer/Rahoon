using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>
/// Public discovery (Phase 2): the list, the map and the comparison. All three read through <see cref="DiscoveryQuery"/>
/// (published opportunities, their published terms, public coordinates only). A signed-in buyer can ask for «match=me»: their
/// own profile is read from the session; no endpoint accepts another person's reference.
/// </summary>
public static class DiscoveryEndpoints
{
    public const int MapCap = 500;
    public const int CompareMax = 4;

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/market");
        g.MapGet("/opportunities", Search);
        g.MapGet("/opportunities/map", MapMarkers);
        g.MapGet("/compare", Compare);
    }

    private sealed record Resolved(SearchCriteria Criteria, RankingPreferences? Prefs, object? Profile, List<Guid> SavedIds);

    /// <summary>The person's saved ids and, with «match=me», their own buyer profile merged into the criteria. Read before the system scope.</summary>
    private static async Task<Resolved> ResolveAsync(SearchCriteria c, RahoonDbContext db, RequestContext rc)
    {
        List<Guid> saved = [];
        if (rc.IsIndividual) saved = await db.SavedOpportunities.Where(s => s.ApplicantUserId == rc.UserId && s.RemovedAt == null).Select(s => s.OpportunityId).ToListAsync();
        if (!c.MatchMe) return new Resolved(c, null, null, saved);
        var r = await Matching.OwnLiveRequestAsync(db, rc);
        if (r is null) return new Resolved(c, null, new { applied = false, reason = rc.IsIndividual ? "no_buyer_request" : "signed_out" }, saved);
        return new Resolved(Matching.WithProfile(c, r), Matching.Preferences(r), new { applied = true, r.Reference, revision = r.PreferencesRevision }, saved);
    }

    public static string SortExplanation(string sort, bool budget, bool prefs) => sort switch
    {
        "now" => "الأقل في المطلوب منك الآن أولًا.",
        "total" => "الأقل في إجمالي الالتزام أو سعر الشراء أولًا.",
        "relevance" => string.Join("، ", new[]
        {
            prefs ? "الأكثر تحقيقًا لتفضيلاتك" : null,
            "الأرقام المكتملة التي راجعها الفريق أولًا",
            budget ? "ثم الأقل في المطلوب الآن" : null,
            "ثم الأحدث نشرًا.",
        }.Where(x => x is not null)),
        _ => "الأحدث نشرًا أولًا.",
    };

    private static async Task<IResult> Search(HttpRequest http, RahoonDbContext db, RequestContext rc)
    {
        var asked = SearchCriteria.Parse(http.Query);
        var (c, prefs, profile, saved) = await ResolveAsync(asked, db, rc);
        using var _ = rc.BeginSystemScope();
        var cap = c.Capacity;
        var (passing, incomplete) = DiscoveryQuery.Budget(DiscoveryQuery.Attributes(DiscoveryQuery.Published(db), c), cap);
        var total = await passing.CountAsync();
        var excluded = cap.Any ? await incomplete.CountAsync() : 0;
        var withoutLocation = await passing.CountAsync(x => x.O.PublicLatitude == null || x.O.PublicLongitude == null);
        var sort = c.EffectiveSort;
        var rows = await DiscoveryQuery.Order(passing, sort, cap.Any, prefs).Skip((c.Page - 1) * c.PageSize).Take(c.PageSize).ToListAsync();

        // Facets count the other filters (a facet never counts itself), with the same visibility and budget.
        var cityFacet = await DiscoveryQuery.Budget(DiscoveryQuery.Attributes(DiscoveryQuery.Published(db), c, skipCity: true), cap).Passing
            .GroupBy(x => x.O.City).Select(g => new { key = g.Key, count = g.Count() }).ToListAsync();
        var typeFacet = await DiscoveryQuery.Budget(DiscoveryQuery.Attributes(DiscoveryQuery.Published(db), c, skipTypes: true), cap).Passing
            .GroupBy(x => x.O.PropertyType).Select(g => new { key = g.Key, count = g.Count() }).ToListAsync();

        var items = rows.Select(x => new
        {
            card = OpportunityProjection.Card(x.O, x.T, saved.Contains(x.O.Id)),
            fit = cap.Any ? Affordability.Classify(cap, x.T) : null,
            match = c.MatchMe && prefs is not null ? Matching.Explain(x.O, c, prefs) : null,
        });
        return Results.Ok(new
        {
            items, total, page = c.Page, pageSize = c.PageSize, pages = (int)Math.Ceiling(total / (double)c.PageSize),
            excludedIncomplete = excluded, withoutLocation, sort, sortExplanation = SortExplanation(sort, cap.Any, prefs is { Any: true }),
            // The URL state echoes what was asked, never the profile values merged in: a shared link carries «match=me», not a buyer's numbers.
            query = asked.ToQuery(sortAndPage: true, includePage: true), profile,
            facets = new
            {
                cities = cityFacet.OrderByDescending(f => f.count).ThenBy(f => f.key, StringComparer.Ordinal)
                    .Select(f => new { f.key, label = FieldCatalog.City(f.key)?.Label ?? f.key, f.count }),
                types = typeFacet.OrderByDescending(f => f.count).ThenBy(f => f.key, StringComparer.Ordinal)
                    .Select(f => new { f.key, label = FieldCatalog.Label(FieldCatalog.PropertyTypes, f.key), f.count }),
            },
        });
    }

    /// <summary>
    /// Map markers for the same filtered set as the list (bounds included when given). Only public points: the exact point when the
    /// owner agreed to show it, otherwise the centre of a ~1 km cell (<see cref="OpportunityProjection.PublicPoint"/>).
    /// </summary>
    private static async Task<IResult> MapMarkers(HttpRequest http, RahoonDbContext db, RequestContext rc)
    {
        var (c, prefs, _, _) = await ResolveAsync(SearchCriteria.Parse(http.Query), db, rc);
        using var _ = rc.BeginSystemScope();
        var cap = c.Capacity;
        var (passing, _) = DiscoveryQuery.Budget(DiscoveryQuery.Attributes(DiscoveryQuery.Published(db), c), cap);
        var total = await passing.CountAsync();
        var located = passing.Where(x => x.O.PublicLatitude != null && x.O.PublicLongitude != null);
        var rows = await DiscoveryQuery.Order(located, c.EffectiveSort, cap.Any, prefs).Take(MapCap + 1)
            .Select(x => new
            {
                x.O.Reference, x.O.Title, x.O.City, x.O.District, x.O.PropertyType, lat = x.O.PublicLatitude!.Value, lng = x.O.PublicLongitude!.Value,
                precision = x.O.LocationPrecision, x.T.DueNow, x.T.PurchaseTotal, x.O.PhotoIds, x.O.IsDemo,
            }).ToListAsync();
        var capped = rows.Count > MapCap;
        var markers = rows.Take(MapCap).Select(m => new
        {
            reference = m.Reference, title = m.Title, cityLabel = FieldCatalog.City(m.City)?.Label ?? m.City, district = m.District,
            propertyTypeLabel = FieldCatalog.Label(FieldCatalog.PropertyTypes, m.PropertyType), lat = m.lat, lng = m.lng,
            precision = m.precision == "exact" ? "exact" : "approximate", dueNow = m.DueNow, buyerTotal = m.PurchaseTotal,
            coverUrl = m.PhotoIds.Count > 0 ? OpportunityProjection.PhotoUrl(m.PhotoIds[0]) : null, isDemo = m.IsDemo,
        }).ToList();
        var locatedCount = capped ? await located.CountAsync() : markers.Count;
        return Results.Ok(new { markers, total, located = locatedCount, withoutLocation = total - locatedCount, capped, cap = MapCap });
    }

    // ── Comparison ──

    /// <summary>A comparison cell: a value, «unknown» (never 0) or «not_applicable» (a different path, not a comparable zero).</summary>
    public sealed record Cell(string State, decimal? Value, string? Text);

    private static Cell V(decimal? v, string? text = null) => v is null ? new Cell("unknown", null, text ?? "غير معروف بعد") : new Cell("value", v, text);
    private static Cell NA(string text) => new("not_applicable", null, text);
    private static Cell T(string text) => new("value", null, text);

    private static async Task<IResult> Compare(string? refs, RahoonDbContext db, RequestContext rc)
    {
        var list = (refs ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(r => r.Length <= 20).Distinct().Take(CompareMax).ToList();
        var buyer = await Matching.OwnLiveRequestAsync(db, rc);
        var cap = buyer is null ? null : Matching.Capacity(buyer);
        using var _ = rc.BeginSystemScope();
        var rows = await DiscoveryQuery.Published(db).Where(x => list.Contains(x.O.Reference)).ToListAsync();
        var approvals = await PublicApprovalsAsync(db, rows.Select(r => r.O.SaleRequestId).ToList());
        var items = list.Select(reference =>
        {
            var x = rows.FirstOrDefault(r => r.O.Reference == reference);
            // Withdrawn, paused, reserved or never published: say only that it is not available — nothing it used to show.
            if (x is null) return (object)new { reference, available = false };
            var o = x.O;
            var t = x.T;
            var bank = o.Track == "financier";
            var freq = FieldCatalog.Label(FieldCatalog.Frequencies, t.InstallmentFrequency);
            return new
            {
                reference, available = true, title = o.Title, isDemo = o.IsDemo, track = o.Track, trackLabel = FieldCatalog.Label(OpportunityProjection.Tracks, o.Track),
                coverUrl = o.PhotoIds.Count > 0 ? OpportunityProjection.PhotoUrl(o.PhotoIds[0]) : null,
                cells = new Dictionary<string, Cell>
                {
                    ["dueNow"] = V(t.DueNow, t.NeedsNewFinancing ? "للشراء النقدي" : null),
                    ["buyerTotal"] = V(t.PurchaseTotal, bank ? "سعر الشراء مع التكاليف" : "المطلوب الآن + الرصيد + التكاليف اللاحقة"),
                    ["futureBalance"] = bank ? NA("لا رصيد لمطور؛ يُسدَّد تمويل صاحب العقار عند الإتمام") : V(t.FutureBalance),
                    ["installment"] = bank ? NA("قسط صاحب العقار لا ينتقل إليك؛ قسطك إن موّلت تحدده جهتك")
                        : t.Installment is { } inst ? new Cell("value", inst, freq) : t.FutureBalance == 0 ? NA("لا أقساط") : V(null),
                    ["monthlyEquivalent"] = bank ? NA("لا ينطبق") : t.InstallmentMonthlyEquivalent is { } me ? new Cell("value", me, "للمقارنة فقط") : t.FutureBalance == 0 ? NA("لا أقساط") : V(null),
                    ["extraPayments"] = bank ? NA("لا ينطبق")
                        : t.LargestExtraPayment is { } ex ? new Cell("value", ex, $"{(t.ExtraPaymentRecurrence switch { "annual" => "سنويًا", "once" => "مرة واحدة", _ => "تكرارها غير مسجل" })}{(t.NextExtraPaymentDate is { Length: > 0 } d ? $" · القادمة {d}" : "")}")
                        : T("لا توجد دفعة إضافية مسجلة"),
                    ["remainingMonths"] = bank ? NA("لا ينطبق") : t.RemainingMonths is { } rm ? new Cell("value", rm, "شهرًا") : t.FutureBalance == 0 ? NA("لا أقساط") : V(null),
                    ["verification"] = T(t.Quality switch
                    {
                        "complete_verified" => $"راجع الفريق الأرقام{(t.VerifiedOn is { } vo ? $" في {vo:yyyy-MM-dd}" : "")}",
                        "complete_estimate" => "تقدير مبدئي؛ لم يتحقق الفريق من كل الأرقام",
                        _ => "تقدير غير مكتمل",
                    }),
                    ["location"] = T($"{FieldCatalog.City(o.City)?.Label ?? o.City}{(o.District is { } dd ? $"، {dd}" : "")}{(o.PublicLatitude is null ? "" : o.LocationPrecision == "exact" ? " · موقع دقيق" : " · موقع تقريبي")}"),
                    ["delivery"] = o.PropertyType == "land" ? NA("أرض") : o.Readiness == "ready" ? T("جاهز")
                        : o.DeliveryMonth is { } dm ? T($"تحت الإنشاء · التسليم المتوقع {dm}") : o.Readiness is null ? V(null) : T("تحت الإنشاء · موعد التسليم غير معروف"),
                    ["financing"] = T(t.NeedsNewFinancing ? "يمكن الشراء نقدًا أو بتمويل جديد يخضع لموافقة جهتك" : bank ? "شراء نقدي" : "انتقال الالتزام بموافقة المطور"),
                },
                approvals = approvals.GetValueOrDefault(o.SaleRequestId) ?? [],
                fit = cap is { Any: true } ? Affordability.Classify(cap, t) : null,
            };
        }).ToList();
        return Results.Ok(new { items, max = CompareMax, hasProfile = cap is { Any: true } });
    }

    /// <summary>Public-safe approval summary per sale request: the party's own decision on a transfer. No documents, no party name.</summary>
    public static async Task<Dictionary<Guid, List<object>>> PublicApprovalsAsync(RahoonDbContext db, List<Guid> saleRequestIds)
    {
        var obligations = await db.SaleObligations.Where(o => saleRequestIds.Contains(o.SaleRequestId) && o.RemovedAt == null).OrderBy(o => o.SortOrder).ToListAsync();
        var approvals = await db.ExternalApprovals.Where(a => saleRequestIds.Contains(a.SaleRequestId)).OrderBy(a => a.RecordedAt).ToListAsync();
        return obligations.GroupBy(o => o.SaleRequestId).ToDictionary(g => g.Key, g => g.Select(o =>
        {
            var a = approvals.LastOrDefault(x => x.ObligationId == o.Id);
            var status = a?.Status ?? ExternalApprovalStatus.NotRequested;
            return (object)new
            {
                party = o.Kind == "developer" ? "موافقة المطور على النقل" : "موافقة جهة التمويل",
                status, statusLabel = ExternalApprovalLabels.Labels[status], conditions = status == ExternalApprovalStatus.Conditional ? a?.Conditions : null,
                expiresOn = a?.ExpiresOn,
            };
        }).ToList());
    }
}
