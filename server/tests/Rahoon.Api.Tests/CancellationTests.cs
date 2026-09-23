using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>C01 cancellation maker-checker: step-up, reason, open-complaint block, requester ≠ approver, workflow transition.</summary>
[Collection(ApiCollection.Name)]
public sealed class CancellationTests(ApiFixture api)
{
    private const string Reason = "سُدد التمويل بالكامل من جهة خارجية وفق خطاب المخالصة الوارد.";

    private Task OpenComplaintAsync(string r) => api.WithDbAsync(async db =>
    {
        var c = await db.Cases.FirstAsync(x => x.Reference == r);
        db.Complaints.Add(new Complaint
        {
            OrganizationId = c.OrganizationId, Reference = $"CMP-T-{Guid.NewGuid():N}"[..20], CaseId = c.Id, Type = ComplaintType.Complaint, Subject = "اعتراض على المبلغ",
            Body = "أعتقد أن هناك خطأ في المبلغ.", SubmittedVia = "phone", SubmittedByLabel = "المالك", SubmittedAt = DateTimeOffset.UtcNow,
            DueOn = B3Scenarios.TodayRiyadh.AddDays(5),
        });
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Cancellation_needs_reason_step_up_and_a_different_approver()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());

        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = "قصير" })).Status);
        var (sNoMfa, noMfa) = await sara.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = Reason });
        Assert.Equal(HttpStatusCode.Forbidden, sNoMfa);
        Assert.Equal("step_up_required", TestClient.Str(noMfa, "code"));
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example"); // no case.cancel
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = Reason })).Status);

        await sara.StepUpAsync();
        var (s, created) = await sara.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = Reason, expectedStatus = "awaiting_data" });
        Assert.True(s == HttpStatusCode.OK, created?.ToJsonString());
        Assert.Equal("نورة الشهري", TestClient.Str(created, "approver"));
        var id = TestClient.Str(created, "id");
        var (sDup, dup) = await sara.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = Reason });
        Assert.Equal(HttpStatusCode.Conflict, sDup);
        Assert.Equal("cancellation_pending", TestClient.Str(dup, "code"));

        // Workspace exposes the pending request; only the assigned approver may decide.
        var (_, wsSara) = await sara.GetAsync($"/api/cases/{r}");
        var pendingSara = wsSara!["sensitive"]!["pendingCancellation"]!;
        Assert.Equal(id, TestClient.Str(pendingSara, "id"));
        Assert.False(pendingSara["canDecide"]!.GetValue<bool>());
        Assert.Equal(CaseStatus.AwaitingData, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync()));

        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        var (_, wsNoura) = await noura.GetAsync($"/api/cases/{r}");
        Assert.True(wsNoura!["sensitive"]!["pendingCancellation"]!["canDecide"]!.GetValue<bool>());
        var (sNouraNoMfa, _) = await noura.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "approve", reason = "المخالصة موثقة ولا مانع." });
        Assert.Equal(HttpStatusCode.Forbidden, sNouraNoMfa);

        // An approver not assigned to the request (senior approver) cannot take it over.
        var salman = await api.LoginAsync("s.alomari@alufuq.example");
        await salman.StepUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await salman.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "approve", reason = "المخالصة موثقة ولا مانع." })).Status);

        await noura.StepUpAsync();
        var (sOk, ok) = await noura.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "approve", reason = "المخالصة موثقة ولا مانع من الإلغاء." });
        Assert.True(sOk == HttpStatusCode.OK, ok?.ToJsonString());
        Assert.Equal("cancelled", TestClient.Str(ok, "caseStatus"));
        var c = await api.WithDbAsync(db => db.Cases.FirstAsync(x => x.Reference == r));
        Assert.Equal(CaseStatus.Cancelled, c.Status);
        Assert.Equal(Reason, c.CancelReason);
        var a = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(x => x.Id == Guid.Parse(id)));
        Assert.Equal(ApprovalStatus.Approved, a.Status);
        Assert.NotEqual(a.SubmittedByUserId, a.DecidedByUserId);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "case.transition" && e.ToState == "cancelled")));
        Assert.Equal(HttpStatusCode.Conflict, (await noura.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "approve", reason = "محاولة ثانية بعد القرار." })).Status);
    }

    [Fact]
    public async Task Requester_holding_both_permissions_still_cannot_approve_their_own_request()
    {
        var dual = await B3Scenarios.UserWithPermissionsAsync(api, "dual",
            P.PortfolioView, P.CaseView, P.CaseViewAll, P.AuditView, P.CaseCancel, P.CaseCancelApprove);
        var sara = await B3Scenarios.SaraAsync(api);
        var r = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());

        await dual.StepUpAsync();
        var (s, created) = await dual.PostAsync($"/api/cases/{r}/cancellation-requests", new { reason = Reason });
        Assert.True(s == HttpStatusCode.OK, created?.ToJsonString());
        Assert.Equal("نورة الشهري", TestClient.Str(created, "approver")); // routed away from the requester
        var id = TestClient.Str(created, "id");

        var (sSelf, self) = await dual.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "approve", reason = "أعتمد طلبي بنفسي للاختبار." });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);
        Assert.Contains("فصل المهام", TestClient.Str(self, "title"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "cancellation.blocked" && e.Blocked)));
        Assert.Equal(CaseStatus.AwaitingData, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync()));

        // Rejection by the assigned approver leaves the case as it was.
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var (sRej, rej) = await noura.PostAsync($"/api/cases/{r}/cancellation-requests/{id}/decision", new { decision = "reject", reason = "المخالصة غير مرفقة؛ لا يُلغى الملف." });
        Assert.True(sRej == HttpStatusCode.OK, rej?.ToJsonString());
        Assert.Equal("awaiting_data", TestClient.Str(rej, "caseStatus"));
    }

    [Fact]
    public async Task Open_complaint_blocks_the_request_and_the_approval()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        await sara.StepUpAsync();

        var blockedRef = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        await OpenComplaintAsync(blockedRef);
        var (s, res) = await sara.PostAsync($"/api/cases/{blockedRef}/cancellation-requests", new { reason = Reason });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Contains(res!["reasons"]!.AsArray(), x => x!.GetValue<string>().Contains("شكوى"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == blockedRef && e.Type == "cancellation.blocked" && e.Blocked)));
        Assert.False(await api.WithDbAsync(db => db.ApprovalRequests.AnyAsync(a => a.Subject == ApprovalSubject.Cancellation && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == blockedRef))));

        // A complaint opened after the request blocks the approval too (workflow guard), leaving the case untouched.
        var lateRef = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        var (_, created) = await sara.PostAsync($"/api/cases/{lateRef}/cancellation-requests", new { reason = Reason });
        await OpenComplaintAsync(lateRef);
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var (sLate, late) = await noura.PostAsync($"/api/cases/{lateRef}/cancellation-requests/{TestClient.Str(created, "id")}/decision",
            new { decision = "approve", reason = "المخالصة موثقة ولا مانع من الإلغاء." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sLate);
        Assert.Equal("guard_failed", TestClient.Str(late, "code"));
        Assert.Equal(CaseStatus.AwaitingData, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == lateRef).Select(c => c.Status).FirstAsync()));
        Assert.Equal(ApprovalStatus.Pending, await api.WithDbAsync(db => db.ApprovalRequests.Where(a => a.Id == Guid.Parse(TestClient.Str(created, "id"))).Select(a => a.Status).FirstAsync()));
    }
}
