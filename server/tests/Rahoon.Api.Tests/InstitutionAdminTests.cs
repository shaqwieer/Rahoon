using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>S05 staff invitations and institution admin A01–A07: maker-checker, platform minima, tenant scoping.</summary>
[Collection(ApiCollection.Name)]
public sealed class InstitutionAdminTests(ApiFixture api)
{
    [Fact]
    public async Task Staff_invitation_is_domain_restricted_single_use_and_ends_in_an_mfa_session()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (bad, badBody) = await layla.PostAsync("/api/settings/invitations", new { email = "someone@gmail.com", fullName = "شخص ما", phone = "0551239876", roleKey = "case_officer" });
        Assert.Equal(HttpStatusCode.BadRequest, bad);
        Assert.NotNull(badBody!["errors"]!["email"]);

        var email = AdminScenarios.UniqueEmail("alufuq.example");
        var (s, inv) = await layla.PostAsync("/api/settings/invitations", new { email, fullName = "ريان خالد العمري", phone = "0551239876", roleKey = "case_officer" });
        Assert.Equal(HttpStatusCode.OK, s);
        var token = TestClient.Str(inv, "sandboxToken");
        // The raw token never reaches storage: the sandbox e-mail body is redacted.
        Assert.False(await api.WithDbAsync(db => db.OutboundMessages.AnyAsync(m => m.Body.Contains(token))));

        var anon = api.Client();
        var (_, info) = await anon.GetAsync($"/api/public/staff-invitations/{token}");
        Assert.Equal("active", TestClient.Str(info, "status"));
        Assert.Equal("مصرف الأفق", TestClient.Str(info, "organization"));
        Assert.Equal("موظف حالة", TestClient.Str(info, "role"));

