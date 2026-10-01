using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Market;
using Rahoon.Api.Modules.Market.Discovery;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;
using Shape = Rahoon.Api.Tests.Infrastructure.DiscoveryRows.Shape;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 2 acceptance through the API: one spine for list, map, facets and comparison; strict affordability in SQL agreeing with
/// the C# classifier; public points only; explained matching with revisions; favorites, comparison and saved searches with
/// unavailable opportunities, duplicates and other people's ids. Each test works in its own city.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DiscoveryTests(ApiFixture api)
{
    private static List<JsonNode> Items(JsonNode? body) => body!["items"]!.AsArray().Select(i => i!).ToList();
    private static string Ref(JsonNode item) => item["card"]!["reference"]!.GetValue<string>();

    private async Task<List<string>> AllRefsAsync(TestClient c, string query)
    {
        var refs = new List<string>();
        for (var page = 1; page < 50; page++)
        {
            var (s, body) = await c.GetAsync($"/api/market/opportunities?{query}&pageSize=2&page={page}");
            Assert.Equal(HttpStatusCode.OK, s);
            refs.AddRange(Items(body).Select(Ref));
            if (page >= body!["pages"]!.GetValue<int>()) break;
        }
        return refs;
    }

    private static readonly Shape[] Shapes =
    [
        new() { DueNow = 300_000, Total = 800_000, FutureBalance = 500_000, Installment = 5_000, Frequency = "monthly", RemainingMonths = 60 },
        new() { DueNow = 300_000, Total = 900_000, FutureBalance = 600_000, Installment = 15_000, Frequency = "quarterly", ExtraPayment = 40_000, ExtraRecurrence = "annual", RemainingMonths = 72 },
        new() { DueNow = null, Total = null, FutureBalance = 400_000, Installment = 4_000, Frequency = "monthly", Quality = "incomplete" },
        new() { DueNow = 200_000, Total = 600_000, FutureBalance = 400_000, Installment = null, Frequency = null },
        new() { DueNow = 250_000, Total = 250_000, FutureBalance = 0, Track = "financier", NeedsNewFinancing = true },
        new() { DueNow = 280_000, Total = 700_000, FutureBalance = 420_000, Installment = 4_000, Frequency = "monthly", ExtraPayment = 30_000, ExtraRecurrence = null, RemainingMonths = 40 },
        new() { DueNow = 350_000, Total = 950_000, FutureBalance = 600_000, Installment = 6_000, Frequency = "monthly", ExtraPayment = 100_000, ExtraRecurrence = "once", RemainingMonths = 36 },
        new() { DueNow = 100_000, Total = null, FutureBalance = 300_000, Installment = 2_500, Frequency = "semiannual", RemainingMonths = 120, Quality = "complete_verified" },
    ];

    [Fact]
    public async Task Strict_budget_in_sql_agrees_with_the_classifier_on_every_row_and_profile()
    {
        const string city = "abha";
        foreach (var s in Shapes) await DiscoveryRows.InsertAsync(api, city, s);
        var rows = await api.WithDbAsync(db => db.Opportunities.Where(o => o.City == city && o.Status == OpportunityStatus.Published)
            .Join(db.OpportunityTerms, o => o.PublishedTermsId, t => t.Id, (o, t) => new { o.Reference, t }).ToListAsync());
        Assert.Equal(Shapes.Length, rows.Count);

        (string Query, CapacityProfile Cap)[] profiles =
        [
            ("maxNow=400000", new(400_000, null, null, null)),
            ("maxNow=320000", new(320_000, null, null, null)),
            ("maxInstallment=6000", new(null, 6_000, "monthly", null)),
            ("maxInstallment=5000", new(null, 5_000, "monthly", null)),
            ("maxInstallment=9000&freq=quarterly&maxTotal=900000&maxNow=320000", new(320_000, 9_000, "quarterly", 900_000)),
            ("maxTerm=48", new(null, null, null, null, 48)),
            ("maxNow=450000&maxInstallment=8400", new(450_000, 8_400, "monthly", null)),
        ];
        var c = api.Client();
        foreach (var (query, cap) in profiles)
        {
            var expected = rows.Where(x => Affordability.Classify(cap, x.t).Outcome == AffordabilityOutcome.Fits).Select(x => x.Reference).Order().ToList();
            var incomplete = rows.Count(x => Affordability.Classify(cap, x.t).Outcome == AffordabilityOutcome.Incomplete);
            var (_, body) = await c.GetAsync($"/api/market/opportunities?city={city}&{query}&pageSize=24");
            Assert.Equal(expected, Items(body).Select(Ref).Order().ToList());
            Assert.Equal(incomplete, body!["excludedIncomplete"]!.GetValue<int>());
            Assert.All(Items(body), i => Assert.Equal("fits", i["fit"]!["outcome"]!.GetValue<string>()));
        }

        // The quarterly row with 40,000 a year: a comfortable 5,000 monthly average, but 100,000 a year > 60,000.
        var quarterly = rows.Single(x => x.t.AnnualExtraPayment == 40_000).Reference;
        var (_, tight) = await c.GetAsync($"/api/market/opportunities?city={city}&maxInstallment=5000&pageSize=24");
        Assert.DoesNotContain(Items(tight), i => Ref(i) == quarterly);
        var (_, fit) = await c.PostAsync($"/api/market/calc/opportunities/{quarterly}/fit", new { installmentComfort = 5000, installmentFrequency = "monthly" });
        Assert.Equal("does_not_fit", fit!["fit"]!["outcome"]!.GetValue<string>());
        Assert.Contains(fit["fit"]!["limits"]!.AsArray(), l => l!.GetValue<string>().Contains("الدفعة السنوية"));
    }

    [Fact]
    public async Task List_and_map_are_the_same_set_and_pages_are_stable()
    {
        const string city = "tabuk";
        var inserted = new List<DiscoveryRows.Inserted>();
        for (var i = 0; i < 6; i++)
            inserted.Add(await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 300_000, Total = 900_000, FutureBalance = 600_000, Installment = 5_000, Frequency = "monthly" },
                lat: 28.38 + i * 0.01, lng: 36.56 + i * 0.01, precision: i % 2 == 0 ? "approximate" : "exact"));
        var noPoint = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 310_000, Total = 900_000, FutureBalance = 600_000, Installment = 5_000, Frequency = "monthly" });
        var tooDear = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 900_000, Total = 900_000, FutureBalance = 0 }, lat: 28.4, lng: 36.6);

        var c = api.Client();
        const string q = $"city={city}&maxNow=400000&sort=now";
        var paged = await AllRefsAsync(c, q);
        var paged2 = await AllRefsAsync(c, q);
        Assert.Equal(paged, paged2);                       // same order every time (ties broken by id)
        Assert.Equal(paged.Count, paged.Distinct().Count()); // no page overlap
        var (_, whole) = await c.GetAsync($"/api/market/opportunities?{q}&pageSize=24");
        Assert.Equal(Items(whole).Select(Ref).ToList(), paged);
        Assert.Equal(7, whole!["total"]!.GetValue<int>());
        Assert.Equal(1, whole["withoutLocation"]!.GetValue<int>());
        Assert.DoesNotContain(tooDear.Reference, paged);

        var (_, map) = await c.GetAsync($"/api/market/opportunities/map?{q}");
        var markers = map!["markers"]!.AsArray().Select(m => m!["reference"]!.GetValue<string>()).ToList();
        Assert.Equal(paged.Where(r => r != noPoint.Reference).Order(), markers.Order());
        Assert.Equal(7, map["total"]!.GetValue<int>());
        Assert.Equal(1, map["withoutLocation"]!.GetValue<int>());

        // «Search this area»: with bounds the list applies them too, so both still agree.
        const string area = $"{q}&bbox=28.3,36.5,28.405,36.605";
        var inArea = await AllRefsAsync(c, area);
        var (_, mapArea) = await c.GetAsync($"/api/market/opportunities/map?{area}");
        Assert.Equal(inArea.Order(), mapArea!["markers"]!.AsArray().Select(m => m!["reference"]!.GetValue<string>()).Order());
        Assert.True(inArea.Count is > 0 and < 6);

        // The echoed URL state is canonical, so a reload or a shared link gives the same query.
        Assert.Contains("city=tabuk", whole["query"]!.GetValue<string>());
        Assert.Contains("sort=now", whole["query"]!.GetValue<string>());
        var (_, again) = await c.GetAsync($"/api/market/opportunities?{whole["query"]!.GetValue<string>()}&pageSize=24");
        Assert.Equal(Items(whole).Select(Ref), Items(again).Select(Ref));

        // Facets count with the same visibility and budget (the dear one never counts).
        Assert.Equal(7, whole["facets"]!["cities"]!.AsArray().Single(f => f!["key"]!.GetValue<string>() == city)!["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task Map_bounds_and_payloads_never_reveal_an_exact_private_point_or_unpublished_opportunities()
    {
        const string city = "hail";
        const double lat = 27.51234, lng = 41.69876; // public cell centre: 27.515, 41.695
        var shape = new Shape { DueNow = 200_000, Total = 500_000, FutureBalance = 300_000, Installment = 3_000, Frequency = "monthly" };
        var approx = await DiscoveryRows.InsertAsync(api, city, shape, lat: lat, lng: lng, precision: "approximate");
        var exact = await DiscoveryRows.InsertAsync(api, city, shape, lat: 27.6, lng: 41.8, precision: "exact");
        var hidden = new List<DiscoveryRows.Inserted>();
        foreach (var st in new[] { OpportunityStatus.Preparing, OpportunityStatus.Paused, OpportunityStatus.Withdrawn, OpportunityStatus.ReadyToPublish })
            hidden.Add(await DiscoveryRows.InsertAsync(api, city, shape, status: st, lat: 27.55, lng: 41.75, precision: "exact"));

        var c = api.Client();
        var (_, map) = await c.GetAsync($"/api/market/opportunities/map?city={city}");
        var raw = map!.ToJsonString();
        Assert.DoesNotContain("27.51234", raw);
        Assert.DoesNotContain("41.69876", raw);
        Assert.DoesNotContain("exactLatitude", raw, StringComparison.OrdinalIgnoreCase);
        var m = map["markers"]!.AsArray().Single(x => x!["reference"]!.GetValue<string>() == approx.Reference)!;
        Assert.Equal(27.515, m["lat"]!.GetValue<double>(), 6);
        Assert.Equal(41.695, m["lng"]!.GetValue<double>(), 6);
        Assert.Equal("approximate", m["precision"]!.GetValue<string>());
        Assert.Equal(27.6, map["markers"]!.AsArray().Single(x => x!["reference"]!.GetValue<string>() == exact.Reference)!["lat"]!.GetValue<double>(), 6);
        foreach (var h in hidden) Assert.DoesNotContain(h.Reference, raw);

        // A window around the exact point but not the public centre finds nothing: bounds apply to public points only.
        var (_, probe) = await c.GetAsync($"/api/market/opportunities/map?city={city}&bbox=27.511,41.697,27.513,41.700");
        Assert.Empty(probe!["markers"]!.AsArray());
        var (_, probeList) = await c.GetAsync($"/api/market/opportunities?city={city}&bbox=27.511,41.697,27.513,41.700");
        Assert.Equal(0, probeList!["total"]!.GetValue<int>());
        var (_, centre) = await c.GetAsync($"/api/market/opportunities/map?city={city}&bbox=27.514,41.694,27.516,41.696");
        Assert.Single(centre!["markers"]!.AsArray());

        // Invalid or huge windows are refused, not silently widened.
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/market/opportunities/map?bbox=1,2,3")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/market/opportunities/map?bbox=10,10,80,80")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/market/opportunities?bbox=30,40,20,50")).Status);

        // The detail and the comparison don't serve unpublished ones either.
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/market/opportunities/{hidden[1].Reference}")).Status);
        var (_, cmp) = await c.GetAsync($"/api/market/compare?refs={hidden[2].Reference}");
        var only = cmp!["items"]!.AsArray().Single()!.AsObject();
        Assert.False(only["available"]!.GetValue<bool>());
        Assert.Equal(["available", "reference"], only.Select(kv => kv.Key).Order());
    }

    private static object BuyerSave(Guid draft, decimal availableNow, string city, int? bedroomsMin = null, decimal? areaMax = null) => new
    {
        clientDraftId = draft, availableNow, installmentComfort = 6000, installmentFrequency = "monthly", purchaseMode = "cash",
        cities = new[] { city }, propertyTypes = new[] { "apartment" }, bedroomsMin, areaMax, readiness = "any",
    };

    private async Task<(TestClient Buyer, string Ref, Guid Draft)> BuyerAsync(string city, decimal availableNow, int? bedroomsMin = null)
    {
        var buyer = await SellerAsync(api, name: "مشتري اكتشاف");
        var draft = Guid.NewGuid();
        var (_, created) = await Ok(buyer.PostAsync("/api/market/buyer-requests", BuyerSave(draft, availableNow, city, bedroomsMin)));
        var reference = created!["reference"]!.GetValue<string>();
        await Ok(buyer.PostAsync($"/api/market/buyer-requests/{reference}/submit", new { contactName = "مشتري اكتشاف", acceptDeclarations = true }));
        return (buyer, reference, draft);
    }

    [Fact]
    public async Task Matching_explains_itself_and_follows_preference_edits_with_a_new_revision()
    {
        const string city = "najran";
        var small = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 300_000, Total = 800_000, FutureBalance = 500_000, Installment = 4_000, Frequency = "monthly" },
            area: 120, bedrooms: 3);
        var large = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 350_000, Total = 950_000, FutureBalance = 600_000, Installment = 5_000, Frequency = "monthly" },
            area: 200, bedrooms: 4, readiness: "under_construction", deliveryMonth: "2027-06");
        var villa = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 100_000, Total = 500_000, FutureBalance = 400_000, Installment = 2_000, Frequency = "monthly" },
            propertyType: "villa");
        var unknown = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = null, FutureBalance = 400_000, Installment = 2_000, Frequency = "monthly", Quality = "incomplete" });

        var (buyer, reference, draft) = await BuyerAsync(city, 400_000, bedroomsMin: 4);
        var (_, mine) = await Ok(buyer.GetAsync("/api/market/buyer-requests/mine"));
        Assert.Equal(1, mine!["matching"]!["revision"]!.GetValue<int>());
        var refs = Items(new JsonObject { ["items"] = mine["suggestions"]!.DeepClone() }).Select(Ref).ToList();
        Assert.Equal([large.Reference, small.Reference], refs);            // 4 bedrooms ranks first
        Assert.DoesNotContain(villa.Reference, refs);                       // type is mandatory eligibility
        Assert.Contains(mine["incomplete"]!.AsArray(), i => i!["card"]!["reference"]!.GetValue<string>() == unknown.Reference);
        var first = mine["suggestions"]![0]!;
        Assert.Contains(first["match"]!["preferences"]!.AsArray(), p => p!.GetValue<string>().Contains("4 غرف"));
        Assert.Contains(first["match"]!["eligibility"]!.AsArray(), p => p!.GetValue<string>().Contains("نجران"));
        Assert.Contains(first["fit"]!["reasons"]!.AsArray(), p => p!.GetValue<string>().Contains("المطلوب الآن"));
        Assert.DoesNotContain("%", mine["suggestions"]!.ToJsonString()); // no invented percentage

        // Edit preferences: smaller area preferred, no bedroom minimum, less cash → new revision, new order and a non-fit.
        var edit = BuyerSave(draft, 320_000, city, bedroomsMin: null, areaMax: 150);
        await Ok(buyer.PutAsync($"/api/market/buyer-requests/{reference}", edit));
        var (_, after) = await Ok(buyer.GetAsync("/api/market/buyer-requests/mine"));
        Assert.Equal(2, after!["matching"]!["revision"]!.GetValue<int>());
        Assert.Equal([small.Reference], after["suggestions"]!.AsArray().Select(i => i!["card"]!["reference"]!.GetValue<string>()));
        Assert.Equal(1, after["matching"]!["doesNotFit"]!.GetValue<int>());
        // Saving the same values again is not a new revision.
        await Ok(buyer.PutAsync($"/api/market/buyer-requests/{reference}", edit));
        var (_, same) = await Ok(buyer.GetAsync("/api/market/buyer-requests/mine"));
        Assert.Equal(2, same!["matching"]!["revision"]!.GetValue<int>());
    }

    [Fact]
    public async Task Match_me_uses_only_the_signed_in_persons_own_profile()
    {
        const string city = "jazan";
        var cheap = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 150_000, Total = 600_000, FutureBalance = 450_000, Installment = 3_000, Frequency = "monthly" });
        var dear = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 450_000, Total = 900_000, FutureBalance = 450_000, Installment = 3_000, Frequency = "monthly" });
        var (poor, poorRef, _) = await BuyerAsync(city, 200_000);
        var (rich, richRef, _) = await BuyerAsync(city, 500_000);

        var (_, p) = await poor.GetAsync($"/api/market/opportunities?match=me&city={city}");
        Assert.Equal(poorRef, p!["profile"]!["reference"]!.GetValue<string>());
        Assert.Equal([cheap.Reference], Items(p).Select(Ref));
        // A URL naming someone else's request is ignored: the profile is always the session's own.
        var (_, r) = await rich.GetAsync($"/api/market/opportunities?match=me&city={city}&buyer={poorRef}&profile={poorRef}");
        Assert.Equal(richRef, r!["profile"]!["reference"]!.GetValue<string>());
        Assert.Equal(new[] { cheap.Reference, dear.Reference }.Order(), Items(r).Select(Ref).Order());
        // The echoed URL state carries «match=me», never the buyer's numbers.
        Assert.DoesNotContain("maxNow", r["query"]!.GetValue<string>());
        Assert.Contains("match=me", r["query"]!.GetValue<string>());
        // Signed out: nothing is applied.
        var (_, anon) = await api.Client().GetAsync($"/api/market/opportunities?match=me&city={city}");
        Assert.False(anon!["profile"]!["applied"]!.GetValue<bool>());
        Assert.Equal(2, anon["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Favorites_and_comparison_show_unavailable_opportunities_honestly()
    {
        const string city = "khamis";
        var dev = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 300_000, Total = 900_000, FutureBalance = 600_000, Installment = 5_000, Frequency = "monthly", ExtraPayment = 20_000, ExtraRecurrence = "annual" });
        var bank = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 1_100_000, Total = 1_100_000, FutureBalance = 0, Track = "financier", NeedsNewFinancing = true });
        var unknown = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = null, Total = null, FutureBalance = 500_000, Installment = 5_000, Frequency = "monthly", Quality = "incomplete" });
        var preparing = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 1 }, status: OpportunityStatus.Preparing);
        var spare = await DiscoveryRows.InsertAsync(api, city, new Shape { DueNow = 200_000, Total = 200_000, FutureBalance = 0 });

        var buyer = await SellerAsync(api, name: "مشتري مفضلة");
        await Ok(buyer.PostAsync($"/api/market/opportunities/{dev.Reference}/save"));
        await Ok(buyer.PostAsync($"/api/market/opportunities/{dev.Reference}/save")); // twice: still one
        Assert.Equal(1, await api.WithDbAsync(db => db.SavedOpportunities.CountAsync(s => s.OpportunityId == dev.Id)));
        Assert.Equal(HttpStatusCode.NotFound, (await buyer.PostAsync($"/api/market/opportunities/{preparing.Reference}/save")).Status);

        await DiscoveryRows.SetStatusAsync(api, dev.Id, OpportunityStatus.Withdrawn);
        var (_, saved) = await Ok(buyer.GetAsync("/api/market/my/saved"));
        var row = saved!.AsArray().Single(x => x!["reference"]!.GetValue<string>() == dev.Reference)!;
        Assert.False(row["available"]!.GetValue<bool>());
        Assert.Null(row["card"]);
        Assert.DoesNotContain("300000", row.ToJsonString());

        var (_, cmp) = await api.Client().GetAsync($"/api/market/compare?refs={dev.Reference},{bank.Reference},{unknown.Reference},NOPE-1,{spare.Reference}");
        var items = cmp!["items"]!.AsArray();
        Assert.Equal(4, items.Count); // at most four
        Assert.Equal(["available", "reference"], items[0]!.AsObject().Select(kv => kv.Key).Order());
        var bankCells = items[1]!["cells"]!;
        Assert.Equal("not_applicable", bankCells["installment"]!["state"]!.GetValue<string>());
        Assert.Equal("not_applicable", bankCells["futureBalance"]!["state"]!.GetValue<string>());
        Assert.Contains("لا ينتقل", bankCells["installment"]!["text"]!.GetValue<string>());
        var unknownCells = items[2]!["cells"]!;
        Assert.Equal("unknown", unknownCells["dueNow"]!["state"]!.GetValue<string>());
        Assert.Null(unknownCells["dueNow"]!["value"]);
        Assert.False(items[3]!["available"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Saved_searches_are_normalized_deduplicated_and_private_to_their_owner()
    {
        var owner = await SellerAsync(api, name: "صاحب بحث");
        var other = await SellerAsync(api, name: "شخص آخر");
        var (_, a) = await Ok(owner.PostAsync("/api/market/my/searches", new { name = "شقق ينبع", query = "maxNow=500000&city=yanbu&page=3&types=apartment", alertsEnabled = false }));
        Assert.True(a!["created"]!.GetValue<bool>());
        var id = a["search"]!["id"]!.GetValue<string>();
        Assert.DoesNotContain("page", a["search"]!["query"]!.GetValue<string>());
        // Same criteria in another order: the first search, not a duplicate.
        var (_, b) = await Ok(owner.PostAsync("/api/market/my/searches", new { query = "types=apartment&city=yanbu&maxNow=500000.00", alertsEnabled = false }));
        Assert.False(b!["created"]!.GetValue<bool>());
        Assert.Equal(id, b["search"]!["id"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/market/my/searches", new { query = "city=jeddah", alertsEnabled = true, consent = false })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/market/my/searches", new { query = "maxNow=abc", alertsEnabled = false })).Status);

        // Another person: not listed, and every action on the id is «not found».
        var (_, theirs) = await Ok(other.GetAsync("/api/market/my/searches"));
        Assert.DoesNotContain(theirs!["items"]!.AsArray(), x => x!["id"]!.GetValue<string>() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/market/my/searches/{id}", new { name = "x" })).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/market/my/searches/{id}/pause")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/market/my/searches/{id}")).Status);
        // Staff are not individuals.
        var staff = await api.LoginAsync(Lead);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/market/my/searches")).Status);

        // Stale version → 409; turning alerts on needs consent.
        var version = a["search"]!["version"]!.GetValue<uint>();
        await Ok(owner.PutAsync($"/api/market/my/searches/{id}", new { name = "اسم جديد", version }));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsync($"/api/market/my/searches/{id}", new { name = "قديم", version })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsync($"/api/market/my/searches/{id}", new { alertsEnabled = true })).Status);
        var (_, on) = await Ok(owner.PutAsync($"/api/market/my/searches/{id}", new { alertsEnabled = true, consent = true }));
        Assert.True(on!["search"]!["alertsEnabled"]!.GetValue<bool>());

        // Deleting frees the criteria for a new search.
        await Ok(owner.DeleteAsync($"/api/market/my/searches/{id}"));
        var (_, again) = await Ok(owner.PostAsync("/api/market/my/searches", new { query = "city=yanbu&maxNow=500000&types=apartment", alertsEnabled = false }));
        Assert.True(again!["created"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Public_photos_are_revalidated_so_a_withdrawn_listing_stops_serving_them()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);
        var (_, detail) = await api.Client().GetAsync($"/api/market/opportunities/{op}");
        var url = detail!["photos"]![0]!["url"]!.GetValue<string>();
        using var http = api.CreateClient();
        using var first = await http.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("no-cache", first.Headers.CacheControl!.ToString());
        var etag = first.Headers.ETag!.Tag;
        using var conditional = new HttpRequestMessage(HttpMethod.Get, url);
        conditional.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var second = await http.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);

        var publisher = await api.LoginAsync(Verifier);
        await Ok(publisher.PostAsync($"/api/team/market/opportunities/{op}/withdraw", new { reason = "طلب المالك سحب الفرصة" }));
        using var revalidate = new HttpRequestMessage(HttpMethod.Get, url);
        revalidate.Headers.TryAddWithoutValidation("If-None-Match", etag);
        using var gone = await http.SendAsync(revalidate);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
