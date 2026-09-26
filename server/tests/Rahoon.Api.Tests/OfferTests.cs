using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Requests;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A step 7 — the lender's offer recorded from its letter, verified by a second member (never the recorder, MFA
/// step-up, DB check), published to the individual, and the individual's documented response (accept with an SMS
/// consent record; decline, question or counter without any action against them), relayed and then continued or closed.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OfferTests(ApiFixture api)
{
    internal static async Task<string> UploadLetterAsync(TestClient team, string reference)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\n% lender letter\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "letter.pdf");
        content.Add(new StringContent("lender_letter"), "kind");
        content.Add(new StringContent("false"), "visibleToApplicant");
        var (s, body) = await team.PostMultipartAsync($"/api/team/requests/{reference}/documents", content);
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(body));
        return TestClient.Str(body, "documentId");
    }

    internal static object P1Offer(string letterId, string lenderRef = "AF-2026-7781") => new
    {
        path = "p1", newInstallment = 3100m, termMonths = 240, startText = "من القسط التالي بعد التوقيع لدى الجهة",
        conditions = "سداد القسط الجديد بانتظام", effectText = "ينخفض قسطك الشهري من 4,200 إلى 3,100 ريال، وتطول مدة التمويل.",
        lenderReference = lenderRef, lenderLetterDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), lenderValidityText = "صالح 30 يوماً من تاريخ الخطاب",
        letterDocumentId = letterId, shareLetter = true,
    };

    internal static async Task<string> RecordAsync(TestClient nayef, string reference)
    {
        var letter = await UploadLetterAsync(nayef, reference);
        var (s, body) = await nayef.PostAsync($"/api/team/requests/{reference}/offers", P1Offer(letter));
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(body));
        return TestClient.Str(body, "id");
    }

    internal static readonly string[] Checklist = ["amounts_match_letter", "terms_match_letter", "reference_and_date_match", "effect_text_accurate"];

    internal static async Task PublishAsync(ApiFixture api, string reference, string offerId)
    {
        var abeer = await api.LoginAsync(TeamRequestTests.Abeer);
        await abeer.StepUpAsync();
        var (s, body) = await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(body));
    }

    /// <summary>A request with a published offer: (individual, coordinator, reference).</summary>
    internal static async Task<(TestClient Individual, TestClient Coordinator, string Reference)> OfferAvailableAsync(ApiFixture api)
    {
        var (individual, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var offerId = await RecordAsync(nayef, reference);
        await PublishAsync(api, reference, offerId);
        return (individual, nayef, reference);
    }

    private static object Relay() => new
    {
        channel = "email", occurredAt = DateTimeOffset.UtcNow.AddMinutes(-5), counterpart = "إدارة التحصيل", summary = "أرسلنا رد العميل بالبريد الرسمي.",
        applicantText = (string?)null,
    };

    [Fact]
    public async Task Offer_is_recorded_from_the_letter_verified_by_another_member_with_step_up_and_then_published()
    {
        var (individual, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var (sNoLetter, noLetter) = await nayef.PostAsync($"/api/team/requests/{reference}/offers", P1Offer(Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, sNoLetter);
        Assert.NotNull(noLetter!["errors"]!["letterDocumentId"]);

        var offerId = await RecordAsync(nayef, reference);
        var (_, hidden) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Null(hidden!["offer"]);                                          // nothing reaches the individual before verification

        // The recorder cannot verify their own offer (refusal audited), even as the team lead would need another member.
        var (sSelf, _) = await nayef.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);

        var abeer = await api.LoginAsync(TeamRequestTests.Abeer);
        var (_, queue) = await abeer.GetAsync("/api/team/verify");
        Assert.Contains(reference, TestClient.Raw(queue));
        Assert.Equal(HttpStatusCode.OK, (await abeer.GetAsync($"/api/team/requests/{reference}")).Status); // verifier may open it now
        var (sNoStep, noStep) = await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, sNoStep);
        Assert.Equal("step_up_required", TestClient.Str(noStep, "code"));
        await abeer.StepUpAsync();
        var (sHalf, _) = await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = new[] { "amounts_match_letter" } });
        Assert.Equal(HttpStatusCode.BadRequest, sHalf);
        Assert.Equal(HttpStatusCode.OK, (await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = Checklist })).Status);

        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("offer_available", TestClient.Str(detail, "status"));
        Assert.Equal("applicant", TestClient.Str(detail, "waitingOn"));
        var offer = detail!["offer"]!;
        Assert.Equal("p1", TestClient.Str(offer, "path"));
        Assert.Equal(3100m, offer["newInstallment"]!.GetValue<decimal>());
        Assert.Equal("صالح 30 يوماً من تاريخ الخطاب", TestClient.Str(offer, "lenderValidityText"));
        Assert.False(string.IsNullOrEmpty(TestClient.Str(offer, "letterVersionId")));   // the letter is shared (D-5 proposal)
        var json = TestClient.Raw(detail);
        Assert.DoesNotContain("نايف", json);
        Assert.DoesNotContain("عبير", json);
        Assert.DoesNotContain("amounts_match_letter", json);

        // The letter itself is downloadable by the individual.
        var (sFile, _, type) = await individual.GetBytesAsync($"/api/my/requests/{reference}/documents/{TestClient.Str(offer, "letterVersionId")}/file");
        Assert.Equal(HttpStatusCode.OK, sFile);
        Assert.Equal("application/pdf", type);
    }

    [Fact]
    public async Task The_team_lead_holds_both_permissions_but_still_cannot_verify_an_offer_they_recorded()
    {
        var (_, _, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var lead = await api.LoginAsync(TeamRequestTests.Lead);
        var offerId = await RecordAsync(lead, reference);
        await lead.StepUpAsync();
        var (s, body) = await lead.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, s);
        Assert.Contains("سجّلته بنفسك", TestClient.Raw(body));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.offer_verify_blocked")));
    }

    [Fact]
    public async Task The_database_refuses_a_verifier_who_is_the_recorder()
    {
        var (_, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var offerId = Guid.Parse(await RecordAsync(nayef, reference));
        await Assert.ThrowsAsync<DbUpdateException>(() => api.WithDbAsync(async db =>
        {
            var o = await db.RequestOffers.SingleAsync(x => x.Id == offerId);
            o.VerifiedByUserId = o.RecordedByUserId;
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task A_returned_offer_never_reaches_the_individual()
    {
        var (individual, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var offerId = await RecordAsync(nayef, reference);
        var abeer = await api.LoginAsync(TeamRequestTests.Abeer);
        var (sNoReason, _) = await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify", new { decision = "return" });
        Assert.Equal(HttpStatusCode.BadRequest, sNoReason);
        Assert.Equal(HttpStatusCode.OK, (await abeer.PostAsync($"/api/team/requests/{reference}/offers/{offerId}/verify",
            new { decision = "return", reason = "مدة التمويل في الخطاب 228 شهراً لا 240." })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Null(detail!["offer"]);
        Assert.Equal("lender_coordination", TestClient.Str(detail, "status"));
        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        Assert.Contains(team!["offers"]!.AsArray(), o => TestClient.Str(o, "status") == "returned");
    }

    [Fact]
    public async Task Accepting_needs_an_sms_code_is_idempotent_and_is_relayed_before_closing()
    {
        var (individual, nayef, reference) = await OfferAvailableAsync(api);
        var (sBad, _) = await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", new { kind = "accept", code = "000000" });
        Assert.True(sBad is HttpStatusCode.Unauthorized or HttpStatusCode.Gone, sBad.ToString());

        var (_, otp) = await individual.PostAsync($"/api/my/requests/{reference}/offer/accept/otp");
        var key = Guid.NewGuid().ToString();
        var body = new { kind = "accept", code = TestClient.Str(otp, "sandboxCode") };
        var (s1, r1) = await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", body, key);
        var (s2, r2) = await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", body, key);
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(TestClient.Raw(r1), TestClient.Raw(r2));
        Assert.Equal($"{reference}-R1", TestClient.Str(r1, "reference"));

        var (_, recorded) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("response_recorded", TestClient.Str(recorded, "status"));
        Assert.Equal("team", TestClient.Str(recorded, "waitingOn"));
        Assert.Contains("ليست توقيعاً ملزماً", TestClient.Raw(recorded!["responses"]));

        // Closing before the response is relayed is refused; relay, then close with the outcome shown to the individual.
        var (sEarly, early) = await nayef.PostAsync($"/api/team/requests/{reference}/close", new { outcomeCode = "offer_accepted", summary = "قبلت العرض." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sEarly);
        Assert.Contains("نقل رد العميل", TestClient.Raw(early));
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/relay", Relay())).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/close",
            new { outcomeCode = "offer_accepted", summary = "قبلت عرض جهتك ونقلنا موافقتك إليها. التنفيذ يتم مع جهتك الممولة." })).Status);
        var (_, closed) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("closed", TestClient.Str(closed, "status"));
        Assert.Contains("التنفيذ يتم مع جهتك", TestClient.Str(closed!["outcome"], "summary"));
        Assert.Contains("نقلنا ردك", TestClient.Raw(closed["timeline"]));
    }

    [Fact]
    public async Task Decline_needs_no_reason_and_coordination_continues_to_a_new_offer()
    {
        var (individual, nayef, reference) = await OfferAvailableAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", new { kind = "decline" })).Status);
        var (sCont, _) = await nayef.PostAsync($"/api/team/requests/{reference}/continue", new { nextStep = (string?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sCont);        // relay first
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/relay", Relay())).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/continue", new { nextStep = (string?)null })).Status);

        var letter = await UploadLetterAsync(nayef, reference);
        var (s, v2) = await nayef.PostAsync($"/api/team/requests/{reference}/offers", P1Offer(letter, "AF-2026-7790"));
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(2, v2!["versionNo"]!.GetValue<int>());
        await PublishAsync(api, reference, TestClient.Str(v2, "id"));
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("AF-2026-7790", TestClient.Str(detail!["offer"], "lenderReference"));
        Assert.True(detail["canRespond"]!.GetValue<bool>());
        await api.WithDbAsync(async db =>
        {
            var req = await db.Requests.SingleAsync(x => x.Reference == reference);
            var statuses = await db.RequestOffers.Where(o => o.RequestId == req.Id).OrderBy(o => o.VersionNo).Select(o => o.Status).ToListAsync();
            Assert.Equal([RequestOfferStatus.Superseded, RequestOfferStatus.Published], statuses);
            return 0;
        });
    }

    [Fact]
    public async Task Question_or_counter_needs_text_and_another_individual_cannot_respond()
    {
        var (individual, _, reference) = await OfferAvailableAsync(api);
        var (sNo, noText) = await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", new { kind = "counter" });
        Assert.Equal(HttpStatusCode.BadRequest, sNo);
        Assert.NotNull(noText!["errors"]!["text"]);
        var intruder = await RequestTests.IndividualAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/my/requests/{reference}/offer/respond", new { kind = "decline" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/offer/respond",
            new { kind = "counter", text = "أستطيع قسطاً قدره 2,800 ريال." })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("counter", TestClient.Str(detail!["responses"]![0], "kind"));
    }

    [Fact]
    public async Task Lender_with_no_offer_is_closed_with_the_lender_answer_as_relayed()
    {
        var (individual, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/close", new
        {
            outcomeCode = "lender_no_offer", summary = "ردّت جهتك الممولة كما نقلها فريق رهون: «لا يوجد لدينا تمويل عقاري مطابق لهذه البيانات». لم يُتخذ أي إجراء بخصوص تمويلك بسبب هذا الطلب.",
        })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("closed", TestClient.Str(detail, "status"));
        Assert.Equal("lender_no_offer", TestClient.Str(detail!["outcome"], "code"));
    }
}
