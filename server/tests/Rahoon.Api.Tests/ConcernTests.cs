using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A step 8 — P4 «معالجة العقبات»: objections and complaints on a request answered by the Rahoon team (a complaint
/// never by the request's own coordinator), reopening after an upheld objection to «غير مناسب للخدمة», manual specialist
/// referral (V9), and no response-time promise anywhere (Q6/Q12).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConcernTests(ApiFixture api)
{
    [Fact]
    public async Task Objection_to_an_amount_is_referenced_answered_by_the_team_and_shown_to_the_individual()
    {
        var (individual, _, reference) = await TeamRequestTests.InReviewAsync(api);
        var (s, raised) = await individual.PostAsync($"/api/my/requests/{reference}/concerns",
            new { kind = "objection", subject = "amount", text = "القسط الذي ذكرته 4,200 لكن الصحيح 4,500 ريال." });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(raised));
        var concernRef = TestClient.Str(raised, "reference");
        Assert.Matches(@"^OBJ-\d{4}-\d{5}$", concernRef);
        Assert.DoesNotContain("أيام", TestClient.Str(raised, "message"));   // no response-time promise (Q6)
        Assert.DoesNotContain("خلال", TestClient.Str(raised, "message"));

        var lead = await api.LoginAsync(TeamRequestTests.Lead);
        var (_, queue) = await lead.GetAsync("/api/team/concerns");
        var item = queue!["items"]!.AsArray().First(i => TestClient.Str(i, "reference") == concernRef)!;
        Assert.Equal(reference, TestClient.Str(item, "requestReference"));
        Assert.Equal(HttpStatusCode.OK, (await lead.PostAsync($"/api/team/concerns/{TestClient.Str(item, "id")}/answer",
            new { outcome = "upheld", response = "صححنا القسط في ملف طلبك إلى 4,500 ريال، وسنعتمده في التواصل مع جهتك." })).Status);

        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        var concern = detail!["concerns"]!.AsArray().First(c => TestClient.Str(c, "reference") == concernRef)!;
        Assert.Equal("answered", TestClient.Str(concern, "status"));
        Assert.Equal("upheld", TestClient.Str(concern, "outcome"));
        Assert.Contains("4,500", TestClient.Str(concern, "responseText"));
        Assert.DoesNotContain("لمى", TestClient.Raw(detail));             // who answered stays internal
    }

    [Fact]
    public async Task A_complaint_is_not_answered_by_the_requests_own_coordinator()
    {
        var (individual, nayef, reference) = await TeamRequestTests.InReviewAsync(api);
        var (_, raised) = await individual.PostAsync($"/api/my/requests/{reference}/concerns", new { kind = "complaint", subject = "service", text = "لم يصلني أي تحديث منذ مدة." });
        Assert.StartsWith("CMP-", TestClient.Str(raised, "reference"));
        var (_, queue) = await nayef.GetAsync("/api/team/concerns");
        var item = queue!["items"]!.AsArray().First(i => TestClient.Str(i, "reference") == TestClient.Str(raised, "reference"))!;
        Assert.False(item["mayAnswer"]!.GetValue<bool>());
        var (s, _) = await nayef.PostAsync($"/api/team/concerns/{TestClient.Str(item, "id")}/answer", new { outcome = "clarified", response = "نعتذر." });
        Assert.Equal(HttpStatusCode.Forbidden, s);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.concern_answer_blocked")));

        var turki = await api.LoginAsync(TeamRequestTests.Turki);
        Assert.Equal(HttpStatusCode.OK, (await turki.PostAsync($"/api/team/concerns/{TestClient.Str(item, "id")}/answer",
            new { outcome = "clarified", response = "نعتذر عن التأخر في التحديث؛ ننتظر رد جهتك وسنبلغك فور وصوله." })).Status);
    }

    [Fact]
    public async Task An_upheld_objection_to_not_suitable_reopens_the_study()
    {
        var (individual, nayef, reference) = await TeamRequestTests.InReviewAsync(api);
        await nayef.PostAsync($"/api/team/requests/{reference}/not-eligible", new { reason = "يبدو أن التمويل شخصي وليس عقارياً." });
        var (sReopen, early) = await nayef.PostAsync($"/api/team/requests/{reference}/reopen", new { reason = "مراجعة" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sReopen);
        Assert.Contains("اعتراض مقبول", TestClient.Raw(early));

        var (_, raised) = await individual.PostAsync($"/api/my/requests/{reference}/concerns",
            new { kind = "objection", subject = "decision", text = "التمويل عقاري؛ أرفقت عقد التمويل." });
        var lead = await api.LoginAsync(TeamRequestTests.Lead);
        var (_, queue) = await lead.GetAsync("/api/team/concerns");
        var id = TestClient.Str(queue!["items"]!.AsArray().First(i => TestClient.Str(i, "reference") == TestClient.Str(raised, "reference")), "id");
        await lead.PostAsync($"/api/team/concerns/{id}/answer", new { outcome = "upheld", response = "راجعنا العقد وتبين أنه تمويل عقاري؛ أعدنا فتح دراسة طلبك." });
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/reopen", new { reason = "اعتراض مقبول على القرار" })).Status);

        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("team_review", TestClient.Str(detail, "status"));
        Assert.True(detail!["canWithdraw"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Specialist_referral_is_a_manual_record_and_the_internal_note_stays_internal()
    {
        var (individual, nayef, reference) = await TeamRequestTests.InReviewAsync(api);
        var (sBad, bad) = await nayef.PostAsync($"/api/team/requests/{reference}/referrals", new { specialistType = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, sBad);
        Assert.NotNull(bad!["errors"]!["specialistType"]);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/referrals", new
        {
            specialistType = "financial_counselling", specialistName = "مستشار مالي معتمد (بيانات تجريبية)", note = "ملاحظة داخلية عن الإحالة",
            applicantText = "أحلناك إلى مستشار مالي لمساعدتك في ترتيب ميزانيتك؛ سيتواصل معك بعد موافقتك.",
        })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        var json = TestClient.Raw(detail);
        Assert.Contains("أحلناك إلى مستشار مالي", json);
        Assert.DoesNotContain("ملاحظة داخلية عن الإحالة", json);
    }

    [Fact]
    public async Task Drafts_cannot_raise_concerns_and_fields_are_validated()
    {
        var individual = await RequestTests.IndividualAsync(api);
        var draft = await RequestTests.DraftAsync(individual);
        Assert.Equal(HttpStatusCode.Conflict, (await individual.PostAsync($"/api/my/requests/{draft}/concerns", new { kind = "objection", subject = "data", text = "x" })).Status);
        var (s, body) = await individual.PostAsync($"/api/my/requests/{draft}/concerns", new { kind = "rant", subject = "?", text = "" });
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.NotNull(body!["errors"]!["kind"]);
        Assert.NotNull(body["errors"]!["text"]);
        var other = await RequestTests.IndividualAsync(api);
        var submitted = await RequestTests.SubmittedAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/api/my/requests/{submitted}/concerns", new { kind = "objection", subject = "data", text = "x" })).Status);
    }
}
