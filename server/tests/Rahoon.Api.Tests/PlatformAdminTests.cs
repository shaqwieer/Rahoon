using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>S02 demo requests → PA02 onboarding, PA06 masked monitoring and dual-approved temporary access, PA11 masking.</summary>
[Collection(ApiCollection.Name)]
public sealed partial class PlatformAdminTests(ApiFixture api)
{
    [GeneratedRegex(@"RH-\d{4}-\d{6}")]
    private static partial Regex CaseRef();

    [Fact]
    public async Task Demo_request_is_validated_and_creates_no_account()
    {
        var anon = api.Client();
        var valid = new { orgName = "شركة الريادة للتمويل", orgType = "finance", portfolioSize = "500 – 2,000 حالة", contactName = "هالة المالكي", jobTitle = "مديرة التحصيل", consent = true };

        var (s1, b1) = await anon.PostAsync("/api/public/demo-requests", new { valid.orgName, valid.orgType, valid.contactName, valid.jobTitle, email = "l.alghamdi@gmail.com", consent = true });
        Assert.Equal(HttpStatusCode.BadRequest, s1);
        Assert.Equal("استخدم بريد المنشأة وليس بريداً شخصياً", b1!["errors"]!["email"]![0]!.GetValue<string>());

        var domain = $"riyada{Guid.NewGuid().ToString("N")[..6]}.example";
        var email = $"h.almalki@{domain}";
        var (s2, b2) = await anon.PostAsync("/api/public/demo-requests", new { valid.orgName, valid.orgType, valid.contactName, valid.jobTitle, email, consent = false });
        Assert.Equal(HttpStatusCode.BadRequest, s2);
        Assert.NotNull(b2!["errors"]!["consent"]);
        var (s3, b3) = await anon.PostAsync("/api/public/demo-requests", new { orgType = "finance", valid.contactName, email, consent = true });
        Assert.Equal(HttpStatusCode.BadRequest, s3);
        Assert.NotNull(b3!["errors"]!["orgName"]);
        Assert.NotNull(b3["errors"]!["jobTitle"]);

        var (ok, created) = await anon.PostAsync("/api/public/demo-requests", new { valid.orgName, valid.orgType, valid.portfolioSize, valid.contactName, valid.jobTitle, email, mobile = "0551112299", consent = true });
        Assert.True(ok == HttpStatusCode.OK, created?.ToJsonString());
        var reference = TestClient.Str(created, "reference");
        Assert.Matches(@"^APP-\d{4}-\d{4}$", reference);
        Assert.False(await api.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));

        // PA02: review → approve provisions an onboarding institution and invites the contact as its admin.
        var ahmad = await api.LoginAsync("a.almutairi@rahoon.example");
        Assert.Equal(HttpStatusCode.Conflict, (await ahmad.PostAsync($"/api/platform/institution-applications/{reference}/review", new { action = "approve" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await ahmad.PostAsync($"/api/platform/institution-applications/{reference}/review", new { action = "in_review", verificationNote = "مستندات الترخيص مرفوعة" })).Status);
        var (ap, approved) = await ahmad.PostAsync($"/api/platform/institution-applications/{reference}/review", new { action = "approve", note = "اكتمل التحقق اليدوي من الترخيص." });
        Assert.True(ap == HttpStatusCode.OK, approved?.ToJsonString());
        var orgId = Guid.Parse(TestClient.Str(approved, "organizationId"));
        Assert.Equal(OrganizationStatus.Onboarding, await api.WithDbAsync(db => db.Organizations.Where(o => o.Id == orgId).Select(o => o.Status).FirstAsync()));

        var admin = api.Client();
        var token = TestClient.Str(approved, "sandboxToken");
        var (_, info) = await admin.GetAsync($"/api/public/staff-invitations/{token}");
        Assert.Equal("مسؤول المنشأة", TestClient.Str(info, "role"));
        var (acc, accepted) = await admin.PostAsync($"/api/public/staff-invitations/{token}/accept",
            new { fullName = "هالة المالكي", password = AdminScenarios.StrongPassword, ackPolicy = true, phone = "0551112299" });
        Assert.True(acc == HttpStatusCode.OK, accepted?.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync("/api/auth/mfa/verify", new { code = TestClient.Str(accepted, "sandboxCode") })).Status);
        var (_, me) = await admin.GetAsync("/api/auth/me");
        Assert.Equal("شركة الريادة للتمويل", TestClient.Str(me!["organization"], "name"));
        Assert.Contains("org_admin", me["roles"]!.AsArray().Select(r => r!.GetValue<string>()));
        Assert.Equal(OrganizationStatus.Active, await api.WithDbAsync(db => db.Organizations.Where(o => o.Id == orgId).Select(o => o.Status).FirstAsync()));
    }

