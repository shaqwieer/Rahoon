using System.Net;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Requests;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A-2 step 1 (ADR 0002 §6) — execution tracking of an accepted P1/P2 offer: the team records the lender's
/// agreement, schedule, confirmations, notices and closure documents from the lender's document, a second member verifies
/// each record, and the individual sees only published records and their own payment reports. Nothing is confirmed without
/// the lender's evidence, and no action follows a lender notice.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ExecutionTests(ApiFixture api)
{
    private static readonly string[] Checklist = ["values_match_source", "reference_and_date_match", "kind_is_correct", "explanation_accurate"];
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    /// <summary>A published P1 offer accepted with the SMS code and relayed; still <c>response_recorded</c>.</summary>
    private static async Task<(TestClient Individual, TestClient Coordinator, string Reference)> AcceptedAsync(ApiFixture api, string path = "p1")
    {
        var (individual, nayef, reference) = await TeamRequestTests.CoordinatingAsync(api);
        var letter = await OfferTests.UploadLetterAsync(nayef, reference);
        object body = path == "p1" ? OfferTests.P1Offer(letter) : new
        {
            path = "p3", saleTerms = "تنسيق بيع رضائي بسعر لا يقل عن تقييم معتمد", effectText = "يُباع العقار بالتنسيق مع جهتك، وتُسدَّد المديونية من الحصيلة.",
            lenderReference = "AF-2026-7799", lenderLetterDate = Today.AddDays(-1), letterDocumentId = letter, shareLetter = true,
        };
        var (s, offer) = await nayef.PostAsync($"/api/team/requests/{reference}/offers", body);
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(offer));
        await OfferTests.PublishAsync(api, reference, TestClient.Str(offer, "id"));
        var (_, otp) = await individual.PostAsync($"/api/my/requests/{reference}/offer/accept/otp");
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/offer/respond",
            new { kind = "accept", code = TestClient.Str(otp, "sandboxCode") })).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/relay", OfferTests.Relay())).Status);
        return (individual, nayef, reference);
    }

    internal static async Task<(TestClient Individual, TestClient Coordinator, string Reference)> TrackingAsync(ApiFixture api)
    {
        var (individual, nayef, reference) = await AcceptedAsync(api);
        var (s, b) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/start", new { nextStep = (string?)null });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(b));
        return (individual, nayef, reference);
    }

    private static object Agreement(string sourceId) => new
    {
        kind = "agreement", sourceDocumentId = sourceId, lenderReference = "AF-2026-8001", lenderDate = Today.AddDays(-2),
        summaryText = "فعّلت الجهة ملحق إعادة الجدولة: قسط 3,100 ريال لمدة 240 شهراً.",
        explanationText = "أصبح قسطك 3,100 ريال، وتدفعه لجهتك مباشرة بحسب جدولها.", activationDate = Today.AddDays(-2),
        schedule = new[]
        {
            new { no = 1, dueDate = Today.AddDays(-1), amount = 3100m },
            new { no = 2, dueDate = Today.AddMonths(1), amount = 3100m },
            new { no = 3, dueDate = Today.AddMonths(2), amount = 3100m },
        },
    };

    private static async Task<string> RecordAsync(TestClient coordinator, string reference, object body)
    {
        var (s, b) = await coordinator.PostAsync($"/api/team/requests/{reference}/execution/records", body);
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(b));
        return TestClient.Str(b, "id");
    }

    private static async Task PublishAsync(ApiFixture api, string reference, string recordId)
    {
        var abeer = await api.LoginAsync(TeamRequestTests.Abeer);
        await abeer.StepUpAsync();
        var (s, b) = await abeer.PostAsync($"/api/team/requests/{reference}/execution/records/{recordId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(b));
    }

    private static async Task<string> RecordAndPublishAsync(ApiFixture api, TestClient coordinator, string reference, Func<string, object> body)
    {
        var letter = await OfferTests.UploadLetterAsync(coordinator, reference);
        var id = await RecordAsync(coordinator, reference, body(letter));
        await PublishAsync(api, reference, id);
        return id;
    }

    private static async Task<string> UploadProofAsync(TestClient individual, string reference)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\n% transfer receipt\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "transfer.pdf");
        content.Add(new StringContent("payment_proof"), "kind");
        var (s, body) = await individual.PostMultipartAsync($"/api/my/requests/{reference}/documents", content);
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(body));
        return TestClient.Str(body, "documentId");
    }

    [Fact]
    public async Task Tracking_starts_only_from_an_accepted_relayed_P1_or_P2_offer_and_new_outcomes_need_tracking()
    {
        var (individual, nayef, reference) = await AcceptedAsync(api);
        // The tracking outcomes aren't available before tracking starts.
        var (sEarly, _) = await nayef.PostAsync($"/api/team/requests/{reference}/close", new { outcomeCode = "executed_closed", summary = "اكتمل." });
        Assert.Equal(HttpStatusCode.BadRequest, sEarly);
        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        Assert.Contains(team!["actions"]!.AsArray(), a => TestClient.Str(a, "key") == "start_execution_tracking" && a!["enabled"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/execution/start", new { nextStep = (string?)null })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("execution_tracking", TestClient.Str(detail, "status"));
        Assert.Equal("قيد متابعة التنفيذ", TestClient.Str(detail, "statusLabel"));
        Assert.Equal("lender", TestClient.Str(detail, "waitingOn"));
        Assert.True(detail!["canReportPayment"]!.GetValue<bool>());
        Assert.Equal(ExecutionEndpoints.NoFundsNote, TestClient.Str(detail["execution"], "noFundsNote"));
        Assert.Equal(ExecutionEndpoints.WithdrawEffect, TestClient.Str(detail, "withdrawEffect"));

        // From tracking, only the tracking outcomes close the request.
        var (sOld, _) = await nayef.PostAsync($"/api/team/requests/{reference}/close", new { outcomeCode = "offer_accepted", summary = "قبلت العرض." });
        Assert.Equal(HttpStatusCode.BadRequest, sOld);
    }

    [Fact]
    public async Task A_declined_offer_or_a_P3_sale_is_not_tracked()
    {
        var (individual, nayef, reference) = await OfferTests.OfferAvailableAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/offer/respond", new { kind = "decline" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/relay", OfferTests.Relay())).Status);
        var (sDecl, decl) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/start", new { nextStep = (string?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sDecl);
        Assert.Contains("ليس قبولاً", TestClient.Raw(decl));

        var (_, sale, saleRef) = await AcceptedAsync(api, "p3");
        var (sP3, p3) = await sale.PostAsync($"/api/team/requests/{saleRef}/execution/start", new { nextStep = (string?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sP3);
        Assert.Contains("P3", TestClient.Raw(p3));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == saleRef && e.Type == "request.transition_blocked")));
        // A P3 acceptance still closes as before (sale execution is Phase 2).
        Assert.Equal(HttpStatusCode.OK, (await sale.PostAsync($"/api/team/requests/{saleRef}/close",
            new { outcomeCode = "offer_accepted", summary = "قبلت عرض البيع الرضائي، وتنسيق البيع في مرحلة لاحقة." })).Status);
    }

    [Fact]
    public async Task The_offer_accepted_refusal_follows_the_latest_response_not_the_state()
    {
        var (_, nayef, reference) = await TrackingAsync(api);
        // Back to coordination (Q17) with a reason; the latest response is still an accepted P1 offer.
        var (sNoReason, _) = await nayef.PostAsync($"/api/team/requests/{reference}/continue", new { nextStep = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, sNoReason);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/continue",
            new { reason = "أبلغتنا جهتك بتعديل على الاتفاق، ونتابع معها على طلبك نفسه." })).Status);
        var (s, body) = await nayef.PostAsync($"/api/team/requests/{reference}/close", new { outcomeCode = "offer_accepted", summary = "قبلت العرض." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Contains("متابعة التنفيذ", TestClient.Raw(body));
    }

    [Fact]
    public async Task Records_need_the_lenders_document_from_this_request_and_another_member_verifies_with_step_up()
    {
        var (individual, nayef, reference) = await TrackingAsync(api);
        var (sNone, none) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/records", Agreement(Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, sNone);
        Assert.NotNull(none!["errors"]!["sourceDocumentId"]);
        // A lender letter from another request is refused.
        var (_, other, otherRef) = await TeamRequestTests.CoordinatingAsync(api);
        var foreign = await OfferTests.UploadLetterAsync(other, otherRef);
        var (sForeign, _) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/records", Agreement(foreign));
        Assert.Equal(HttpStatusCode.BadRequest, sForeign);

        var letter = await OfferTests.UploadLetterAsync(nayef, reference);
        var recordId = await RecordAsync(nayef, reference, Agreement(letter));
        var (_, before) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.True(before!["execution"]!["agreement"] is null);                 // nothing before verification
        Assert.DoesNotContain("pending_verification", TestClient.Raw(before));

        var (sSelf, _) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/records/{recordId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);                              // a coordinator can't verify at all

        var abeer = await api.LoginAsync(TeamRequestTests.Abeer);
        var (_, queue) = await abeer.GetAsync("/api/team/verify/execution");
        Assert.Contains(reference, TestClient.Raw(queue));
        Assert.Equal(HttpStatusCode.OK, (await abeer.GetAsync($"/api/team/requests/{reference}")).Status);   // verifier may open it now
        var (sNoStep, noStep) = await abeer.PostAsync($"/api/team/requests/{reference}/execution/records/{recordId}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, sNoStep);
        Assert.Equal("step_up_required", TestClient.Str(noStep, "code"));
        await abeer.StepUpAsync();
        var (sHalf, _) = await abeer.PostAsync($"/api/team/requests/{reference}/execution/records/{recordId}/verify", new { decision = "publish", checklist = new[] { "values_match_source" } });
        Assert.Equal(HttpStatusCode.BadRequest, sHalf);
        Assert.Equal(HttpStatusCode.OK, (await abeer.PostAsync($"/api/team/requests/{reference}/execution/records/{recordId}/verify",
            new { decision = "publish", checklist = Checklist })).Status);

        var (_, after) = await individual.GetAsync($"/api/my/requests/{reference}");
        var execution = after!["execution"]!;
        Assert.Equal("AF-2026-8001", TestClient.Str(execution["agreement"], "lenderReference"));
        Assert.Equal(3100m, execution["agreement"]!["newInstallment"]!.GetValue<decimal>());    // prefilled from the accepted offer
        Assert.Equal(3, execution["schedule"]!.AsArray().Count);
        Assert.All(execution["schedule"]!.AsArray(), row => Assert.Null(row!["state"]));       // no overdue state, even for a past due date
        Assert.False(string.IsNullOrEmpty(TestClient.Str(execution["agreement"], "sourceVersionId")));
        var json = TestClient.Raw(after);
        Assert.DoesNotContain("نايف", json);
        Assert.DoesNotContain("عبير", json);
        Assert.DoesNotContain("values_match_source", json);
        Assert.DoesNotContain("overdue", json);
    }

    [Fact]
    public async Task The_team_lead_holds_both_permissions_but_still_cannot_verify_a_record_they_made()
    {
        var (_, _, reference) = await TrackingAsync(api);
        var lead = await api.LoginAsync(TeamRequestTests.Lead);
        var letter = await OfferTests.UploadLetterAsync(lead, reference);
        var id = await RecordAsync(lead, reference, Agreement(letter));
        await lead.StepUpAsync();
        var (s, body) = await lead.PostAsync($"/api/team/requests/{reference}/execution/records/{id}/verify", new { decision = "publish", checklist = Checklist });
        Assert.Equal(HttpStatusCode.Forbidden, s);
        Assert.Contains("سجّلته بنفسك", TestClient.Raw(body));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.execution_verify_blocked")));
    }

    [Fact]
    public async Task The_database_refuses_an_execution_verifier_who_is_the_recorder()
    {
        var (_, nayef, reference) = await TrackingAsync(api);
        var letter = await OfferTests.UploadLetterAsync(nayef, reference);
        var id = Guid.Parse(await RecordAsync(nayef, reference, Agreement(letter)));
        await Assert.ThrowsAsync<DbUpdateException>(() => api.WithDbAsync(async db =>
        {
            var x = await db.RequestExecutionRecords.SingleAsync(r => r.Id == id);
            x.VerifiedByUserId = x.RecordedByUserId;
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task A_payment_report_is_never_confirmed_without_a_published_lender_confirmation()
    {
        var (individual, nayef, reference) = await TrackingAsync(api);
        await RecordAndPublishAsync(api, nayef, reference, Agreement);
        var proof = await UploadProofAsync(individual, reference);

        var (sNoProof, _) = await individual.PostAsync($"/api/my/requests/{reference}/payment-reports", new { amount = 3100m, transferDate = Today, proofDocumentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, sNoProof);
        var (sBadNo, _) = await individual.PostAsync($"/api/my/requests/{reference}/payment-reports", new { amount = 3100m, transferDate = Today, scheduleItemNo = 9, proofDocumentId = proof });
        Assert.Equal(HttpStatusCode.BadRequest, sBadNo);
        var intruder = await RequestTests.IndividualAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostAsync($"/api/my/requests/{reference}/payment-reports",
            new { amount = 3100m, transferDate = Today, proofDocumentId = proof })).Status);

        var key = Guid.NewGuid().ToString();
        var body = new { amount = 3100m, transferDate = Today, bankReference = "TRF-1", scheduleItemNo = 1, proofDocumentId = proof };
        var (s1, r1) = await individual.PostAsync($"/api/my/requests/{reference}/payment-reports", body, key);
        var (s2, r2) = await individual.PostAsync($"/api/my/requests/{reference}/payment-reports", body, key);
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(TestClient.Raw(r1), TestClient.Raw(r2));
        Assert.Equal($"{reference}-P1", TestClient.Str(r1, "reference"));

        var (_, reported) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("reported", TestClient.Str(reported!["execution"]!["paymentReports"]![0], "status"));
        Assert.Equal("reported", TestClient.Str(reported["execution"]!["schedule"]![0], "state"));

        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        var reportId = TestClient.Str(team!["execution"]!["paymentReports"]![0], "id");
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/payment-reports/{reportId}/note",
            new { text = "لم تؤكده جهتك بعد؛ طلبنا منها التأكيد." })).Status);

        // The lender's confirmation, recorded but not yet verified, changes nothing for the individual.
        var letter = await OfferTests.UploadLetterAsync(nayef, reference);
        var confirmationId = await RecordAsync(nayef, reference, new
        {
            kind = "payment_confirmation", sourceDocumentId = letter, lenderReference = "AF-2026-8010", lenderDate = Today, summaryText = "تؤكد الجهة استلام القسط الأول.",
            explanationText = "أكدت جهتك استلام قسطك الأول.", amount = 3100m, receivedOn = Today, scheduleItemNo = 1, answersReportId = reportId,
        });
        var (_, pending) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("not_confirmed_yet", TestClient.Str(pending!["execution"]!["paymentReports"]![0], "status"));
        Assert.Empty(pending["execution"]!["confirmations"]!.AsArray());

        await PublishAsync(api, reference, confirmationId);
        var (_, confirmed) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("confirmed_by_lender", TestClient.Str(confirmed!["execution"]!["paymentReports"]![0], "status"));
        Assert.Equal("confirmed_by_lender", TestClient.Str(confirmed["execution"]!["schedule"]![0], "state"));
        Assert.Single(confirmed["execution"]!["confirmations"]!.AsArray());

        // A confirmed report can't be answered again, and an answered report can't be linked twice.
        Assert.Equal(HttpStatusCode.Conflict, (await nayef.PostAsync($"/api/team/requests/{reference}/payment-reports/{reportId}/note", new { text = "…" })).Status);
        var again = await OfferTests.UploadLetterAsync(nayef, reference);
        var (sTwice, _) = await nayef.PostAsync($"/api/team/requests/{reference}/execution/records", new
        {
            kind = "payment_confirmation", sourceDocumentId = again, lenderReference = "AF-2026-8011", lenderDate = Today, summaryText = "تكرار.",
            explanationText = "تكرار.", amount = 3100m, receivedOn = Today, answersReportId = reportId,
        });
        Assert.Equal(HttpStatusCode.BadRequest, sTwice);
    }

    [Fact]
    public async Task Completion_needs_a_verified_closure_document_relevant_to_the_path()
    {
        var (individual, nayef, reference) = await TrackingAsync(api);
        var (s0, b0) = await nayef.PostAsync($"/api/team/requests/{reference}/close", new { outcomeCode = "executed_closed", summary = "اكتمل التنفيذ." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s0);
        Assert.Contains("مستند إغلاق", TestClient.Raw(b0));

        object Closure(string letter, string kind) => new
        {
            kind = "closure_document", sourceDocumentId = letter, lenderReference = $"AF-2026-9{kind.Length:D3}", lenderDate = Today, documentKind = kind,
            summaryText = "مستند من الجهة.", explanationText = "هذا مستند من جهتك يخص اتفاقك.",
        };
        // A clearance letter isn't the relevant document for a P1 rescheduling (Q18 interim mapping, V13).
        await RecordAndPublishAsync(api, nayef, reference, l => Closure(l, "clearance"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await nayef.PostAsync($"/api/team/requests/{reference}/close",
            new { outcomeCode = "executed_closed", summary = "اكتمل التنفيذ." })).Status);
        // Recorded but not verified doesn't count either.
        var pendingLetter = await OfferTests.UploadLetterAsync(nayef, reference);
        await RecordAsync(nayef, reference, Closure(pendingLetter, "rescheduling_confirmation"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await nayef.PostAsync($"/api/team/requests/{reference}/close",
            new { outcomeCode = "executed_closed", summary = "اكتمل التنفيذ." })).Status);

        await RecordAndPublishAsync(api, nayef, reference, l => Closure(l, "rescheduling_confirmation"));
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/close",
            new { outcomeCode = "executed_closed", summary = "اكتملت إعادة الجدولة بحسب ما أكدته جهتك، والمستندات محفوظة في صفحة طلبك." })).Status);

        var (_, closed) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("closed", TestClient.Str(closed, "status"));
        Assert.Equal("executed_closed", TestClient.Str(closed!["outcome"], "code"));
        var docs = closed["execution"]!["closureDocuments"]!.AsArray();
        Assert.Equal(2, docs.Count);
        var (sFile, _, type) = await individual.GetBytesAsync($"/api/my/requests/{reference}/documents/{TestClient.Str(docs[0], "sourceVersionId")}/file");
        Assert.Equal(HttpStatusCode.OK, sFile);
        Assert.Equal("application/pdf", type);
    }

    [Fact]
    public async Task A_lender_notice_is_explained_with_no_action_and_the_team_sets_who_is_awaited()
    {
        var (individual, nayef, reference) = await TrackingAsync(api);
        await RecordAndPublishAsync(api, nayef, reference, l => new
        {
            kind = "lender_notice", sourceDocumentId = l, lenderReference = "AF-2026-8100", lenderDate = Today, noticeCategory = "missed_installment",
            summaryText = "تفيد الجهة بأن القسط الثاني لم يصلها.", explanationText = "تقول جهتك إن قسطك الثاني لم يصلها. إن كنت سددته فأبلغنا مع الإثبات.",
        });
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        var notice = detail!["execution"]!["notices"]![0]!;
        Assert.Equal("missed_installment", TestClient.Str(notice, "category"));
        Assert.Equal(ExecutionEndpoints.NoActionNote, TestClient.Str(notice, "noActionNote"));
        Assert.Equal("execution_tracking", TestClient.Str(detail, "status"));   // nothing changes automatically

        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/updates",
            new { text = "نحتاج إثبات تحويل القسط الثاني إن كنت سددته.", waitingOn = "applicant" })).Status);
        var (_, waiting) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("applicant", TestClient.Str(waiting, "waitingOn"));

        var (_, lnayef, other) = await TeamRequestTests.CoordinatingAsync(api);
        var (sOutside, _) = await lnayef.PostAsync($"/api/team/requests/{other}/updates", new { text = "تحديث.", waitingOn = "applicant" });
        Assert.Equal(HttpStatusCode.BadRequest, sOutside);                   // «ننتظر» is set this way only during tracking
    }

    [Fact]
    public async Task Consent_withdrawal_pauses_tracking_the_team_can_still_publish_and_a_new_consent_resumes_it()
    {
        var (individual, nayef, reference) = await TrackingAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/consent/withdraw")).Status);
        var (_, paused) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("info_requested", TestClient.Str(paused, "status"));
        Assert.False(paused!["canReportPayment"]!.GetValue<bool>());

        await RecordAndPublishAsync(api, nayef, reference, Agreement);   // what the lender sends on its own can still be recorded
        var (_, otp) = await individual.PostAsync($"/api/my/requests/{reference}/consent/otp");
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/consent",
            new { code = TestClient.Str(otp, "sandboxCode"), accept = true })).Status);
        var (_, resumed) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("execution_tracking", TestClient.Str(resumed, "status"));
        Assert.Equal("lender", TestClient.Str(resumed, "waitingOn"));
        Assert.Equal("AF-2026-8001", TestClient.Str(resumed!["execution"]!["agreement"], "lenderReference"));
    }

    [Fact]
    public async Task Withdrawing_during_tracking_explains_the_agreement_is_unaffected_and_others_get_nothing()
    {
        var (individual, _, reference) = await TrackingAsync(api);
        foreach (var email in new[] { "s.alqahtani@alufuq.example", "a.almutairi@rahoon.example" })
        {
            var outsider = await api.LoginAsync(email);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/team/verify/execution")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync($"/api/team/requests/{reference}/execution/records", new { kind = "agreement" })).Status);
        }
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/withdraw", new { reason = (string?)null })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("withdrawn", TestClient.Str(detail, "status"));
        Assert.Contains(ExecutionEndpoints.WithdrawEffect, TestClient.Raw(detail!["timeline"]));
    }
}
