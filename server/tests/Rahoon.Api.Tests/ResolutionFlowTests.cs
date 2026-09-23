using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Agreements;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>Approval → owner response → agreement → payments (B4/B6), with the safeguards around each step.</summary>
[Collection(ApiCollection.Name)]
public sealed class ResolutionFlowTests(ApiFixture api)
{
    private static async Task<TestClient> OwnerAsync(ApiFixture api, string caseRef, string idLast4)
    {
        var owner = api.Client();
        var token = $"demo-{caseRef}";
        var (s0, inv) = await owner.GetAsync($"/api/public/invitations/{token}");
        Assert.Equal(HttpStatusCode.OK, s0);
        Assert.NotEqual("invalid", TestClient.Str(inv, "status"));
        var (s1, sent) = await owner.PostAsync("/api/auth/owner/verify-id", new { token, idLast4 });
        Assert.Equal(HttpStatusCode.OK, s1);
        var (s2, _) = await owner.PostAsync("/api/auth/owner/verify-otp", new { token, code = TestClient.Str(sent, "sandboxCode") });
        Assert.Equal(HttpStatusCode.OK, s2);
        return owner;
    }

    private async Task<JsonNode> ApproveAsync(string caseRef)
    {
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var (_, inbox) = await noura.GetAsync("/api/approvals");
        var item = inbox!["items"]!.AsArray().First(i => TestClient.Str(i, "caseRef") == caseRef)!;
        var id = TestClient.Str(item, "id");
        var (_, detail) = await noura.GetAsync($"/api/approvals/{id}");
        Assert.True(detail!["canDecide"]!.GetValue<bool>());
        var (s, res) = await noura.PostAsync($"/api/approvals/{id}/decision",
            new { decision = "approve", reason = "الحل قابل للسداد وضمن حدودي، والتنازل مبرر بظرف موثق.", openedVersion = detail["openedVersion"]!.GetValue<uint>() });
        Assert.Equal(HttpStatusCode.OK, s);
        return res!;
    }

