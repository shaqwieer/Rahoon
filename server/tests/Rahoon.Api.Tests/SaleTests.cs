using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Closure;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Sale;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>Builds voluntary-sale tracks through the owner and lender APIs, each on a fresh case.</summary>
public static class SaleScenarios
{
    public static readonly string[] Acks = ["proceeds_repay", "owner_decides", "privacy", "withdrawal_right"];

    public static string Raw(JsonNode? n) => n?.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) ?? "";

    public static async Task OwnerRequestAsync(TestClient owner)
    {
        var (_, otp) = await owner.PostAsync("/api/owner/sale/request/otp");
        var (s, b) = await owner.PostAsync("/api/owner/sale/request", new { message = "أرغب في بيع العقار بسعر السوق وسداد التمويل.", proposedMinPrice = 900_000, acknowledgements = Acks, code = TestClient.Str(otp, "sandboxCode") });
        Assert.True(s == HttpStatusCode.OK, Raw(b));
    }

    /// <summary>Logs in as whoever the request was routed to and completes a step-up.</summary>
    public static async Task<TestClient> AssignedApproverAsync(ApiFixture api, Guid approvalId)
    {
        var email = await api.WithDbAsync(db => db.ApprovalRequests.Where(a => a.Id == approvalId)
            .Join(db.Users, a => a.AssignedApproverUserId, u => u.Id, (a, u) => u.Email).FirstAsync());
        var approver = await api.LoginAsync(email, "مصرف الأفق");
        await approver.StepUpAsync();
        return approver;
    }

    /// <summary>Owner request → decision → approval → scoped consent. Returns the case reference and the owner client.</summary>
    public static async Task<(string Reference, TestClient Owner)> OpenAsync(ApiFixture api, decimal minPrice = 900_000)
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        await OwnerRequestAsync(owner);
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (s, d) = await sara.PostAsync($"/api/cases/{r}/sale/decision", new { reason = "بطلب المالك؛ صافي متوقع يغطي المديونية مع فائض له.", expectedStatus = "proposed_solution" });
        Assert.True(s == HttpStatusCode.OK, Raw(d));
        var approver = await AssignedApproverAsync(api, Guid.Parse(TestClient.Str(d, "approvalRequestId")));
        var (s2, a) = await approver.PostAsync($"/api/sale-approvals/{TestClient.Str(d, "approvalRequestId")}/decision", new { decision = "approve", reason = "الشروط مكتملة والفائض المتوقع لصالح المالك." });
        Assert.True(s2 == HttpStatusCode.OK, Raw(a));
        Assert.Equal("voluntary_sale", TestClient.Str(a, "caseStatus"));
        await ConsentAsync(owner, minPrice);
        return (r, owner);
    }

    public static async Task ConsentAsync(TestClient owner, decimal minPrice)
    {
        var (_, otp) = await owner.PostAsync("/api/owner/sale/consent/otp");
        var (s, b) = await owner.PostAsync("/api/owner/sale/consent", new { minPrice, visitDays = new[] { "thu", "sat" }, visitWindow = "4–7 م", acknowledged = true, code = TestClient.Str(otp, "sandboxCode") });
        Assert.True(s == HttpStatusCode.OK, Raw(b));
    }

    public static object Offer(decimal price, string method = "Cash", int validDays = 10) => new
    {
        price, paymentMethod = method, proofOfFunds = "شيك مصدق (نسخة)", conditions = (string?)null, proposedTransferDays = 21,
        validUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(validDays).ToString("yyyy-MM-dd"), certainty = "High", buyerNdaConfirmed = true,
    };
}

[Collection(ApiCollection.Name)]
public sealed class SaleTests(ApiFixture api)
{
    private const string Alufuq = "مصرف الأفق";

    [Fact]
    public async Task Owner_request_then_approved_decision_opens_sale_and_consent_unlocks_preparation()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", Alufuq);