    /// <summary>Requests temp access for the first monitored case of the organization the requester has no open request for.</summary>
    private static async Task<(string RequestId, string MaskedId)> RequestAccessAsync(TestClient requester, Guid orgId)
    {
        var (s, monitor) = await requester.GetAsync($"/api/platform/cases/monitor?organizationId={orgId}");
        Assert.Equal(HttpStatusCode.OK, s);
        foreach (var row in monitor!["items"]!.AsArray())
        {
            var (rs, req) = await requester.PostAsync("/api/platform/temp-access", new
            {
                handle = TestClient.Str(row, "handle"), reason = "SUP-2026-1201 — قالب رسالة الرفض لا يعرض السبب.", supportTicketRef = "SUP-2026-1201", durationMinutes = 120,
            });
            if (rs == HttpStatusCode.Conflict) continue;
            Assert.True(rs == HttpStatusCode.OK, req?.ToJsonString());
            return (TestClient.Str(req, "id"), TestClient.Str(req, "maskedId"));
        }
        throw new InvalidOperationException("no case available for a temp-access request");
    }

    [Fact]
    public async Task Case_monitoring_is_masked_and_temp_access_needs_institution_and_auditor()
    {
        var alufuq = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "alufuq").Select(o => o.Id).FirstAsync());
        var rana = await api.LoginAsync("r.alsubaie@rahoon.example"); // دعم تقني

        // Monitoring: opaque ids only — no references, names, ids or amounts.
        var (ms, monitor) = await rana.GetAsync($"/api/platform/cases/monitor?organizationId={alufuq}");
        Assert.Equal(HttpStatusCode.OK, ms);
        var json = monitor!.ToJsonString();
        Assert.DoesNotMatch(CaseRef(), json);
        Assert.DoesNotContain("عبدالله", json);
        Assert.DoesNotContain("1,284,560", json);
        Assert.All(monitor["items"]!.AsArray(), i => Assert.StartsWith("C-", TestClient.Str(i, "maskedId")));
        Assert.Equal(HttpStatusCode.Forbidden, (await rana.GetAsync("/api/cases/RH-2026-004172")).Status);

        // Request validation: support ticket reference and ≤ 4 hours.
        var handle = TestClient.Str(monitor["items"]![0], "handle");
        Assert.Equal(HttpStatusCode.BadRequest, (await rana.PostAsync("/api/platform/temp-access", new { handle, reason = "مشكلة في القالب لدى المنشأة", supportTicketRef = "1201", durationMinutes = 120 })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await rana.PostAsync("/api/platform/temp-access", new { handle, reason = "مشكلة في القالب لدى المنشأة", supportTicketRef = "SUP-2026-1201", durationMinutes = 300 })).Status);

        var (requestId, maskedId) = await RequestAccessAsync(rana, alufuq);
        Assert.Equal(HttpStatusCode.Forbidden, (await rana.PostAsync($"/api/platform/temp-access/{requestId}/approve")).Status); // no auditor permission
        var (_, notYet) = await rana.GetAsync($"/api/platform/temp-access/{requestId}/view");
        Assert.Equal("temp_access_inactive", TestClient.Str(notYet, "code"));

        // (b) platform auditor.
        var faisal = await api.LoginAsync("f.aldossary@rahoon.example");
        await faisal.StepUpAsync();
        var (fa, afterAuditor) = await faisal.PostAsync($"/api/platform/temp-access/{requestId}/approve");
        Assert.True(fa == HttpStatusCode.OK, afterAuditor?.ToJsonString());
        Assert.Equal("pending", TestClient.Str(afterAuditor, "status"));

        // (a) an org_admin of that institution — not any institution user.
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        Assert.Equal(HttpStatusCode.Forbidden, (await sara.GetAsync("/api/settings/temp-access")).Status);
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await maha.PostAsync($"/api/settings/temp-access/{requestId}/approve")).Status);
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (_, pendingList) = await layla.GetAsync("/api/settings/temp-access");
        Assert.Contains(pendingList!.AsArray(), r => TestClient.Str(r, "id") == requestId && r!["canApprove"]!.GetValue<bool>());
        await layla.StepUpAsync();
        var (la, active) = await layla.PostAsync($"/api/settings/temp-access/{requestId}/approve");
        Assert.True(la == HttpStatusCode.OK, active?.ToJsonString());
        Assert.Equal("active", TestClient.Str(active, "status"));

        // While active the requester sees the real reference, read-only; every screen is logged.
        var (vs, view) = await rana.GetAsync($"/api/platform/temp-access/{requestId}/view?screen=documents");
        Assert.True(vs == HttpStatusCode.OK, view?.ToJsonString());
        Assert.Matches(CaseRef(), TestClient.Str(view, "reference"));
        Assert.Equal(maskedId, TestClient.Str(view, "maskedId"));
        Assert.True(view!["readOnly"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await rana.GetAsync($"/api/cases/{TestClient.Str(view, "reference")}")).Status); // still no lender endpoints
        var reqGuid = Guid.Parse(requestId);
        Assert.Equal(1, await api.WithDbAsync(db => db.Set<TempAccessViewLog>().CountAsync(v => v.RequestId == reqGuid)));

        // Auto-expiry: access ends, the institution is notified with the view count.
        await api.WithDbAsync(async db =>
        {
            var t = await db.TempAccessRequests.FirstAsync(x => x.Id == reqGuid);
            t.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-5);
            return await db.SaveChangesAsync();
        });
        var (es, expired) = await rana.GetAsync($"/api/platform/temp-access/{requestId}/view");
        Assert.Equal(HttpStatusCode.Forbidden, es);
        Assert.Equal("temp_access_inactive", TestClient.Str(expired, "code"));
        var closed = await api.WithDbAsync(db => db.TempAccessRequests.FirstAsync(x => x.Id == reqGuid));
        Assert.Equal(TempAccessStatus.Expired, closed.Status);
        Assert.NotNull(closed.InstitutionNotifiedAt);
        var laylaId = await api.WithDbAsync(db => db.Users.Where(u => u.Email == "l.alghamdi@alufuq.example").Select(u => u.Id).FirstAsync());
        Assert.True(await api.WithDbAsync(db => db.Notifications.AnyAsync(n => n.UserId == laylaId && n.Title == "انتهى وصول الدعم المؤقت" && n.Body!.Contains(closed.MaskedCaseId))));
    }

    [Fact]
    public async Task Requester_can_never_approve_their_own_temp_access()
    {
        var alufuq = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "alufuq").Select(o => o.Id).FirstAsync());
        var dualEmail = await AdminScenarios.UserWithPermissionsAsync(api, "platform", P.PlatformOps, P.PlatformTempAccess, P.PlatformTempAccessApprove);
        var dual = await api.LoginAsync(dualEmail);
        var (requestId, _) = await RequestAccessAsync(dual, alufuq);
        await dual.StepUpAsync();
        var (s, body) = await dual.PostAsync($"/api/platform/temp-access/{requestId}/approve");
        Assert.Equal(HttpStatusCode.Forbidden, s);
        Assert.Equal("maker_checker", TestClient.Str(body, "code"));
        Assert.Equal(HttpStatusCode.OK, (await dual.PostAsync($"/api/platform/temp-access/{requestId}/revoke", new { reason = "انتهى الاختبار" })).Status);
    }

    [Fact]
    public async Task Platform_views_are_aggregate_and_audit_references_are_masked()
    {
        var ahmad = await api.LoginAsync("a.almutairi@rahoon.example");
        var (os, ops) = await ahmad.GetAsync("/api/platform/ops");
        Assert.Equal(HttpStatusCode.OK, os);
        Assert.DoesNotMatch(CaseRef(), ops!.ToJsonString());
        Assert.True(ops["kpis"]!.AsArray().First(k => TestClient.Str(k, "key") == "active_institutions")!["value"]!.GetValue<int>() >= 3);
        var (ss, services) = await ahmad.GetAsync("/api/platform/services");
        Assert.Equal(HttpStatusCode.OK, ss);
        Assert.Equal("enabled", TestClient.Str(services!["services"]!.AsArray().First(x => TestClient.Str(x, "key") == "database"), "state"));
        var (_, catalog) = await ahmad.GetAsync("/api/platform/permissions");
        Assert.Equal(P.Catalog.Count, catalog!["items"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await ahmad.GetAsync("/api/platform/audit")).Status); // ops is not an auditor

        var faisal = await api.LoginAsync("f.aldossary@rahoon.example");
        var (a, audit) = await faisal.GetAsync("/api/platform/audit");
        Assert.Equal(HttpStatusCode.OK, a);
        Assert.NotEmpty(audit!["items"]!.AsArray());
        Assert.DoesNotMatch(CaseRef(), audit.ToJsonString());
        Assert.Equal(HttpStatusCode.BadRequest, (await faisal.PostAsync("/api/platform/audit/exports", new { reason = "" })).Status);
        var (es, bytes, headers) = await faisal.PostRawAsync("/api/platform/audit/exports", new { reason = "طلب قانوني" });
        Assert.Equal(HttpStatusCode.OK, es);
        Assert.True(headers.ContainsKey("X-Content-SHA256"));
        Assert.DoesNotMatch(CaseRef(), System.Text.Encoding.UTF8.GetString(bytes));

        var hessa = await api.LoginAsync("h.alotaibi@rahoon.example"); // الامتثال
        var (cs, complaints) = await hessa.GetAsync("/api/platform/complaints?level=all");
        Assert.Equal(HttpStatusCode.OK, cs);
        Assert.All(complaints!["items"]!.AsArray(), c => Assert.True(c!["subjectHidden"]!.GetValue<bool>()));
        var (_, rules) = await hessa.GetAsync("/api/platform/defaults/approval-rules");
        Assert.Contains(rules!["rules"]!.AsArray(), r => TestClient.Str(r, "key") == PlatformRuleKeys.SeparationOfDuties && !r!["editable"]!.GetValue<bool>());
        var (rs, rb) = await hessa.PutAsync("/api/platform/defaults/approval-rules", new { rules = new[] { new { key = PlatformRuleKeys.SeparationOfDuties, value = 0m } }, reason = "محاولة" });
        Assert.Equal(HttpStatusCode.BadRequest, rs);
        Assert.NotNull(rb!["errors"]!["rules"]);
    }
}
