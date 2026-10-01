using System.Net;
using System.Text.Json.Nodes;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>Builds exit/buy journeys through the API so market tests never depend on each other or on seeded rows.</summary>
public static class MarketScenarios
{
    private static int _seq;
    public const string Lead = "l.alharbi@team.rahoon.example";
    public const string Coordinator = "n.alyami@team.rahoon.example";
    public const string Verifier = "a.alqahtani@team.rahoon.example";

    /// <summary>A fresh Saudi mobile in a range no seed uses (0569xxxxxx).</summary>
    public static string NewPhone() => $"0569{Interlocked.Increment(ref _seq):D6}";

    public static async Task<TestClient> SellerAsync(ApiFixture api, string? phone = null, string name = "بائع اختبار")
    {
        var c = api.Client();
        await c.PhoneLoginAsync(phone ?? NewPhone(), name);
        return c;
    }

    public static object DeveloperDraft(Guid? draftId = null) => new
    {
        clientDraftId = draftId ?? Guid.NewGuid(),
        propertyType = "apartment", city = "riyadh", district = "النرجس", obligationMode = "developer",
        answers = new Dictionary<string, string?> { ["area"] = "150", ["bedrooms"] = "3", ["readiness"] = "ready", ["description"] = "شقة اختبار جاهزة." },
        obligations = new[]
        {
            new
            {
                kind = "developer", partyOtherName = "مطور اختبار",
                answers = new Dictionary<string, string?>
                {
                    ["paid_approved"] = "300000", ["remaining_balance"] = "700000", ["installment_amount"] = "12000", ["installment_frequency"] = "monthly",
                    ["extra_payments"] = "none", ["arrears_state"] = "has", ["arrears_amount"] = "20000", ["arrears_in_balance"] = "yes", ["owner_target"] = "290000",
                },
            },
        },
        location = new { lat = 24.8352, lng = 46.6556, label = "النرجس", displayWish = "approximate" },
    };

    public static object Submit(string name = "بائع اختبار") => new { contactName = name, relationship = "owner", acceptDeclarations = true, acceptProcessing = true };

    /// <summary>Creates and submits a developer sale request; returns the owner client and the reference.</summary>
    public static async Task<(TestClient Owner, string Ref)> SubmittedAsync(ApiFixture api)
    {
        var owner = await SellerAsync(api);
        var (s, body) = await owner.PostAsync("/api/market/sale-requests", DeveloperDraft());
        if (s != HttpStatusCode.OK) throw new InvalidOperationException($"create failed {s}: {TestClient.Raw(body)}");
        var reference = body!["file"]!["reference"]!.GetValue<string>();
        var (s2, sub) = await owner.PostAsync($"/api/market/sale-requests/{reference}/submit", Submit());
        if (s2 != HttpStatusCode.OK) throw new InvalidOperationException($"submit failed {s2}: {TestClient.Raw(sub)}");
        return (owner, reference);
    }

    public static async Task UploadPhotoAsync(TestClient owner, string reference)
    {
        var form = new MultipartFormDataContent();
        // 1×1 PNG.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "photo.png");
        var (s, body) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/photos", form);
        if (s != HttpStatusCode.OK) throw new InvalidOperationException($"photo upload failed {s}: {TestClient.Raw(body)}");
    }

    /// <summary>Submitted → under review → approved by the team; returns the team client.</summary>
    public static async Task<TestClient> ApprovedAsync(ApiFixture api, string reference)
    {
        var team = await api.LoginAsync(Lead);
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/start-review"));
        await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/approve", new { reason = "مستندات مكتملة" }));
        return team;
    }

    /// <summary>Approved request with an accepted photo → opportunity prepared, sent, confirmed by the owner, published. Returns OP reference.</summary>
    public static async Task<string> PublishedAsync(ApiFixture api, TestClient owner, string reference, decimal buyerCostsNow = 15000)
    {
        await UploadPhotoAsync(owner, reference);
        var team = await ApprovedAsync(api, reference);
        var (_, detail) = await team.GetAsync($"/api/team/market/sale-requests/{reference}");
        foreach (var p in detail!["file"]!["photos"]!.AsArray())
            await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/photos/{p!["id"]!.GetValue<string>()}/review", new { status = "accepted" }));
        var (_, created) = await Ok(team.PostAsync($"/api/team/market/sale-requests/{reference}/opportunity"));
        var op = created!["reference"]!.GetValue<string>();
        await Ok(team.PutAsync($"/api/team/market/opportunities/{op}/terms", new
        {
            developer = new
            {
                paidApproved = 300000, remainingBalance = 700000, arrearsState = "has", arrears = 20000, arrearsInBalance = "yes", arrearsPayer = "buyer", reduction = 10000,
                installment = 12000, installmentFrequency = "monthly", remainingInstallments = 57,
            },
            sellerCosts = 0, buyerCostsNow, buyerCostsLater = 0, needsNewFinancing = false, states = new Dictionary<string, string> { ["paid_approved"] = "verified" },
            transferConditions = "يتطلب موافقة المطور.", verificationScope = "المدفوع من كشف المطور.", verifiedOn = "2026-09-20",
        }));
        await Ok(team.PostAsync($"/api/team/market/opportunities/{op}/send-to-owner"));
        var (_, mine) = await Ok(owner.GetAsync($"/api/market/sale-requests/{reference}/opportunity"));
        await Ok(owner.PostAsync($"/api/market/sale-requests/{reference}/opportunity/confirm", new { termsId = mine!["terms"]!["id"]!.GetValue<string>() }));
        await Ok(team.PostAsync($"/api/team/market/opportunities/{op}/checklist", new { items = new[] { "relationship", "figures", "photos_location", "completion_path", "approvals" } }));
        var verifier = await api.LoginAsync(Verifier);
        await Ok(verifier.PostAsync($"/api/team/market/opportunities/{op}/publish"));
        return op;
    }

    public static async Task<(HttpStatusCode, JsonNode?)> Ok(Task<(HttpStatusCode Status, JsonNode? Body)> call)
    {
        var (s, b) = await call;
        if (s != HttpStatusCode.OK) throw new InvalidOperationException($"expected 200, got {(int)s}: {TestClient.Raw(b)}");
        return (s, b);
    }
}
