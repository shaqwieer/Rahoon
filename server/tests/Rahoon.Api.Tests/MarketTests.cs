using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Market;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// The exit/buy platform end to end through the API (docs/phases/phase-m-exit-marketplace.md, acceptance criteria 2–10):
/// mobile sign-in without duplicates, the first request and its completion, team review, preparation → owner confirmation →
/// publication, privacy of documents and location, search with unknown values, and interests.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MarketTests(ApiFixture api)
{
    // ── Accounts (#3) ──

    private string Hash(string phone) => api.Services.GetRequiredService<PiiProtector>().LookupHash(phone);

    [Fact]
    public async Task Same_mobile_signs_in_to_the_same_account_and_never_creates_a_second_one()
    {
        var phone = NewPhone();
        var first = await api.Client().PhoneLoginAsync(phone, "مستخدم مكرر");
        Assert.True(first!["firstTime"]!.GetValue<bool>());
        var second = await api.Client().PhoneLoginAsync(phone);
        Assert.False(second!["firstTime"]!.GetValue<bool>());
        var hash = Hash(phone);
        Assert.Equal(1, await api.WithDbAsync(db => db.IndividualProfiles.CountAsync(p => p.PhoneHash == hash)));
    }

    [Fact]
    public async Task Account_says_honestly_whether_a_code_confirmed_the_mobile()
    {
        var c = await SellerAsync(api);
        var (_, account) = await c.GetAsync("/api/account");
        Assert.Equal("sms_code", TestClient.Str(account, "phoneVerification"));

        await using var noSms = api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SmsConfirmation"] = "false" })));
        var other = new TestClient(noSms.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false }));
        var verify = await other.PhoneLoginAsync(NewPhone(), "بدون رسائل");
        Assert.False(verify!["smsVerified"]!.GetValue<bool>());
        var (_, acc2) = await other.GetAsync("/api/account");
        Assert.Equal("not_verified_sms_disabled", TestClient.Str(acc2, "phoneVerification"));
    }

    // ── First request and completion (#2, #3) ──

    [Fact]
    public async Task First_request_is_sent_without_photos_or_documents_and_a_retry_never_duplicates_it()
    {
        var owner = await SellerAsync(api);
        var draft = Guid.NewGuid();
        var (s1, a) = await owner.PostAsync("/api/market/sale-requests", DeveloperDraft(draft));
        var (s2, b) = await owner.PostAsync("/api/market/sale-requests", DeveloperDraft(draft));
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.OK, s2);
        var reference = a!["file"]!["reference"]!.GetValue<string>();
        Assert.Equal(reference, b!["file"]!["reference"]!.GetValue<string>());

        var (s3, sub) = await owner.PostAsync($"/api/market/sale-requests/{reference}/submit", Submit());
        Assert.Equal(HttpStatusCode.OK, s3);
        Assert.Equal("submitted", TestClient.Str(sub, "status"));
        var (s4, again) = await owner.PostAsync($"/api/market/sale-requests/{reference}/submit", Submit());
        Assert.Equal(HttpStatusCode.OK, s4);
        Assert.True(again!["alreadySubmitted"]!.GetValue<bool>());

        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        var media = file!["completeness"]!.AsArray().First(g => g!["key"]!.GetValue<string>() == "media")!;
        Assert.False(media["done"]!.GetValue<bool>());
        var count = await api.WithDbAsync(db => db.SaleRequests.CountAsync(r => r.ClientDraftId == draft));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task More_than_one_obligation_party_is_no_longer_accepted()
    {
        // «أكثر من جهة» was removed (2026-10-02): the catalog doesn't offer it and the API refuses it; one party per request.
        var (_, catalog) = await api.Client().GetAsync("/api/market/catalog");
        Assert.DoesNotContain(catalog!["obligationModes"]!.AsArray(), m => m!["value"]!.GetValue<string>() == "multiple");
        var owner = await SellerAsync(api);
        var (_, created) = await owner.PostAsync("/api/market/sale-requests", new
        {
            clientDraftId = Guid.NewGuid(), propertyType = "apartment", city = "riyadh", district = "النرجس", obligationMode = "multiple",
            obligations = new[] { new { kind = "developer", partyOtherName = "مطور" }, new { kind = "financier", partyOtherName = "بنك" } },
        });
        Assert.NotNull(created!["invalid"]!["obligationMode"]);
        var file = created["file"]!;
        Assert.NotEqual("multiple", file["obligationMode"]?.GetValue<string>());
        Assert.True(file["obligations"]!.AsArray().Count <= 1);
    }

    [Fact]
    public async Task Land_request_with_room_fields_is_stripped_and_still_submits()
    {
        var owner = await SellerAsync(api);
        var (_, created) = await owner.PostAsync("/api/market/sale-requests", new
        {
            clientDraftId = Guid.NewGuid(), propertyType = "land", city = "riyadh", district = "العارض", obligationMode = "financier",
            answers = new Dictionary<string, string?> { ["area"] = "600", ["bedrooms"] = "4", ["bathrooms"] = "2", ["land_use"] = "residential" },
            obligations = new[] { new { kind = "financier", partyOtherName = "جهة اختبار", answers = new Dictionary<string, string?> { ["arrears_state"] = "none", ["payoff_amount"] = "__unknown", ["asking_price"] = "900000", ["paid_approved"] = "1" } } },
        });
        var file = created!["file"]!;
        Assert.Null(file["answers"]!["bedrooms"]);
        Assert.Null(file["answers"]!["bathrooms"]);
        Assert.Null(file["obligations"]![0]!["answers"]!["paid_approved"]);
        var (s, _) = await owner.PostAsync($"/api/market/sale-requests/{TestClient.Str(file, "reference")}/submit", Submit());
        Assert.Equal(HttpStatusCode.OK, s);
    }

    [Fact]
    public async Task Submit_requires_an_answer_or_unknown_for_the_key_figures()
    {
        var owner = await SellerAsync(api);
        var (_, created) = await owner.PostAsync("/api/market/sale-requests", new
        {
            clientDraftId = Guid.NewGuid(), propertyType = "apartment", city = "jeddah", district = "الصفا", obligationMode = "developer",
            obligations = new[] { new { kind = "developer", partyOtherName = "مطور", answers = new Dictionary<string, string?> { ["paid_approved"] = "100000" } } },
        });
        var (s, body) = await owner.PostAsync($"/api/market/sale-requests/{TestClient.Str(created!["file"], "reference")}/submit", Submit());
        Assert.Equal(HttpStatusCode.BadRequest, s);
        var errors = body!["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("o0.remaining_balance"));
        Assert.True(errors.ContainsKey("o0.arrears_state"));
        Assert.False(errors.ContainsKey("o0.paid_approved"));
    }

    // ── Review (#4) ──

    [Fact]
    public async Task Team_asks_for_completion_the_owner_resubmits_and_the_log_keeps_every_decision()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await AssignAsync(api, $"sale-requests/{reference}", Coordinator);
        var team = await api.LoginAsync(Coordinator);
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/start-review"));
        var (s0, bad) = await team.PostAsync($"/api/team/market/sale-requests/{reference}/request-completion", new { items = Array.Empty<string>(), note = "" });
        Assert.Equal(HttpStatusCode.BadRequest, s0);
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/request-completion", new { items = new[] { "photos", "doc:payment_proof" }, note = "نحتاج صور العقار وكشف المطور." }));

        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        Assert.Equal("needsCompletion", TestClient.Str(file, "status"));
        Assert.Equal(2, file!["openCompletion"]!["items"]!.AsArray().Count);

        await Ok(owner.PostAsync($"/api/market/sale-requests/{reference}/resubmit", new { note = "أضفت الصور." }));
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/approve", new { reason = "" }));
        var (_, detail) = await team.GetAsync($"/api/team/market/sale-requests/{reference}");
        var kinds = detail!["events"]!.AsArray().Select(e => e!["kind"]!.GetValue<string>()).ToList();
        Assert.Contains("completion_requested", kinds);
        Assert.Contains("resubmitted", kinds);
        Assert.Contains("approved", kinds);
        Assert.NotNull(detail["completionRequests"]![0]!["answeredAt"]);
    }

    [Fact]
    public async Task Rejection_needs_a_reason_and_only_the_team_can_decide()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var (sOwner, _) = await owner.PostAsync($"/api/team/market/sale-requests/{reference}/reject", new { reason = "سبب كافٍ للرفض" });
        Assert.Equal(HttpStatusCode.Forbidden, sOwner);
        await AssignAsync(api, $"sale-requests/{reference}", Coordinator);
        var team = await api.LoginAsync(Coordinator);
        var (s1, _) = await team.PostAsync($"/api/team/market/sale-requests/{reference}/reject", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, s1);
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/reject", new { reason = "العقار خارج نطاق الخدمة حاليًا." }));
        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        Assert.Equal("rejected", TestClient.Str(file, "status"));
        Assert.Contains(file!["events"]!.AsArray(), e => e!["reason"]?.GetValue<string>() == "العقار خارج نطاق الخدمة حاليًا.");
    }

    [Fact]
    public async Task Another_person_cannot_read_or_change_a_sale_request()
    {
        var (_, reference) = await SubmittedAsync(api);
        var stranger = await SellerAsync(api);
        var (s1, _) = await stranger.GetAsync($"/api/market/sale-requests/{reference}");
        var (s2, _) = await stranger.PutAsync($"/api/market/sale-requests/{reference}", new { district = "x" });
        Assert.Equal(HttpStatusCode.NotFound, s1);
        Assert.Equal(HttpStatusCode.NotFound, s2);
        var (s3, _) = await api.Client().GetAsync($"/api/market/sale-requests/{reference}");
        Assert.Equal(HttpStatusCode.Unauthorized, s3);
    }

    [Fact]
    public async Task Owner_cannot_change_answers_once_approved()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await ApprovedAsync(api, reference);
        var (s, body) = await owner.PutAsync($"/api/market/sale-requests/{reference}", new { district = "الملقا" });
        Assert.Equal(HttpStatusCode.Conflict, s);
        Assert.Equal("locked", TestClient.Str(body, "code"));
    }

    [Fact]
    public async Task Owner_edit_of_a_verified_figure_drops_the_verification()
    {
        var (owner, reference) = await SubmittedAsync(api);
        // Verifying a figure is market.verify: the operations manager holds it for all work.
        var team = await api.LoginAsync(OpsManager);
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/start-review"));
        var (_, detail) = await team.GetAsync($"/api/team/market/sale-requests/{reference}");
        var obligationId = detail!["file"]!["obligations"]![0]!["id"]!.GetValue<string>();
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/verify-figure", new
        {
            fieldKey = $"o:{obligationId}:paid_approved", value = "300000", source = "developer_statement", sourceDate = "2026-09-01",
        }));
        await Ok(owner.PutAsync($"/api/market/sale-requests/{reference}", new
        {
            obligations = new[] { new { id = obligationId, kind = "developer", partyOtherName = "مطور اختبار", answers = new Dictionary<string, string?> { ["paid_approved"] = "310000", ["remaining_balance"] = "700000", ["installment_amount"] = "12000", ["extra_payments"] = "none", ["arrears_state"] = "none" } } },
        }));
        var active = await api.WithDbAsync(db => db.FigureVerifications.CountAsync(v => v.FieldKey == $"o:{obligationId}:paid_approved" && v.SupersededAt == null));
        Assert.Equal(0, active);
    }

    // ── Opportunity: prepare ≠ confirm ≠ publish (#5, #9) ──

    [Fact]
    public async Task Approval_does_not_publish_and_publication_needs_the_owner_confirmation()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await UploadPhotoAsync(owner, reference);
        var team = await ApprovedAsync(api, reference);
        var (_, created) = await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/opportunity"));
        var op = TestClient.Str(created, "reference");

        var (s0, _) = await api.Client().GetAsync($"/api/market/opportunities/{op}");
        Assert.Equal(HttpStatusCode.NotFound, s0);
        var verifier = await api.LoginAsync(Verifier);
        var (s1, blocked) = await verifier.PostAsync($"/api/team/market/opportunities/{op}/publish");
        Assert.Equal(HttpStatusCode.Conflict, s1);
        Assert.Contains(blocked!["reasons"]!.AsArray(), r => r!.GetValue<string>().Contains("المالك"));

        // The owner has no publish route at all, and can't confirm before the team sends the summary.
        var (s2, _) = await owner.PostAsync($"/api/team/market/opportunities/{op}/publish");
        Assert.Equal(HttpStatusCode.Forbidden, s2);
    }

    [Fact]
    public async Task Full_journey_publishes_with_approximate_location_and_no_private_data()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);

        var (s, detail) = await api.Client().GetAsync($"/api/market/opportunities/{op}");
        Assert.Equal(HttpStatusCode.OK, s);
        var raw = TestClient.Raw(detail);
        // Exact point 24.8352, 46.6556 → public grid centre; the Google Maps link uses the public point only.
        Assert.DoesNotContain("24.8352", raw);
        Assert.DoesNotContain("46.6556", raw);
        var loc = detail!["location"]!;
        Assert.Equal("approximate", TestClient.Str(loc, "precision"));
        Assert.Contains($"{loc["lat"]!.GetValue<double>().ToString(System.Globalization.CultureInfo.InvariantCulture)},", TestClient.Str(loc, "googleMapsUrl"));
        Assert.DoesNotContain("بائع اختبار", raw);
        Assert.DoesNotContain("/documents/", raw);
        Assert.Equal(325000m, detail["terms"]!["dueNow"]!.GetValue<decimal>());
        Assert.Equal(1005000m, detail["terms"]!["buyerTotal"]!.GetValue<decimal>());
        Assert.False(detail["terms"]!["commission"]!["policyApproved"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Changing_published_figures_makes_a_new_version_that_the_public_doesnt_see_until_republished()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);
        var team = await api.LoginAsync(Lead);
        await Ok(team.PutAsync($"/api/team/market/opportunities/{op}/terms", new
        {
            developer = new { paidApproved = 300000, remainingBalance = 700000, arrearsState = "none", reduction = 0, installment = 12000, installmentFrequency = "monthly" },
            sellerCosts = 0, buyerCostsNow = 15000, buyerCostsLater = 0, needsNewFinancing = false,
        }));
        var (_, pub) = await api.Client().GetAsync($"/api/market/opportunities/{op}");
        Assert.Equal(325000m, pub!["terms"]!["dueNow"]!.GetValue<decimal>());
        Assert.Equal(1, pub["terms"]!["versionNo"]!.GetValue<int>());
        var (_, teamView) = await team.GetAsync($"/api/team/market/opportunities/{op}");
        Assert.Equal(2, teamView!["draftTerms"]!["versionNo"]!.GetValue<int>());
        Assert.False(teamView["actions"]!["publish"]!.GetValue<bool>());
    }

    // ── Privacy (#6) ──

    [Fact]
    public async Task Private_documents_are_never_served_to_visitors_buyers_or_through_the_photo_route()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var (_, doc) = await owner.UploadAsync($"/api/market/sale-requests/{reference}/documents", "contract.pdf", new Dictionary<string, string> { ["kind"] = "developer_contract" });
        var url = TestClient.Str(doc, "url");
        var id = TestClient.Str(doc, "id");
        var (sOwner, _, _) = await owner.GetBytesAsync(url);
        Assert.Equal(HttpStatusCode.OK, sOwner);
        var (sAnon, _, _) = await api.Client().GetBytesAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, sAnon);
        var buyer = await SellerAsync(api);
        var (sBuyer, _, _) = await buyer.GetBytesAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, sBuyer);
        var (sPublic, _, _) = await api.Client().GetBytesAsync($"/api/market/photos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, sPublic);
        // Unassigned: case managers (assigned scope) don't see it; once assigned, its case manager does.
        Assert.Equal(HttpStatusCode.NotFound, (await (await api.LoginAsync(Coordinator)).GetBytesAsync(url)).Status);
        await AssignAsync(api, $"sale-requests/{reference}", Coordinator);
        var team = await api.LoginAsync(Coordinator);
        var (sTeam, _, _) = await team.GetBytesAsync(url);
        Assert.Equal(HttpStatusCode.OK, sTeam);
    }

    [Fact]
    public async Task Listing_photos_are_public_only_inside_a_published_opportunity()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await UploadPhotoAsync(owner, reference);
        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        var photoId = file!["photos"]![0]!["id"]!.GetValue<string>();
        var (before, _, _) = await api.Client().GetBytesAsync($"/api/market/photos/{photoId}");
        Assert.Equal(HttpStatusCode.NotFound, before);
    }

    // ── Search (#7) ──

    [Fact]
    public async Task Budget_search_never_matches_an_unknown_amount_as_zero()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);
        // Seeded OP-2026-00005 (Jeddah townhouse) has unknown buyer costs → due-now unknown.
        var (_, all) = await api.Client().GetAsync("/api/market/opportunities?pageSize=24");
        Assert.Contains(all!["items"]!.AsArray(), i => i!["card"]!["dueNow"] is null);

        var (_, budget) = await api.Client().GetAsync("/api/market/opportunities?maxNow=400000&pageSize=24");
        var items = budget!["items"]!.AsArray();
        Assert.All(items, i => Assert.True(i!["card"]!["dueNow"]!.GetValue<decimal>() <= 400000));
        Assert.Contains(items, i => i!["card"]!["reference"]!.GetValue<string>() == op);
        Assert.True(budget["excludedIncomplete"]!.GetValue<int>() >= 1);

        var (_, tooLow) = await api.Client().GetAsync("/api/market/opportunities?maxNow=300000&pageSize=24");
        Assert.DoesNotContain(tooLow!["items"]!.AsArray(), i => i!["card"]!["reference"]!.GetValue<string>() == op);
    }

    [Fact]
    public async Task Installment_filter_uses_the_monthly_equivalent_and_counts_the_annual_payment()
    {
        // Seeded Jeddah townhouse: 21,000 quarterly = 7,000 monthly, plus 50,000 every year (Phase 2: a year is 134,000).
        // 7,000 a month is comfortable for the installments alone, but not with the annual payment (84,000 a year).
        var (_, low) = await api.Client().GetAsync("/api/market/opportunities?city=jeddah&maxInstallment=7000&freq=monthly");
        Assert.DoesNotContain(low!["items"]!.AsArray(), i => i!["card"]!["installmentFrequency"]?.GetValue<string>() == "quarterly");
        // 11,200 a month (134,400 a year) covers both; the city filter still applies.
        var (_, r) = await api.Client().GetAsync("/api/market/opportunities?city=jeddah&maxInstallment=11200&freq=monthly");
        Assert.All(r!["items"]!.AsArray(), i => Assert.Equal("jeddah", i!["card"]!["city"]!.GetValue<string>()));
        Assert.Contains(r["items"]!.AsArray(), i => i!["card"]!["installmentFrequency"]?.GetValue<string>() == "quarterly");
        var (_, none) = await api.Client().GetAsync("/api/market/opportunities?city=jeddah&maxInstallment=6000&freq=monthly");
        Assert.DoesNotContain(none!["items"]!.AsArray(), i => i!["card"]!["installmentFrequency"]?.GetValue<string>() == "quarterly");
    }

    // ── Interest (#10) ──

    [Fact]
    public async Task Interest_is_linked_saved_once_visible_to_the_team_and_reserves_nothing()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var op = await PublishedAsync(api, owner, reference);
        var buyer = await SellerAsync(api, name: "مشتري اختبار");
        var (_, br) = await Ok(buyer.PostAsync("/api/market/buyer-requests", new
        {
            clientDraftId = Guid.NewGuid(), availableNow = 400000, installmentComfort = 13000, installmentFrequency = "monthly", purchaseMode = "cash",
            cities = new[] { "riyadh" }, propertyTypes = new[] { "apartment" },
        }));
        await Ok(buyer.PostAsync($"/api/market/buyer-requests/{TestClient.Str(br, "reference")}/submit", new { contactName = "مشتري اختبار", acceptDeclarations = true }));

        var (_, i1) = await Ok(buyer.PostAsync($"/api/market/opportunities/{op}/interest", new { message = "مهتم", contactPreference = "call" }));
        var (_, i2) = await Ok(buyer.PostAsync($"/api/market/opportunities/{op}/interest", new { message = "مرة ثانية" }));
        Assert.True(i1!["created"]!.GetValue<bool>());
        Assert.False(i2!["created"]!.GetValue<bool>());
        Assert.Equal(TestClient.Str(i1, "reference"), TestClient.Str(i2, "reference"));

        var stored = await api.WithDbAsync(db => db.Interests.FirstAsync(i => i.Reference == TestClient.Str(i1, "reference")));
        Assert.NotNull(stored.BuyerRequestId);
        var opp = await api.WithDbAsync(db => db.Opportunities.FirstAsync(o => o.Reference == op));
        Assert.Equal(opp.PublishedTermsId, stored.TermsId);
        Assert.Equal(OpportunityStatus.Published, opp.Status);

        var team = await api.LoginAsync(OpsManager);
        var (_, list) = await team.GetAsync("/api/team/market/interests?pageSize=100");
        Assert.Contains(list!["items"]!.AsArray(), x => x!["reference"]!.GetValue<string>() == TestClient.Str(i1, "reference"));
        // Unassigned interest on an opportunity nobody gave the case manager: outside their scope.
        var (_, cmList) = await (await api.LoginAsync(Coordinator2)).GetAsync("/api/team/market/interests?pageSize=100");
        Assert.DoesNotContain(cmList!["items"]!.AsArray(), x => x!["reference"]!.GetValue<string>() == TestClient.Str(i1, "reference"));
        var (_, mine) = await owner.PostAsync($"/api/market/opportunities/{op}/interest", new { message = "x" });
        Assert.Equal("own_opportunity", TestClient.Str(mine, "code"));
    }

    // ── Buyer request (#4) ──

    [Fact]
    public async Task Buyer_capacity_change_after_approval_goes_back_to_review_but_preferences_dont()
    {
        var buyer = await SellerAsync(api, name: "مشتري معدل");
        var draft = Guid.NewGuid();
        var body = new
        {
            clientDraftId = draft, availableNow = 300000m, installmentComfort = 9000m, installmentFrequency = "monthly", purchaseMode = "cash",
            cities = new[] { "riyadh" }, propertyTypes = new[] { "apartment" },
        };
        var (_, br) = await Ok(buyer.PostAsync("/api/market/buyer-requests", body));
        var reference = TestClient.Str(br, "reference");
        await Ok(buyer.PostAsync($"/api/market/buyer-requests/{reference}/submit", new { contactName = "مشتري معدل", acceptDeclarations = true }));
        await AssignAsync(api, $"buyer-requests/{reference}", Coordinator);
        var team = await api.LoginAsync(Coordinator);
        await Ok(team.PostAsync($"/api/team/market/buyer-requests/{reference}/start-review"));
        await Ok(team.PostAsync($"/api/team/market/buyer-requests/{reference}/approve", new { reason = "" }));

        await Ok(buyer.PutAsync($"/api/market/buyer-requests/{reference}", body with { }));
        var (_, mine1) = await buyer.GetAsync("/api/market/buyer-requests/mine");
        Assert.Equal("approvedForMatching", TestClient.Str(mine1!["request"], "status"));

        await Ok(buyer.PutAsync($"/api/market/buyer-requests/{reference}", new
        {
            clientDraftId = draft, availableNow = 500000m, installmentComfort = 9000m, installmentFrequency = "monthly", purchaseMode = "cash",
            cities = new[] { "riyadh" }, propertyTypes = new[] { "apartment" },
        }));
        var (_, mine2) = await buyer.GetAsync("/api/market/buyer-requests/mine");
        Assert.Equal("submitted", TestClient.Str(mine2!["request"], "status"));
    }
}