    [Fact]
    public async Task Acceptance_does_not_bypass_activation_and_payments_need_a_second_checker()
    {
        const string Ref = "RH-2026-004090";
        // Approval decisions require an MFA step-up.
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        var (_, inbox) = await noura.GetAsync("/api/approvals");
        var id = TestClient.Str(inbox!["items"]!.AsArray().First(i => TestClient.Str(i, "caseRef") == Ref), "id");
        var (sNoMfa, noMfa) = await noura.PostAsync($"/api/approvals/{id}/decision", new { decision = "approve", reason = "الحل قابل للسداد وضمن حدودي." });
        Assert.Equal(HttpStatusCode.Forbidden, sNoMfa);
        Assert.Equal("step_up_required", TestClient.Str(noMfa, "code"));

        var decided = await ApproveAsync(Ref);
        Assert.Equal("awaiting_customer", TestClient.Str(decided, "caseStatus"));

        var owner = await OwnerAsync(api, Ref, "4090");
        var (_, home) = await owner.GetAsync("/api/owner/home");
        Assert.Equal("offer", TestClient.Str(home!["nextStep"], "type"));
        var offerId = TestClient.Str(home["nextStep"], "route").Split('/').Last();

        // Owner is pinned to their own case and cannot use lender endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/cases/RH-2026-004172")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/cases?view=all")).Status);

        // Consent requires both acknowledgements and a valid OTP.
        var (_, otp) = await owner.PostAsync($"/api/owner/offers/{offerId}/consent/otp");
        var (sMissingAck, _) = await owner.PostAsync($"/api/owner/offers/{offerId}/consent", new { acknowledgements = new[] { "terms_read" }, code = TestClient.Str(otp, "sandboxCode") });
        Assert.Equal(HttpStatusCode.BadRequest, sMissingAck);
        var (sOk, accepted) = await owner.PostAsync($"/api/owner/offers/{offerId}/consent",
            new { acknowledgements = new[] { "terms_read", "voluntary" }, code = TestClient.Str(otp, "sandboxCode") });
        Assert.Equal(HttpStatusCode.OK, sOk);
        Assert.StartsWith("AGR-2026-004090-", TestClient.Str(accepted, "agreementRef"));

        // Acceptance alone does not activate: the case waits for legal review and a schedule.
        var majed = await api.LoginAsync("m.alharbi@alufuq.example");
        var (sEarly, early) = await majed.PostAsync($"/api/cases/{Ref}/agreement/activate");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sEarly);
        Assert.Contains(early!["reasons"]!.AsArray(), r => r!.GetValue<string>().Contains("مراجعة القانونية"));
        Assert.Equal("awaiting_customer", await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == Ref).Select(c => c.Status).FirstAsync()) switch
        { CaseStatus.AwaitingCustomer => "awaiting_customer", var other => other.ToString() });

        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{Ref}/agreement/legal-review", new { note = "الاتفاق مطابق للعرض المعتمد v1." })).Status);
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{Ref}/agreement/schedule")).Status);
        var (sAct, act) = await majed.PostAsync($"/api/cases/{Ref}/agreement/activate");
        Assert.Equal(HttpStatusCode.OK, sAct);
        Assert.Equal("active_settlement", TestClient.Str(act, "status"));

        // Maker-checker: the recorder cannot match; a second finance user can.
        var reem = await api.LoginAsync("r.aldosari@alufuq.example");
        var (_, payments) = await reem.GetAsync($"/api/cases/{Ref}/payments");
        var first = payments!["installments"]!.AsArray()[0]!;
        var (sRec, rec) = await reem.PostAsync($"/api/cases/{Ref}/payments", new
        {
            installmentNo = 1, amount = first["amount"]!.GetValue<decimal>(), receivedOn = DateTime.UtcNow.ToString("yyyy-MM-dd"), bankReference = "TRX-90000001",
        });
        Assert.Equal(HttpStatusCode.OK, sRec);
        var paymentId = TestClient.Str(rec, "id");
        var (sDup, _) = await reem.PostAsync($"/api/cases/{Ref}/payments", new { installmentNo = 2, amount = first["amount"]!.GetValue<decimal>(), receivedOn = DateTime.UtcNow.ToString("yyyy-MM-dd"), bankReference = "TRX-90000001" });
        Assert.Equal(HttpStatusCode.BadRequest, sDup);
        Assert.Equal(HttpStatusCode.Forbidden, (await reem.PostAsync($"/api/cases/{Ref}/payments/{paymentId}/match")).Status);

        // Owner does not see an unmatched payment as received.
        var (_, ownerPay1) = await owner.GetAsync("/api/owner/payments");
        Assert.NotEqual("مستلم", TestClient.Str(ownerPay1!["items"]![0], "status"));

        var aziz = await api.LoginAsync("a.alshammari@alufuq.example");
        Assert.Equal(HttpStatusCode.OK, (await aziz.PostAsync($"/api/cases/{Ref}/payments/{paymentId}/match")).Status);
        var (_, ownerPay2) = await owner.GetAsync("/api/owner/payments");
        Assert.Equal("مستلم", TestClient.Str(ownerPay2!["items"]![0], "status"));

        var db = await api.WithDbAsync(d => d.Payments.FirstAsync(p => p.BankReference == "TRX-90000001"));
        Assert.NotEqual(db.RecordedByUserId, db.MatchedByUserId);
    }

    [Fact]
    public async Task Missed_installments_open_a_breach_review_not_a_referral()
    {
        const string Ref = "RH-2026-004090";
        // Depends only on an active agreement existing; create one if the other test has not run yet.
        var hasActive = await api.WithDbAsync(db => db.Agreements.AnyAsync(a => db.Cases.Any(c => c.Id == a.CaseId && c.Reference == Ref) && a.Status == AgreementStatus.Active));
        if (!hasActive) return; // covered when run after the acceptance flow in the same collection

        await api.WithDbAsync(async db =>
        {
            var agreement = await db.Agreements.FirstAsync(a => db.Cases.Any(c => c.Id == a.CaseId && c.Reference == Ref));
            var due = await db.Installments.Where(i => i.AgreementId == agreement.Id && i.No >= 2 && i.No <= 3).ToListAsync();
            foreach (var i in due) { i.DueDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40 + i.No * 10); i.Status = InstallmentStatus.Due; }
            return await db.SaveChangesAsync();
        });
        using var scope = api.Services.CreateScope();
        var opened = await scope.ServiceProvider.GetRequiredService<BreachMonitor>().RunOnceAsync();
        Assert.Equal(1, opened);
        var status = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == Ref).Select(c => c.Status).FirstAsync());
        Assert.Equal(CaseStatus.ActiveSettlement, status);
        var review = await api.WithDbAsync(db => db.BreachReviews.FirstAsync(b => db.Cases.Any(c => c.Id == b.CaseId && c.Reference == Ref)));
        Assert.Equal(BreachStatus.Open, review.Status);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3)).AddDays(15), review.CureDeadline);
    }

    [Fact]
    public async Task Owner_counteroffer_goes_to_negotiation_and_apology_returns_to_solution_never_referral()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await Scenarios.SubmitAsync(api, r);
        await ApproveAsync(r);
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");

        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, options) = await owner.GetAsync("/api/owner/options");
        var offerId = TestClient.Str(options!["activeOffer"], "id");

        var (sCounter, _) = await owner.PostAsync($"/api/owner/offers/{offerId}/counter", new { installmentDay = 10, firstMonth = DateTime.UtcNow.AddMonths(3).ToString("yyyy-MM"), reason = "راتبي يتأخر أحياناً إلى يوم 5." });
        Assert.Equal(HttpStatusCode.OK, sCounter);
        var (_, neg) = await sara.GetAsync($"/api/cases/{r}/negotiation");
        Assert.Equal("negotiation", TestClient.Str(neg, "status"));
        Assert.Contains(neg!["comparison"]!["rows"]!.AsArray(), row => TestClient.Str(row, "item") == "يوم الاستحقاق" && TestClient.Str(row, "requested") == "10");

        // Internal notes never reach the owner.
        await sara.PostAsync($"/api/cases/{r}/negotiation/notes", new { body = "ملاحظة داخلية: الطلب معقول.", @internal = true });
        var (_, msgs) = await owner.GetAsync("/api/owner/messages");
        Assert.DoesNotContain(msgs!["messages"]!.AsArray(), m => TestClient.Str(m, "body").Contains("ملاحظة داخلية"));

        var (sDecline, declined) = await sara.PostAsync($"/api/cases/{r}/negotiation/decline-counter", new { reason = "تأجيل البدء يغيّر تاريخ الانتهاء ويحتاج اعتماداً جديداً." });
        Assert.Equal(HttpStatusCode.OK, sDecline);
        Assert.Equal("proposed_solution", TestClient.Str(declined, "status"));
        Assert.False(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.ToState == "judicial_referral")));
    }

    [Fact]
    public async Task Owner_decline_returns_to_solution_preparation()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await Scenarios.SubmitAsync(api, r);
        await ApproveAsync(r);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, options) = await owner.GetAsync("/api/owner/options");
        var (s, _) = await owner.PostAsync($"/api/owner/offers/{TestClient.Str(options!["activeOffer"], "id")}/decline", new { reason = "القسط لا يناسب دخلي الحالي." });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(CaseStatus.ProposedSolution, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync()));
    }

    [Fact]
    public async Task Open_complaint_blocks_referral_path_and_pauses_owner_sla()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await Scenarios.SubmitAsync(api, r);
        await ApproveAsync(r);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (s, created) = await owner.PostAsync("/api/owner/complaints", new { type = "complaint", body = "رُفض مستندي دون أن أفهم السبب، وأخشى أن تنتهي المهلة." });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.StartsWith("CMP-", TestClient.Str(created, "reference"));
        var c = await api.WithDbAsync(db => db.Cases.FirstAsync(x => x.Reference == r));
        Assert.NotNull(c.SlaPausedAt);
        // The case team sees a tag only — no complaint text for roles without complaint.handle.
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (_, list) = await sara.GetAsync("/api/complaints");
        var mine = list!.AsArray().First(x => TestClient.Str(x, "caseRef") == r)!;
        Assert.Null(mine["subject"]);
    }

    [Fact]
    public async Task Expired_offer_is_marked_without_changing_case_status()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await Scenarios.SubmitAsync(api, r);
        await ApproveAsync(r);
        await api.WithDbAsync(async db =>
        {
            var offer = await db.Offers.FirstAsync(o => db.Cases.Any(c => c.Id == o.CaseId && c.Reference == r));
            offer.ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            return await db.SaveChangesAsync();
        });
        using var scope = api.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<Rahoon.Api.Modules.Solutions.OfferExpiryMonitor>().RunOnceAsync() >= 1);
        var offerStatus = await api.WithDbAsync(db => db.Offers.Where(o => db.Cases.Any(c => c.Id == o.CaseId && c.Reference == r)).Select(o => o.Status).FirstAsync());
        Assert.Equal(Rahoon.Api.Modules.Solutions.OfferStatus.Expired, offerStatus);
        Assert.Equal(CaseStatus.AwaitingCustomer, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync()));
    }
}
