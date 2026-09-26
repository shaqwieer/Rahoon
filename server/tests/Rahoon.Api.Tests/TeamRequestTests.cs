using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A step 6 — the «فريق رهون» workspace (ADR 0001 §4.2, §6 #2–#4, #6): assignment-scoped access, review,
/// identity check (V12), information requests, the append-only coordination log gated by consent, messages and
/// internal notes, not-eligible, internal timers that never reach the individual.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TeamRequestTests(ApiFixture api)
{
    public const string Lead = "l.alharbi@team.rahoon.example";
    public const string Nayef = "n.alyami@team.rahoon.example";
    public const string Turki = "t.alshehri@team.rahoon.example";
    public const string Abeer = "a.alqahtani@team.rahoon.example";

    internal static object Entry(bool visible = false, string? applicantText = null, string kind = "general") => new
    {
        channel = "phone", occurredAt = DateTimeOffset.UtcNow.AddMinutes(-30), counterpart = "إدارة التحصيل — أ. خالد", summary = "عرضنا طلب العميل وطلبنا دراسته.",
        visibleToApplicant = visible, applicantText, kind,
    };

    /// <summary>Submitted by a fresh individual and taken + picked up by نايف; identity checked; returns both clients.</summary>
    internal static async Task<(TestClient Individual, TestClient Coordinator, string Reference)> InReviewAsync(ApiFixture api, bool identity = true)
    {
        var individual = await RequestTests.IndividualAsync(api);
        var reference = await RequestTests.SubmittedAsync(api, individual);
        var nayef = await api.LoginAsync(Nayef);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/pick-up", new { nextStep = (string?)null })).Status);
        if (identity) Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/identity-check", new { note = "طابقنا صورة الهوية مع الاسم ورقم الجوال." })).Status);
        return (individual, nayef, reference);
    }

    internal static async Task<(TestClient Individual, TestClient Coordinator, string Reference)> CoordinatingAsync(ApiFixture api)
    {
        var (individual, nayef, reference) = await InReviewAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/coordination", Entry())).Status);
        var (s, b) = await nayef.PostAsync($"/api/team/requests/{reference}/start-coordination", new { nextStep = (string?)null });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(b));
        return (individual, nayef, reference);
    }

    [Fact]
    public async Task Queue_tabs_follow_assignment_and_the_lead_sees_all()
    {
        var reference = await RequestTests.SubmittedAsync(api);
        var nayef = await api.LoginAsync(Nayef);
        var (_, unassigned) = await nayef.GetAsync("/api/team/requests?tab=unassigned");
        Assert.Contains(reference, TestClient.Raw(unassigned));
        var (_, mine) = await nayef.GetAsync("/api/team/requests?tab=mine");
        Assert.Contains("REQ-2026-00302", TestClient.Raw(mine));              // seeded, assigned to نايف
        var (_, asAll) = await nayef.GetAsync("/api/team/requests?tab=all");
        Assert.Equal("mine", TestClient.Str(asAll, "tab"));                   // no view_all → falls back
        Assert.False(asAll!["canViewAll"]!.GetValue<bool>());

        var lead = await api.LoginAsync(Lead);
        var (_, all) = await lead.GetAsync("/api/team/requests?tab=all");
        Assert.Equal("all", TestClient.Str(all, "tab"));
        Assert.Contains(reference, TestClient.Raw(all));
        Assert.Contains("REQ-2026-00302", all.ToJsonString());
        // The internal timer is on the team list; the applicant's name is masked there.
        var item = all["items"]!.AsArray().First(i => TestClient.Str(i, "reference") == reference)!;
        Assert.NotNull(item["timer"]!["level"]);
        Assert.DoesNotContain("عبدالله محمد السبيعي", TestClient.Raw(item));
    }

    [Fact]
    public async Task Drafts_never_reach_the_team_and_other_tenants_get_nothing()
    {
        var individual = await RequestTests.IndividualAsync(api);
        var draft = await RequestTests.DraftAsync(individual);
        var lead = await api.LoginAsync(Lead);
        Assert.Equal(HttpStatusCode.NotFound, (await lead.GetAsync($"/api/team/requests/{draft}")).Status);
        var (_, all) = await lead.GetAsync("/api/team/requests?tab=all");
        Assert.DoesNotContain(draft, TestClient.Raw(all));

        var submitted = await RequestTests.SubmittedAsync(api);
        foreach (var email in new[] { "s.alqahtani@alufuq.example", "a.almutairi@rahoon.example" })
        {
            var other = await api.LoginAsync(email, email.Contains("alufuq") ? "مصرف الأفق" : null);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/team/requests")).Status);
            Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/team/requests/{submitted}")).Status);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await individual.GetAsync("/api/team/requests")).Status);
    }

    [Fact]
    public async Task A_coordinator_cannot_open_a_request_assigned_to_someone_else_and_the_attempt_is_audited()
    {
        var (_, _, reference) = await InReviewAsync(api);
        var turki = await api.LoginAsync(Turki);
        Assert.Equal(HttpStatusCode.Forbidden, (await turki.GetAsync($"/api/team/requests/{reference}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await turki.PostAsync($"/api/team/requests/{reference}/coordination", Entry())).Status);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.SubjectReference == reference && e.Type == "request.access_blocked")));

        var lead = await api.LoginAsync(Lead);
        Assert.Equal(HttpStatusCode.OK, (await lead.GetAsync($"/api/team/requests/{reference}")).Status);
        var (_, members) = await lead.GetAsync("/api/team/members");
        var turkiId = members!.AsArray().First(m => TestClient.Str(m, "name") == "تركي الشهري")!["userId"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.OK, (await lead.PostAsync($"/api/team/requests/{reference}/assign", new { userId = turkiId })).Status);
        Assert.Equal(HttpStatusCode.OK, (await turki.GetAsync($"/api/team/requests/{reference}")).Status);
    }

    [Fact]
    public async Task Start_coordination_needs_identity_check_consent_and_a_logged_contact()
    {
        var (individual, nayef, reference) = await InReviewAsync(api, identity: false);
        var (s1, b1) = await nayef.PostAsync($"/api/team/requests/{reference}/start-coordination", new { nextStep = (string?)null });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s1);
        var reasons = TestClient.Raw(b1!["reasons"]);
        Assert.Contains("هوية", reasons);
        Assert.Contains("سجل التنسيق", reasons);

        await nayef.PostAsync($"/api/team/requests/{reference}/identity-check", new { note = "طابقنا الهوية." });
        // A hidden entry and a visible one: only the visible text reaches the individual.
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/coordination", Entry())).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/coordination",
            Entry(visible: true, applicantText: "تواصلنا مع جهتك الممولة وأرسلنا لها طلبك."))).Status);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/start-coordination", new { nextStep = (string?)null })).Status);

        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("lender_coordination", TestClient.Str(detail, "status"));
        Assert.Equal("lender", TestClient.Str(detail, "waitingOn"));
        var json = TestClient.Raw(detail);
        Assert.Contains("تواصلنا مع جهتك الممولة", json);
        Assert.DoesNotContain("إدارة التحصيل", json);           // counterpart / internal summary never projected
        Assert.DoesNotContain("عرضنا طلب العميل", json);
        Assert.DoesNotContain("daysInStatus", json);             // internal timers (V5) stay on team screens
        Assert.DoesNotContain("طابقنا الهوية", json);

        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        Assert.Equal(2, team!["coordination"]!.AsArray().Count);
        Assert.NotNull(team["timer"]);
    }

    [Fact]
    public async Task No_coordination_without_active_consent_and_the_refusal_is_audited()
    {
        var (individual, nayef, reference) = await CoordinatingAsync(api);
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/consent/withdraw")).Status);
        var (s, body) = await nayef.PostAsync($"/api/team/requests/{reference}/coordination", Entry());
        Assert.True(s is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict, TestClient.Raw(body));
        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        Assert.Equal("info_requested", TestClient.Str(team, "status"));
        Assert.False(team!["consentActive"]!.GetValue<bool>());
        Assert.False(team["can"]!["coordinate"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Information_request_waits_on_the_individual_and_their_answer_returns_it_to_the_team()
    {
        var (individual, nayef, reference) = await InReviewAsync(api);
        var (s, b) = await nayef.PostAsync($"/api/team/requests/{reference}/request-info",
            new { items = new[] { "كشف حساب آخر 3 أشهر" }, message = "نحتاج كشف الحساب لنفهم دخلك الحالي قبل التواصل مع جهتك." });
        Assert.True(s == HttpStatusCode.OK, TestClient.Raw(b));
        var (_, waiting) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("info_requested", TestClient.Str(waiting, "status"));
        Assert.Equal("applicant", TestClient.Str(waiting, "waitingOn"));
        Assert.Contains("كشف الحساب", TestClient.Str(waiting, "nextStepText"));

        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/additions", new { text = "أرفقت الكشف الآن." })).Status);
        var (_, back) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("team_review", TestClient.Str(back, "status"));
        Assert.Equal("team", TestClient.Str(back, "waitingOn"));
    }

    [Fact]
    public async Task Messages_reach_both_sides_but_internal_notes_never_reach_the_individual()
    {
        var (individual, nayef, reference) = await InReviewAsync(api);
        await nayef.PostAsync($"/api/team/requests/{reference}/messages", new { body = "مرحباً، نحن ندرس طلبك الآن." });
        await nayef.PostAsync($"/api/team/requests/{reference}/notes", new { body = "ملاحظة داخلية: التحقق من العقد لاحقاً." });
        Assert.Equal(HttpStatusCode.OK, (await individual.PostAsync($"/api/my/requests/{reference}/messages", new { text = "شكراً لكم." })).Status);

        var (_, mine) = await individual.GetAsync($"/api/my/requests/{reference}/messages");
        var json = TestClient.Raw(mine);
        Assert.Contains("ندرس طلبك", json);
        Assert.Contains("شكراً لكم", json);
        Assert.DoesNotContain("ملاحظة داخلية", json);
        Assert.DoesNotContain("نايف", json);

        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        Assert.Equal(3, team!["messages"]!.AsArray().Count);
    }

    [Fact]
    public async Task Not_eligible_shows_the_reason_to_the_individual()
    {
        var (individual, nayef, reference) = await InReviewAsync(api);
        var (s, _) = await nayef.PostAsync($"/api/team/requests/{reference}/not-eligible", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, s);
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/not-eligible",
            new { reason = "التمويل المذكور تمويل شخصي وليس تمويلاً عقارياً، وهو خارج نطاق الخدمة حالياً." })).Status);
        var (_, detail) = await individual.GetAsync($"/api/my/requests/{reference}");
        Assert.Equal("not_eligible", TestClient.Str(detail, "status"));
        Assert.Contains("خارج نطاق الخدمة", TestClient.Str(detail, "notEligibleReason"));
        Assert.False(detail!["canWithdraw"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Coordination_entries_are_append_only_and_corrections_reference_the_original()
    {
        var (_, nayef, reference) = await CoordinatingAsync(api);
        var (_, team) = await nayef.GetAsync($"/api/team/requests/{reference}");
        var firstId = team!["coordination"]!.AsArray()[0]!["id"]!.GetValue<string>();
        var correction = new
        {
            channel = "email", occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10), counterpart = "إدارة التحصيل", summary = "تصحيح: كان التواصل بالبريد.",
            visibleToApplicant = false, correctsEntryId = firstId,
        };
        Assert.Equal(HttpStatusCode.OK, (await nayef.PostAsync($"/api/team/requests/{reference}/coordination", correction)).Status);
        var (_, after) = await nayef.GetAsync($"/api/team/requests/{reference}");
        var entries = after!["coordination"]!.AsArray();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => TestClient.Str(e, "correctsEntryId") == firstId);
        Assert.Contains(entries, e => TestClient.Str(e, "id") == firstId); // the original is kept

        var (sf, future) = await nayef.PostAsync($"/api/team/requests/{reference}/coordination", new
        {
            channel = "phone", occurredAt = DateTimeOffset.UtcNow.AddDays(2), counterpart = "x", summary = "y", visibleToApplicant = false,
        });
        Assert.Equal(HttpStatusCode.BadRequest, sf);
        Assert.NotNull(future!["errors"]!["occurredAt"]);
    }
}