        // No owner request: the prerequisite is shown unmet and there is no track to decide on.
        var (_, before) = await sara.GetAsync($"/api/cases/{r}/sale/decision");
        Assert.False(before!["prerequisites"]![0]!["ok"]!.GetValue<bool>());
        Assert.False(before["canSubmit"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, (await sara.PostAsync($"/api/cases/{r}/sale/decision", new { reason = "محاولة بدون طلب من المالك." })).Status);

        // Owner request with OTP (all four acknowledgements required).
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, explain) = await owner.GetAsync("/api/owner/sale/explain");
        Assert.True(explain!["canRequest"]!.GetValue<bool>());
        var (_, otp) = await owner.PostAsync("/api/owner/sale/request/otp");
        var code = TestClient.Str(otp, "sandboxCode");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/owner/sale/request", new { acknowledgements = new[] { "privacy" }, code })).Status);
        var (rs, rb) = await owner.PostAsync("/api/owner/sale/request", new { message = "أرغب في البيع بنفسي.", proposedMinPrice = 900_000, acknowledgements = SaleScenarios.Acks, code });
        Assert.True(rs == HttpStatusCode.OK, SaleScenarios.Raw(rb));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/owner/sale/request", new { acknowledgements = SaleScenarios.Acks, code })).Status);
        var consentKinds = await api.WithDbAsync(db => db.ConsentRecords.Where(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r)).Select(x => x.Kind).ToListAsync());
        Assert.Equal(["sale_consent"], consentKinds);

        // L27 estimate: 1,000,000 − 757,000 − 2% × 1,000,000 − 8,000 = 215,000.
        var (_, view) = await sara.GetAsync($"/api/cases/{r}/sale/decision");
        Assert.True(view!["canSubmit"]!.GetValue<bool>(), SaleScenarios.Raw(view));
        var lines = view["estimate"]!["lines"]!.AsArray();
        Assert.Equal(20_000m, lines.First(l => TestClient.Str(l, "key") == "commission")!["amount"]!.GetValue<decimal>());
        Assert.Equal(215_000m, lines.First(l => TestClient.Str(l, "key") == "surplus")!["amount"]!.GetValue<decimal>());

        // Only sale.manage prepares the decision; the reason is mandatory.
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PostAsync($"/api/cases/{r}/sale/decision", new { reason = "محاولة من المحلل بدون صلاحية." })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/sale/decision", new { reason = "" })).Status);
        var (s, decision) = await sara.PostAsync($"/api/cases/{r}/sale/decision", new { reason = "بطلب المالك؛ صافي متوقع يغطي المديونية مع فائض له." });
        Assert.Equal(HttpStatusCode.OK, s);
        var requestId = Guid.Parse(TestClient.Str(decision, "approvalRequestId"));
        var req = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(a => a.Id == requestId));
        Assert.Equal(ApprovalSubject.Sale, req.Subject);
        Assert.NotEqual(req.PreparedByUserId, req.AssignedApproverUserId);

        // Checker needs a fresh step-up; the case does not move before approval.
        var email = await api.WithDbAsync(db => db.Users.Where(u => u.Id == req.AssignedApproverUserId).Select(u => u.Email).FirstAsync());
        var approver = await api.LoginAsync(email, Alufuq);
        var (s1, noStepUp) = await approver.PostAsync($"/api/sale-approvals/{requestId}/decision", new { decision = "approve", reason = "الشروط مكتملة والفائض لصالح المالك." });
        Assert.Equal(HttpStatusCode.Forbidden, s1);
        Assert.Equal("step_up_required", TestClient.Str(noStepUp, "code"));
        Assert.Equal(CaseStatus.ProposedSolution, await StatusAsync(r));
        await approver.StepUpAsync();
        var (s2, approved) = await approver.PostAsync($"/api/sale-approvals/{requestId}/decision", new { decision = "approve", reason = "الشروط مكتملة والفائض لصالح المالك." });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("voluntary_sale", TestClient.Str(approved, "caseStatus"));

        // Formal scoped consent gates preparation.
        Assert.Equal(HttpStatusCode.Conflict, (await sara.PutAsync($"/api/cases/{r}/sale/prep-items/deed_check", new { status = "Done" })).Status);
        var (_, progress) = await owner.GetAsync("/api/owner/sale");
        Assert.Equal("AwaitingConsent", TestClient.Str(progress, "status"));
        Assert.True(progress!["canWithdraw"]!.GetValue<bool>());
        await SaleScenarios.ConsentAsync(owner, 900_000);
        var (_, file) = await sara.GetAsync($"/api/cases/{r}/sale/file");
        Assert.Equal("2 من 8", TestClient.Str(file!["preparation"], "counter"));
        Assert.Equal(900_000m, file["consent"]!["scope"]!["minPrice"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.OK, (await sara.PutAsync($"/api/cases/{r}/sale/prep-items/deed_check", new { status = "Done", memo = "متحقق" })).Status);
        var kinds = await api.WithDbAsync(db => db.ConsentRecords.Where(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r)).Select(x => x.Kind).ToListAsync());
        Assert.Contains("sale_scope_consent", kinds);
    }

    [Fact]
    public async Task Transition_engine_blocks_the_sale_without_documented_owner_consent()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        // A decision that reached approval without any owner request/consent record (e.g. entered by mistake).
        var requestId = await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var sara = await db.Users.FirstAsync(u => u.Email == "s.alqahtani@alufuq.example");
            var noura = await db.Users.FirstAsync(u => u.Email == "n.alshehri@alufuq.example");
            var sale = new VoluntarySale
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, BuyerReference = "VS-T-" + Random.Shared.Next(100000, 999999), Status = SaleStatus.PendingDecision,
                RequestText = "—", RequestedAt = DateTimeOffset.UtcNow,
            };
            var req = new ApprovalRequest
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.Sale, SubjectId = sale.Id, Title = "اعتماد فتح مسار البيع الطوعي",
                PreparedByUserId = sara.Id, SubmittedByUserId = sara.Id, SubmittedAt = DateTimeOffset.UtcNow, SubmitterNote = "بدون طلب من المالك",
                AssignedApproverUserId = noura.Id, DueOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3),
            };
            sale.DecisionApprovalRequestId = req.Id;
            db.Add(sale);
            db.ApprovalRequests.Add(req);
            await db.SaveChangesAsync();
            return req.Id;
        });
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var (s, body) = await noura.PostAsync($"/api/sale-approvals/{requestId}/decision", new { decision = "approve", reason = "محاولة اعتماد بدون موافقة المالك." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Equal("guard_failed", TestClient.Str(body, "code"));
        Assert.Contains(body!["reasons"]!.AsArray(), x => x!.GetValue<string>() == "لا توجد موافقة موثقة من المالك على البيع.");
        Assert.Equal(CaseStatus.ProposedSolution, await StatusAsync(r));
        var blocked = await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Blocked && e.Type == "case.transition_blocked" && e.ToState == "voluntary_sale"));
        Assert.True(blocked);
        var pending = await api.WithDbAsync(db => db.ApprovalRequests.Where(a => a.Id == requestId).Select(a => a.Status).FirstAsync());
        Assert.Equal(ApprovalStatus.Pending, pending);
    }

    [Fact]
    public async Task Owner_otp_is_locked_after_three_wrong_codes()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, otp) = await owner.PostAsync("/api/owner/sale/request/otp");
        var code = TestClient.Str(otp, "sandboxCode");
        var wrong = code == "000000" ? "111111" : "000000";
        HttpStatusCode last = default;
        JsonNode? lastBody = null;
        for (var i = 0; i < 3; i++)
            (last, lastBody) = await owner.PostAsync("/api/owner/sale/request", new { acknowledgements = SaleScenarios.Acks, code = wrong });
        Assert.Equal(HttpStatusCode.Unauthorized, last);
        Assert.Equal("otp_exhausted", TestClient.Str(lastBody, "code"));
        // The attempts were persisted: the right code no longer works and nothing was recorded.
        var (s, _) = await owner.PostAsync("/api/owner/sale/request", new { acknowledgements = SaleScenarios.Acks, code });
        Assert.NotEqual(HttpStatusCode.OK, s);
        Assert.False(await api.WithDbAsync(db => db.Set<VoluntarySale>().AnyAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r))));
        Assert.False(await api.WithDbAsync(db => db.ConsentRecords.AnyAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r))));
    }

    [Fact]
    public async Task Owner_withdrawal_returns_case_to_proposed_solution_never_referral()
    {
        var (r, owner) = await SaleScenarios.OpenAsync(api);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/owner/sale/withdraw", new { confirm = false })).Status);
        var (s, b) = await owner.PostAsync("/api/owner/sale/withdraw", new { reason = "قررت الاحتفاظ بالمنزل", confirm = true });
        Assert.True(s == HttpStatusCode.OK, SaleScenarios.Raw(b));
        Assert.Equal("proposed_solution", TestClient.Str(b, "caseStatus"));
        Assert.Equal(CaseStatus.ProposedSolution, await StatusAsync(r));
        var (sale, consent, audit) = await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var sale = await db.Set<VoluntarySale>().FirstAsync(x => x.CaseId == c.Id);
            var consent = await db.Set<SaleConsent>().FirstAsync(x => x.SaleId == sale.Id);
            var audit = await db.AuditEvents.Where(e => e.CaseId == c.Id && (e.Type == "sale.withdrawn" || e.Type == "case.transition")).OrderBy(e => e.Seq).Select(e => new { e.Type, e.ToState }).ToListAsync();
            return (sale, consent, audit.Select(a => (a.Type, a.ToState)).ToList());
        });
        Assert.Equal(SaleStatus.Withdrawn, sale.Status);
        Assert.Equal(SaleConsentStatus.Withdrawn, consent.Status);
        Assert.Contains(("sale.withdrawn", "proposed_solution"), audit);
        Assert.DoesNotContain(audit, a => a.ToState == "judicial_referral");
        // Nothing left to withdraw; the owner may start a new request later.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/owner/sale/withdraw", new { confirm = true })).Status);
    }

    [Fact]
    public async Task Controlled_listing_broker_scope_offer_maker_checker_and_completion()
    {
        var (r, owner) = await SaleScenarios.OpenAsync(api, minPrice: 900_000);
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", Alufuq);

        // L30: the summary is reviewed by compliance (not by whoever prepared it); the public column is always «لا».
        Assert.Equal(HttpStatusCode.OK, (await sara.PutAsync($"/api/cases/{r}/sale/listing", new { askingPrice = 1_000_000, areaLabel = "شمال الرياض", approvedPhotoCount = 6 })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PostAsync($"/api/cases/{r}/sale/listing/compliance-review", new { note = "x" })).Status);
        var hind = await api.LoginAsync("h.almutairi@alufuq.example");
        Assert.Equal(HttpStatusCode.OK, (await hind.PostAsync($"/api/cases/{r}/sale/listing/compliance-review", new { note = "لا يكشف هوية المالك." })).Status);
        var (_, listing) = await sara.GetAsync($"/api/cases/{r}/sale/listing");
        var matrix = listing!["matrix"]!.AsArray();
        Assert.Equal(8, matrix.Count);
        Assert.All(matrix, row => Assert.Equal("لا", TestClient.Str(row, "public")));
        var buyerJson = SaleScenarios.Raw(listing["buyerPreview"]);
        AssertNoSecrets(buyerJson, r);
        Assert.Contains("شمال الرياض", buyerJson);

        // L31: broker from the institution directory; licence state shown; access scoped to the sale file.
        var (_, candidates) = await sara.GetAsync($"/api/cases/{r}/sale/broker-candidates");
        var items = candidates!["items"]!.AsArray();
        var brokerA = items.First(i => TestClient.Str(i, "name") == "دار الوسطاء «أ»")!;
        Assert.True(brokerA["selectable"]!.GetValue<bool>());
        Assert.Contains(items, i => TestClient.Str(i, "licenseState") == "expiring_soon" && i!["warning"] is not null);
        var (sa, assigned) = await sara.PostAsync($"/api/cases/{r}/sale/broker-assignment", new { providerOrganizationId = TestClient.Str(brokerA, "id") });
        Assert.True(sa == HttpStatusCode.OK, SaleScenarios.Raw(assigned));
        var vs = await api.WithDbAsync(db => db.Set<VoluntarySale>().Where(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r)).Select(x => x.BuyerReference).FirstAsync());

        var sami = await api.LoginAsync("s.alharbi@broker-a.example");
        var (_, mine) = await sami.GetAsync("/api/broker/sales");
        Assert.Contains(mine!["items"]!.AsArray(), i => TestClient.Str(i, "buyerReference") == vs);
        var (sd, detail) = await sami.GetAsync($"/api/broker/sales/{vs}");
        Assert.Equal(HttpStatusCode.OK, sd);
        AssertNoSecrets(SaleScenarios.Raw(detail), r);
        Assert.Equal(HttpStatusCode.Forbidden, (await sami.GetAsync($"/api/cases/{r}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await sami.GetAsync($"/api/cases/{r}/sale")).Status);
        var waleed = await api.LoginAsync("w.alqahtani@broker-d.example"); // a broker, but not assigned to this sale
        Assert.Equal(HttpStatusCode.NotFound, (await waleed.GetAsync($"/api/broker/sales/{vs}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await waleed.PostAsync($"/api/broker/sales/{vs}/offers", SaleScenarios.Offer(990_000))).Status);

        // L32: offers from broker and lender; net = price × 0.98; owner approves before the bank.
        var (so, of1) = await sami.PostAsync($"/api/broker/sales/{vs}/offers", SaleScenarios.Offer(950_000));
        Assert.True(so == HttpStatusCode.OK, SaleScenarios.Raw(of1));
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/sale/offers", SaleScenarios.Offer(980_000, "FinancingPreapproved"))).Status);
        var (_, cmp) = await sara.GetAsync($"/api/cases/{r}/sale/offers");
        var o1 = cmp!["offers"]!.AsArray().First(o => TestClient.Str(o, "code") == "OF-01")!;
        Assert.Equal(931_000m, o1["netAfterCommission"]!.GetValue<decimal>());
        Assert.Equal(757_000m, o1["lenderRecovery"]!.GetValue<decimal>());
        Assert.Equal(174_000m, o1["ownerSurplus"]!.GetValue<decimal>());
        Assert.False(o1["canRequestApproval"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await sara.PostAsync($"/api/cases/{r}/sale/offers/OF-01/approval-request", new { recommendation = "نقدي وبلا شروط وأعلى يقيناً.", attested = true })).Status);

        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/sale/offers/share-with-owner")).Status);
        var (_, ownerOffers) = await owner.GetAsync("/api/owner/sale/offers");
        var ownerJson = SaleScenarios.Raw(ownerOffers);
        Assert.DoesNotContain("سامي", ownerJson);
        Assert.DoesNotContain("دار الوسطاء", ownerJson);
        Assert.Equal(2, ownerOffers!["items"]!.AsArray().Count);
        var (_, aotp) = await owner.PostAsync("/api/owner/sale/offers/OF-01/accept/otp");
        var (sacc, acc) = await owner.PostAsync("/api/owner/sale/offers/OF-01/accept", new { acknowledged = true, code = TestClient.Str(aotp, "sandboxCode") });
        Assert.True(sacc == HttpStatusCode.OK, SaleScenarios.Raw(acc));
        // Withdrawal closes once an offer is accepted.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync("/api/owner/sale/withdraw", new { confirm = true })).Status);

        // Maker (sara) submits; checker needs sale.approve, ≠ maker, and step-up.
        var (sr, sub) = await sara.PostAsync($"/api/cases/{r}/sale/offers/OF-01/approval-request", new { recommendation = "نقدي وبلا شروط وأعلى يقيناً.", attested = true });
        Assert.True(sr == HttpStatusCode.OK, SaleScenarios.Raw(sub));
        var approvalId = Guid.Parse(TestClient.Str(sub, "approvalRequestId"));
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.PostAsync($"/api/sale-approvals/{approvalId}/decision", new { decision = "approve", reason = "محاولة اعتماد من المُعِدّة نفسها." })).Status);
        var email = await api.WithDbAsync(db => db.ApprovalRequests.Where(a => a.Id == approvalId).Join(db.Users, a => a.AssignedApproverUserId, u => u.Id, (a, u) => u.Email).FirstAsync());
        var checker = await api.LoginAsync(email, Alufuq);
        Assert.Equal(HttpStatusCode.Forbidden, (await checker.PostAsync($"/api/sale-approvals/{approvalId}/decision", new { decision = "approve", reason = "اعتماد بدون رمز تحقق." })).Status);
        await checker.StepUpAsync();
        var (sd2, dec) = await checker.PostAsync($"/api/sale-approvals/{approvalId}/decision", new { decision = "approve", reason = "العرض نقدي ويغطي المديونية مع فائض للمالك." });
        Assert.True(sd2 == HttpStatusCode.OK, SaleScenarios.Raw(dec));
        var approval = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(a => a.Id == approvalId));
        Assert.NotEqual(approval.SubmittedByUserId, approval.DecidedByUserId);
        Assert.True(approval.StepUpVerified);

        // Broker access is revoked on approval.
        Assert.Equal(HttpStatusCode.NotFound, (await sami.GetAsync($"/api/broker/sales/{vs}")).Status);

        // L33: completion requires price received + mortgage released, both with references.
        var (sc0, early) = await sara.PostAsync($"/api/cases/{r}/sale/complete", new { reason = "محاولة قبل استلام الثمن" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sc0);
        Assert.Equal(CaseStatus.VoluntarySale, await StatusAsync(r));
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PutAsync($"/api/cases/{r}/sale/tracking/payment_received", new { status = "Done", actualDate = today })).Status);
        Assert.Equal(HttpStatusCode.OK, (await sara.PutAsync($"/api/cases/{r}/sale/tracking/payment_received", new { status = "Done", actualDate = today, reference = "TRX-55012233" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await sara.PutAsync($"/api/cases/{r}/sale/tracking/lien_release", new { status = "Done", actualDate = today, reference = "REL-2026-7781" })).Status);
        var (sc, done) = await sara.PostAsync($"/api/cases/{r}/sale/complete", new { reason = "استلام الثمن وفك الرهن", expectedStatus = "voluntary_sale" });
        Assert.True(sc == HttpStatusCode.OK, SaleScenarios.Raw(done));
        Assert.Equal("awaiting_reconciliation", TestClient.Str(done, "caseStatus"));
        Assert.Equal(19_000m, done!["figures"]!["brokerCommission"]!.GetValue<decimal>());
        Assert.Equal(174_000m, done["figures"]!["ownerSurplus"]!.GetValue<decimal>());
        var recon = await api.WithDbAsync(db => db.Reconciliations.Include(x => x.Lines).FirstAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r)));
        Assert.Equal("voluntary_sale", recon.Basis);
        Assert.Equal(0m, recon.Difference);
        Assert.Contains(recon.Lines, l => l.Kind == "receipt" && l.Amount == 950_000m && l.Reference == "TRX-55012233");
    }

    private static void AssertNoSecrets(string json, string caseRef)
    {
        foreach (var secret in new[] { "خالد سعد", "خالد س.", caseRef, "RH-2026", "مصرف الأفق", "900000", "757000" })
            Assert.DoesNotContain(secret, json);
    }

    private Task<CaseStatus> StatusAsync(string r) => api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync());
}
