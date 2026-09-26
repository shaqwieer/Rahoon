using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Requests;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A step 5 — the individual's request (ADR 0001 §4.4–§4.6, §6): wizard autosave, consent recorded with an SMS
/// code, submit guards, duplicate warning, isolation between individuals and from staff, explicit DTOs, audit chain.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class RequestTests(ApiFixture api)
{
    private static readonly Random Rng = new();
    internal static string NewId(char first = '1') => first + string.Concat(Enumerable.Range(0, 9).Select(_ => Rng.Next(10)));
    internal static string NewPhone() => "05" + string.Concat(Enumerable.Range(0, 8).Select(_ => Rng.Next(10)));

    internal static async Task<TestClient> IndividualAsync(ApiFixture api)
    {
        var c = api.Client();
        var (s, start) = await c.PostAsync("/api/auth/individual/start", new { nationalId = NewId(), phone = NewPhone() });
        Assert.Equal(HttpStatusCode.OK, s);
        var (v, _) = await c.PostAsync("/api/auth/individual/verify", new { code = TestClient.Str(start, "sandboxCode"), acceptTerms = true, awarenessOptIn = false });
        Assert.Equal(HttpStatusCode.OK, v);
        return c;
    }

    internal static async Task<string> InstitutionIdAsync(TestClient c, string nameAr = "مصرف الأفق")
    {
        var (_, list) = await c.GetAsync("/api/institutions");
        return list!.AsArray().First(i => TestClient.Str(i, "nameAr") == nameAr)!["id"]!.GetValue<string>();
    }

    internal static async Task<string> DraftAsync(TestClient c, string? institutionId = null, string? contract = null)
    {
        var (s, created) = await c.PostAsync("/api/my/requests");
        Assert.Equal(HttpStatusCode.OK, s);
        var reference = TestClient.Str(created, "reference");
        institutionId ??= await InstitutionIdAsync(c);
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsync($"/api/my/requests/{reference}", new { step = "lender", institutionId })).Status);
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsync($"/api/my/requests/{reference}", new
        {
            step = "finance", applicantFullName = "عبدالله محمد السبيعي", contractNumber = contract, monthlyInstallment = 4200m, arrearsDuration = "3to6m", propertyCity = "الرياض",
        })).Status);
        Assert.Equal(HttpStatusCode.OK, (await c.PatchAsync($"/api/my/requests/{reference}", new { step = "situation", pathPreference = "keep_home", affordableMonthly = 2500m, situationText = "انخفض دخلي بعد تغيير العمل." })).Status);
        return reference;
    }

    internal static async Task ConsentAsync(TestClient c, string reference)
    {
        var (s, otp) = await c.PostAsync($"/api/my/requests/{reference}/consent/otp");
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(otp));
        var (sc, body) = await c.PostAsync($"/api/my/requests/{reference}/consent", new { code = TestClient.Str(otp, "sandboxCode"), accept = true });
        Assert.True(sc == HttpStatusCode.OK, TestClient.Raw(body));
    }

    internal static async Task<string> SubmittedAsync(ApiFixture api, TestClient? c = null)
    {
        c ??= await IndividualAsync(api);
        var reference = await DraftAsync(c);
        await ConsentAsync(c, reference);
        var (s, body) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(body));
        return reference;
    }

    private static MultipartFormDataContent Pdf(string kind)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4\n% test\n"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "salary.pdf");
        content.Add(new StringContent(kind), "kind");
        return content;
    }

    [Fact]
    public async Task Individual_fills_the_wizard_consents_uploads_and_submits_and_sees_what_happens_next()
    {
        var c = await IndividualAsync(api);
        var reference = await DraftAsync(c);
        Assert.Matches(@"^REQ-\d{4}-\d{5}$", reference);

        var (su, up) = await c.PostMultipartAsync($"/api/my/requests/{reference}/documents", Pdf("salary_statement"));
        Assert.True(su == HttpStatusCode.OK, TestClient.Raw(up));

        var (_, draft) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("draft", TestClient.Str(draft, "status"));
        Assert.Empty(draft!["missing"]!.AsArray());
        Assert.Null(draft["consent"]);
        Assert.Contains("مصرف الأفق", TestClient.Str(draft["consentText"], "text"));

        await ConsentAsync(c, reference);
        var (s, submitted) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(submitted));

        var (_, detail) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("submitted", TestClient.Str(detail, "status"));
        Assert.Equal("team", TestClient.Str(detail, "waitingOn"));
        Assert.Equal("مصرف الأفق", TestClient.Str(detail["consent"], "recipientName"));
        Assert.Equal(MyRequestEndpoints.ConsentTextVersion, TestClient.Str(detail["consent"], "textVersion"));
        Assert.Single(detail["documents"]!.AsArray());
        Assert.Contains(detail["timeline"]!.AsArray(), e => TestClient.Str(e, "kind") == "submitted");
        Assert.False(detail["canEdit"]!.GetValue<bool>());

        var (_, list) = await c.GetAsync("/api/my/requests");
        Assert.Contains(list!.AsArray(), x => TestClient.Str(x, "reference") == reference && TestClient.Str(x, "status") == "submitted");

        // The collected name replaces the masked-ID display label.
        var (_, me) = await c.GetAsync("/api/auth/me");
        Assert.Equal("عبدالله محمد السبيعي", TestClient.Str(me!["user"], "name"));

        await api.WithDbAsync(async db =>
        {
            var events = await db.AuditEvents.Where(e => e.SubjectReference == reference).OrderBy(e => e.Seq).ToListAsync();
            Assert.Contains(events, e => e.Type == "request.consent_recorded" && e.ActorType == "individual");
            Assert.Contains(events, e => e.Type == "request.transition" && e.ToState == "submitted");
            Assert.All(events, e => Assert.Equal(AuditLog.CurrentHashVersion, e.HashVersion));
            var req = await db.Requests.SingleAsync(x => x.Reference == reference);
            var consent = await db.RequestConsents.SingleAsync(x => x.RequestId == req.Id);
            Assert.Contains("مصرف الأفق", consent.TextSnapshot);
            return 0;
        });
    }

    [Fact]
    public async Task Submit_is_refused_without_consent_and_the_refusal_is_audited()
    {
        var c = await IndividualAsync(api);
        var reference = await DraftAsync(c);
        var (s, body) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Equal("guard_failed", TestClient.Str(body, "code"));
        Assert.Contains(body!["reasons"]!.AsArray(), x => x!.GetValue<string>().Contains("موافقة"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.transition_blocked" && e.Blocked)));
    }

    [Fact]
    public async Task Changing_the_lender_after_consent_requires_a_new_consent()
    {
        var c = await IndividualAsync(api);
        var reference = await DraftAsync(c);
        await ConsentAsync(c, reference);
        var other = await InstitutionIdAsync(c, "مصرف الواحة");
        await c.PatchAsync($"/api/my/requests/{reference}", new { step = "lender", institutionId = other });
        var (_, detail) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Null(detail!["consent"]);
        var (s, _) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);

        await c.PatchAsync($"/api/my/requests/{reference}", new { step = "lender", institutionOtherName = "جهة تمويل غير مدرجة" });
        await ConsentAsync(c, reference);
        var (_, d2) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("جهة تمويل غير مدرجة", TestClient.Str(d2!["consent"], "recipientName"));
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false })).Status);
    }

    [Fact]
    public async Task Submit_is_idempotent_and_the_request_is_locked_afterwards()
    {
        var c = await IndividualAsync(api);
        var reference = await DraftAsync(c);
        await ConsentAsync(c, reference);
        var key = Guid.NewGuid().ToString();
        var (s1, b1) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false }, key);
        var (s2, b2) = await c.PostAsync($"/api/my/requests/{reference}/submit", new { acknowledgeDuplicate = false }, key);
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(TestClient.Raw(b1), TestClient.Raw(b2));
        Assert.Equal(1, await api.WithDbAsync(db => db.AuditEvents.CountAsync(e => e.SubjectReference == reference && e.Type == "request.transition" && e.ToState == "submitted")));

        var (sp, locked) = await c.PatchAsync($"/api/my/requests/{reference}", new { step = "finance", applicantFullName = "اسم آخر" });
        Assert.Equal(HttpStatusCode.Conflict, sp);
        Assert.Equal("locked", TestClient.Str(locked, "code"));

        // Adding information stays possible (add-only).
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{reference}/additions", new { text = "أرفقت كشف الحساب لاحقاً." })).Status);
        var (su, _) = await c.PostMultipartAsync($"/api/my/requests/{reference}/documents", Pdf("bank_statement"));
        Assert.Equal(HttpStatusCode.OK, su);
        var (_, detail) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Contains(detail!["documents"]!.AsArray(), d => d!["addedAfterSubmit"]!.GetValue<bool>());
        Assert.Equal(2, detail["timeline"]!.AsArray().Count(e => TestClient.Str(e, "kind") == "info_added"));
    }

    [Fact]
    public async Task Another_individual_gets_not_found_and_a_blocked_write_is_audited()
    {
        var owner = await IndividualAsync(api);
        var reference = await SubmittedAsync(api, owner);
        var intruder = await IndividualAsync(api);

        var (sg, _) = await intruder.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal(HttpStatusCode.NotFound, sg);
        var (sw, _) = await intruder.PostAsync($"/api/my/requests/{reference}/withdraw", new { reason = "x" });
        Assert.Equal(HttpStatusCode.NotFound, sw);
        var (_, list) = await intruder.GetAsync("/api/my/requests");
        Assert.DoesNotContain(reference, TestClient.Raw(list));

        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.access_blocked" && e.Blocked)));
        var (_, still) = await owner.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("submitted", TestClient.Str(still, "status"));
    }

    [Fact]
    public async Task Staff_platform_admins_and_anonymous_callers_cannot_use_individual_request_endpoints()
    {
        var reference = await SubmittedAsync(api);
        var (anon, _) = await api.Client().GetAsync("/api/my/requests");
        Assert.Equal(HttpStatusCode.Unauthorized, anon);
        foreach (var email in new[] { "s.alqahtani@alufuq.example", "a.almutairi@rahoon.example" })
        {
            var staff = await api.LoginAsync(email, email.Contains("alufuq") ? "مصرف الأفق" : null);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/my/requests")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync($"/api/my/requests/{reference}")).Status);
        }
    }

    [Fact]
    public async Task Several_requests_per_account_and_a_duplicate_is_warned_and_linked_not_blocked()
    {
        var c = await IndividualAsync(api);
        var first = await SubmittedAsync(api, c);

        // A request for another lender: no warning.
        var other = await DraftAsync(c, await InstitutionIdAsync(c, "شركة السنبلة للتمويل"));
        await ConsentAsync(c, other);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{other}/submit", new { acknowledgeDuplicate = false })).Status);

        // Same lender again: warned, then allowed once the individual confirms it is a different finance.
        var dup = await DraftAsync(c);
        await ConsentAsync(c, dup);
        var (_, draft) = await c.GetAsync($"/api/my/requests/{dup}");
        Assert.Equal(first, TestClient.Str(draft!["duplicate"], "reference"));
        var (s, refused) = await c.PostAsync($"/api/my/requests/{dup}/submit", new { acknowledgeDuplicate = false });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Contains(first, TestClient.Raw(refused));
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{dup}/submit", new { acknowledgeDuplicate = true })).Status);

        var (_, list) = await c.GetAsync("/api/my/requests");
        Assert.Equal(3, list!.AsArray().Count);
        Assert.Equal(3, list.AsArray().Select(x => TestClient.Str(x, "reference")).Distinct().Count());
        await api.WithDbAsync(async db =>
        {
            var linked = await db.Requests.SingleAsync(r => r.Reference == dup);
            var original = await db.Requests.SingleAsync(r => r.Reference == first);
            Assert.Equal(original.Id, linked.DuplicateOfRequestId);
            return 0;
        });
    }

    [Fact]
    public async Task Individual_dto_never_contains_team_only_documents_or_internal_entries()
    {
        var c = await IndividualAsync(api);
        var reference = await SubmittedAsync(api, c);
        await api.WithDbAsync(async db =>
        {
            var r = await db.Requests.SingleAsync(x => x.Reference == reference);
            db.RequestDocuments.Add(new RequestDocument
            {
                OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = "other", Name = "مذكرة داخلية سرية",
                Source = "team", Visibility = RequestDocumentVisibility.TeamOnly,
            });
            db.RequestUpdates.Add(new RequestUpdate
            {
                OrganizationId = r.OrganizationId, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = "internal_note", Title = "ملاحظة داخلية سرية",
                VisibleToApplicant = false, At = DateTimeOffset.UtcNow,
            });
            return await db.SaveChangesAsync();
        });
        var (_, detail) = await c.GetAsync($"/api/my/requests/{reference}");
        var json = TestClient.Raw(detail);
        Assert.DoesNotContain("سرية", json);
        Assert.DoesNotContain("assignedCoordinator", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Withdrawing_consent_pauses_the_request_and_a_new_consent_resumes_it()
    {
        var c = await IndividualAsync(api);
        var reference = await SubmittedAsync(api, c);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{reference}/consent/withdraw")).Status);
        var (_, paused) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("info_requested", TestClient.Str(paused, "status"));
        Assert.Equal("applicant", TestClient.Str(paused, "waitingOn"));
        Assert.Null(paused!["consent"]);

        await ConsentAsync(c, reference);
        var (_, resumed) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("submitted", TestClient.Str(resumed, "status"));
        Assert.Equal("team", TestClient.Str(resumed, "waitingOn"));
    }

    [Fact]
    public async Task Withdrawing_the_request_stops_sharing_and_ends_it()
    {
        var c = await IndividualAsync(api);
        var reference = await SubmittedAsync(api, c);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/my/requests/{reference}/withdraw", new { reason = "حُلّت المشكلة" })).Status);
        var (_, detail) = await c.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("withdrawn", TestClient.Str(detail, "status"));
        Assert.False(detail!["canWithdraw"]!.GetValue<bool>());
        Assert.Null(detail["consent"]);
        var (s, _) = await c.PostAsync($"/api/my/requests/{reference}/withdraw", new { reason = "" });
        Assert.Equal(HttpStatusCode.Conflict, s);
    }

    [Fact]
    public async Task Audit_chain_verifies_with_request_events_and_with_older_events()
    {
        await SubmittedAsync(api);
        await api.WithDbAsync(async db =>
        {
            var operatorOrg = await db.Organizations.SingleAsync(o => o.Kind == OrganizationKind.Operator);
            Assert.Null(await AuditLog.VerifyChainAsync(db, operatorOrg.Id));
            var alufuq = await db.Organizations.SingleAsync(o => o.ShortCode == "alufuq");
            Assert.Null(await AuditLog.VerifyChainAsync(db, alufuq.Id));
            // Tampering with a subject field breaks a version-2 event.
            var e = await db.AuditEvents.Where(x => x.OrganizationId == operatorOrg.Id && x.SubjectReference != null).OrderBy(x => x.Seq).FirstAsync();
            e.SubjectReference = "REQ-0000-00000";
            Assert.NotEqual(e.Hash, AuditLog.ComputeHash(e));
            return 0;
        });
    }

    [Fact]
    public async Task Invalid_wizard_values_are_field_errors()
    {
        var c = await IndividualAsync(api);
        var (_, created) = await c.PostAsync("/api/my/requests");
        var reference = TestClient.Str(created, "reference");
        var (s, body) = await c.PatchAsync($"/api/my/requests/{reference}", new { step = "finance", monthlyInstallment = -5m, arrearsDuration = "forever" });
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.NotNull(body!["errors"]!["monthlyInstallment"]);
        Assert.NotNull(body["errors"]!["arrearsDuration"]);
        var (si, bi) = await c.PatchAsync($"/api/my/requests/{reference}", new { step = "lender", institutionId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, si);
        Assert.NotNull(bi!["errors"]!["institutionId"]);
    }
}