        // Password policy (S05): ≥ 12, not common, no name/e-mail; acknowledgement required.
        var local = email.Split('@')[0].Split('.')[1];
        var (weak, weakBody) = await anon.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "ريان خالد العمري", password = $"Xy!{local}99", ackPolicy = true });
        Assert.Equal(HttpStatusCode.BadRequest, weak);
        Assert.NotNull(weakBody!["errors"]!["password"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "ريان خالد العمري", password = "short1!", ackPolicy = true })).Status);
        var (noAck, noAckBody) = await anon.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "ريان خالد العمري", password = AdminScenarios.StrongPassword, ackPolicy = false });
        Assert.Equal(HttpStatusCode.BadRequest, noAck);
        Assert.NotNull(noAckBody!["errors"]!["ackPolicy"]);

        var (ok, acc) = await anon.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "ريان خالد العمري", password = AdminScenarios.StrongPassword, ackPolicy = true });
        Assert.Equal(HttpStatusCode.OK, ok);
        Assert.True(acc!["mfaRequired"]!.GetValue<bool>());
        // Not signed in before the SMS step.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/cases?view=all")).Status);
        var (mv, next) = await anon.PostAsync("/api/auth/mfa/verify", new { code = TestClient.Str(acc, "sandboxCode") });
        Assert.Equal(HttpStatusCode.OK, mv);
        Assert.Equal("/portfolio", TestClient.Str(next, "next"));
        var (_, me) = await anon.GetAsync("/api/auth/me");
        Assert.Equal("مصرف الأفق", TestClient.Str(me!["organization"], "name"));

        // Single use.
        var again = api.Client();
        Assert.Equal(HttpStatusCode.Gone, (await again.PostAsync($"/api/public/staff-invitations/{token}/accept", new { fullName = "ريان", password = AdminScenarios.StrongPassword, ackPolicy = true })).Status);
        var (_, used) = await again.GetAsync($"/api/public/staff-invitations/{token}");
        Assert.Equal("used", TestClient.Str(used, "status"));
        Assert.Equal("invalid", TestClient.Str((await again.GetAsync("/api/public/staff-invitations/not-a-token")).Body, "status"));
    }

    [Fact]
    public async Task Role_change_needs_a_different_admin_and_suspension_revokes_sessions()
    {
        var (member, email) = await AdminScenarios.InviteAndAcceptAsync(api);
        var membershipId = await AdminScenarios.MembershipIdAsync(api, email);
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (rs, req) = await layla.PostAsync($"/api/settings/users/{membershipId}/role-change-requests", new { roleKey = "credit_analyst", reason = "انتقال إلى فريق التحليل الائتماني." });
        Assert.True(rs == HttpStatusCode.OK, req?.ToJsonString());
        var requestId = TestClient.Str(req, "id");

        // The requester cannot approve their own request, even with MFA.
        await layla.StepUpAsync();
        var (self, selfBody) = await layla.PostAsync($"/api/settings/role-change-requests/{requestId}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, self);
        Assert.Equal("maker_checker", TestClient.Str(selfBody, "code"));
        // The target cannot approve either (no permission).
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsync($"/api/settings/role-change-requests/{requestId}/approve", new { })).Status);

        // A second admin approves (step-up required).
        var saud = await api.LoginAsync("s.alrashed@alufuq.example");
        var (noMfa, noMfaBody) = await saud.PostAsync($"/api/settings/role-change-requests/{requestId}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, noMfa);
        Assert.Equal("step_up_required", TestClient.Str(noMfaBody, "code"));
        await saud.StepUpAsync();
        var (ok, approved) = await saud.PostAsync($"/api/settings/role-change-requests/{requestId}/approve", new { });
        Assert.True(ok == HttpStatusCode.OK, approved?.ToJsonString());
        var roleKey = await api.WithDbAsync(db => db.MembershipRoles.Where(r => r.MembershipId == membershipId).Select(r => r.Role!.Key).SingleAsync());
        Assert.Equal("credit_analyst", roleKey);
        var (_, me) = await member.GetAsync("/api/auth/me");
        Assert.Contains("analysis.edit", me!["permissions"]!.AsArray().Select(p => p!.GetValue<string>()));

        // Suspension: reason required, audited, and the user's sessions end immediately.
        Assert.Equal(HttpStatusCode.BadRequest, (await layla.PostAsync($"/api/settings/users/{membershipId}/suspend", new { reason = "" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await layla.PostAsync($"/api/settings/users/{membershipId}/suspend", new { reason = "مغادرة الموظف" })).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/portfolio")).Status);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.Type == "user.suspended" && e.Reason == "مغادرة الموظف")));
        var (_, users) = await layla.GetAsync("/api/settings/users");
        Assert.Contains(users!.AsArray(), u => TestClient.Str(u, "email") == email && TestClient.Str(u, "status") == "suspended");
    }

    private static object Tiers(decimal approverWaiver = 0.05m) => new object[]
    {
        new { rank = 0, roleKey = "credit_analyst", levelLabel = "محلل / مدير حالات", solutionKinds = new[] { "Reschedule" }, canApprove = false, escalateToLabel = "إعداد فقط، لا اعتماد" },
        new { rank = 1, roleKey = "approver", levelLabel = "معتمد", solutionKinds = new[] { "Reschedule", "GracePeriod" }, maxAmount = 2_000_000m, maxWaiverPercent = approverWaiver, canApprove = true, escalateToLabel = "معتمد أعلى" },
        new { rank = 2, roleKey = "senior_approver", levelLabel = "معتمد أول", solutionKinds = new[] { "Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale" }, maxAmount = 5_000_000m, maxWaiverPercent = 0.10m, canApprove = true, escalateToLabel = "لجنة المخاطر" },
        new { rank = 3, roleKey = "risk_committee", levelLabel = "لجنة المخاطر", solutionKinds = new[] { "Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale" }, canApprove = true },
    };

    [Fact]
    public async Task Approval_limit_proposal_enforces_platform_minima_and_second_admin()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7).ToString("yyyy-MM-dd");

        // PA07: non-committee waiver above 10% and disabling separation of duties are refused.
        var (w, wBody) = await layla.PostAsync("/api/settings/approval-limits/proposals", new { effectiveFrom, changeSummary = "رفع التنازل", tiers = Tiers(0.15m), submit = true });
        Assert.Equal(HttpStatusCode.BadRequest, w);
        Assert.NotNull(wBody!["errors"]!["tiers"]);
        var (sep, sepBody) = await layla.PostAsync("/api/settings/approval-limits/proposals", new { effectiveFrom, changeSummary = "تعطيل الفصل", tiers = Tiers(), separationOfDuties = false, submit = true });
        Assert.Equal(HttpStatusCode.BadRequest, sep);
        Assert.NotNull(sepBody!["errors"]!["separationOfDuties"]);

        // Same routing as the effective v4 (so concurrent approval tests are unaffected), proposed as vN+1.
        var (ps, proposal) = await layla.PostAsync("/api/settings/approval-limits/proposals", new { effectiveFrom, changeSummary = "توحيد تسميات المستويات دون تغيير الحدود.", tiers = Tiers(), submit = true });
        Assert.True(ps == HttpStatusCode.OK, proposal?.ToJsonString());
        var version = proposal!["version"]!.GetValue<int>();
        Assert.Equal("pending_approval", TestClient.Str(proposal, "status"));

        await layla.StepUpAsync();
        var (self, selfBody) = await layla.PostAsync($"/api/settings/approval-limits/proposals/{version}/approve", new { reason = "اعتماد" });
        Assert.Equal(HttpStatusCode.Forbidden, self);
        Assert.Equal("maker_checker", TestClient.Str(selfBody, "code"));

        var saud = await api.LoginAsync("s.alrashed@alufuq.example");
        await saud.StepUpAsync();
        var (ok, approved) = await saud.PostAsync($"/api/settings/approval-limits/proposals/{version}/approve", new { reason = "راجعت الحدود." });
        Assert.True(ok == HttpStatusCode.OK, approved?.ToJsonString());
        var (_, current) = await layla.GetAsync("/api/settings/approval-limits");
        Assert.Equal(version, current!["effective"]!["version"]!.GetValue<int>());
        var effectiveCount = await api.WithDbAsync(db => db.ApprovalLimitPolicies.CountAsync(p => p.Status == "effective" && db.Organizations.Any(o => o.Id == p.OrganizationId && o.ShortCode == "alufuq")));
        Assert.Equal(1, effectiveCount);
    }

    [Fact]
    public async Task Template_publish_requires_a_publisher_other_than_the_editor()
    {
        var dual = await AdminScenarios.UserWithPermissionsAsync(api, "alufuq", P.TemplateEdit, P.TemplatePublish);
        var editor = await api.LoginAsync(dual);

        // Unknown variables are rejected; the tone check flags coercive wording.
        var (unknown, unknownBody) = await editor.PutAsync("/api/settings/templates/TPL-VISIT-01", new { bodyAr = "نؤكد موعد الزيارة {الموعد} مع {رقم_الهوية}." });
        Assert.Equal(HttpStatusCode.BadRequest, unknown);
        Assert.Contains("{رقم_الهوية}", unknownBody!["errors"]!["bodyAr"]![0]!.GetValue<string>());
        var (_, tone) = await editor.PostAsync("/api/settings/templates/TPL-VISIT-01/tone-check", new { bodyAr = "يجب عليك الحضور فوراً وإلا سنضطر لاتخاذ إجراءات قانونية." });
        Assert.False(tone!.AsArray().First(t => TestClient.Str(t, "key") == "no_threat")!["ok"]!.GetValue<bool>());

        var body = "نؤكد موعد الزيارة يوم {الموعد}. إن لم يناسبك الموعد يمكنك طلب تغييره حتى {المهلة} أو الكتابة لنا بأي سؤال.";
        var (saved, draft) = await editor.PutAsync("/api/settings/templates/TPL-VISIT-01", new { bodyAr = body, bodyEn = "We confirm the visit on {appointment}. You can ask to change it until {deadline}." });
        Assert.True(saved == HttpStatusCode.OK, draft?.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, (await editor.PostAsync("/api/settings/templates/TPL-VISIT-01/submit")).Status);

        var (self, selfBody) = await editor.PostAsync("/api/settings/templates/TPL-VISIT-01/publish");
        Assert.Equal(HttpStatusCode.Forbidden, self);
        Assert.Equal("maker_checker", TestClient.Str(selfBody, "code"));

        var hind = await api.LoginAsync("h.almutairi@alufuq.example"); // compliance: template.publish
        var (pub, published) = await hind.PostAsync("/api/settings/templates/TPL-VISIT-01/publish");
        Assert.True(pub == HttpStatusCode.OK, published?.ToJsonString());
        var (_, detail) = await hind.GetAsync("/api/settings/templates/TPL-VISIT-01");
        Assert.Equal("institution", TestClient.Str(detail!["effective"], "source"));
        Assert.Equal(body, TestClient.Str(detail["effective"], "bodyAr"));

        // Tenant scoping: another institution still sees only the platform base version.
        var alufuqId = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "alufuq").Select(o => o.Id).FirstAsync());
        var baseStill = await api.WithDbAsync(db => db.Templates.AnyAsync(t => t.Code == "TPL-VISIT-01" && t.OrganizationId == null && t.Status == TemplateStatus.Published));
        Assert.True(baseStill);
        Assert.True(await api.WithDbAsync(db => db.Templates.AnyAsync(t => t.Code == "TPL-VISIT-01" && t.OrganizationId == alufuqId && t.Status == TemplateStatus.Published)));
    }

    [Fact]
    public async Task Organization_sla_and_document_rules_respect_platform_bounds()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (_, org) = await layla.GetAsync("/api/settings/organization");
        Assert.True(org!["mfaRequired"]!.GetValue<bool>());
        var (mfa, mfaBody) = await layla.PutAsync("/api/settings/organization", new { nameAr = "مصرف الأفق", city = "الرياض", defaultOwnerLanguage = "ar", idleTimeoutMinutes = 30, allowedEmailDomains = new[] { "alufuq.example" }, mfaRequired = false });
        Assert.Equal(HttpStatusCode.BadRequest, mfa);
        Assert.NotNull(mfaBody!["errors"]!["mfaRequired"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await layla.PutAsync("/api/settings/organization", new { nameAr = "مصرف الأفق", defaultOwnerLanguage = "ar", idleTimeoutMinutes = 240, allowedEmailDomains = new[] { "alufuq.example" } })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await layla.PutAsync("/api/settings/organization", new { nameAr = "مصرف الأفق", defaultOwnerLanguage = "ar", idleTimeoutMinutes = 30, allowedEmailDomains = new[] { "gmail.com" } })).Status);
        // Writing the seeded values back is accepted (and audited).
        Assert.Equal(HttpStatusCode.OK, (await layla.PutAsync("/api/settings/organization", new { nameAr = "مصرف الأفق", city = "الرياض", defaultOwnerLanguage = "ar", idleTimeoutMinutes = 30, allowedEmailDomains = new[] { "alufuq.example" }, mfaRequired = true })).Status);

        var (sla, slaBody) = await layla.PutAsync("/api/settings/sla-rules", new { rules = new[] { new { status = "awaiting_customer", businessDays = 5, ruleNote = "قصير", pausesOnOpenComplaint = true } } });
        Assert.Equal(HttpStatusCode.BadRequest, sla);
        Assert.Contains("7", slaBody!["errors"]!["rules"]![0]!.GetValue<string>());

        var (doc, docBody) = await layla.PutAsync($"/api/settings/document-rules/{await RuleIdAsync("valuation_report")}",
            new { documentTypeKey = "valuation_report", requiredBeforeStatus = "ProposedSolution", uploader = "provider", validityDays = 120, visibleTo = new[] { "case_team" }, ownerSummaryOnly = false });
        Assert.Equal(HttpStatusCode.BadRequest, doc);
        Assert.NotNull(docBody!["errors"]!["validityDays"]);

        var (_, matrix) = await layla.GetAsync("/api/settings/permission-matrix");
        var create = matrix!["rows"]!.AsArray().First(r => TestClient.Str(r, "key") == "case.create")!;
        Assert.Equal("allow", TestClient.Str(create["cells"]!.AsArray().First(c => TestClient.Str(c, "role") == "case_manager"), "grant"));
        Assert.Equal("deny", TestClient.Str(create["cells"]!.AsArray().First(c => TestClient.Str(c, "role") == "approver"), "grant"));
        Assert.Equal("conditional", TestClient.Str(matrix["rows"]!.AsArray().First(r => TestClient.Str(r, "key") == "solution.approve")!["cells"]!.AsArray()
            .First(c => TestClient.Str(c, "role") == "approver"), "grant"));
    }

    private async Task<Guid> RuleIdAsync(string type) =>
        await api.WithDbAsync(db => db.DocumentRules.Where(r => r.DocumentTypeKey == type && db.Organizations.Any(o => o.Id == r.OrganizationId && o.ShortCode == "alufuq")).Select(r => r.Id).FirstAsync());

    [Fact]
    public async Task Reports_export_csv_and_sensitive_activity_is_auditor_only()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (s, csv) = await PostCsvAsync(layla, "/api/settings/reports/cases_by_status/export");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Contains("الحالة", csv);
        Assert.DoesNotContain("1098734542", csv);
        Assert.Equal(HttpStatusCode.Forbidden, (await layla.PostAsync("/api/settings/reports/sensitive_user_activity/export")).Status);

        // A fresh institution auditor (the seeded auditor is used by the lockout test).
        var (auditor, _) = await AdminScenarios.InviteAndAcceptAsync(api, "auditor", layla);
        var (a, activity) = await PostCsvAsync(auditor, "/api/settings/reports/sensitive_user_activity/export");
        Assert.Equal(HttpStatusCode.OK, a);
        Assert.Contains("الفاعل", activity);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.Type == "report.exported" && e.Title.Contains("نشاط المستخدمين الحساس"))));
    }

    internal static async Task<(HttpStatusCode, string)> PostCsvAsync(TestClient c, string path, object? body = null)
    {
        var (s, bytes, _) = await c.PostRawAsync(path, body);
        return (s, Encoding.UTF8.GetString(bytes));
    }
}
