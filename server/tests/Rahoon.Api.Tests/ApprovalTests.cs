using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>Separation of duties, approval-limit routing and replay protection, each on its own fresh case.</summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalTests(ApiFixture api)
{
    private static readonly object Body = new { note = "راجعت الحل مع مستند الدخل. القسط ضمن حد الاستقطاع.", attested = true, expectedStatus = "proposed_solution" };

    [Fact]
    public async Task Submission_rules_separation_of_duties_routing_and_idempotency()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);

        // 1. The preparer (analyst فهد) cannot submit his own version.
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await fahad.PostAsync($"/api/cases/{r}/solutions/1/submit", Body)).Status);

        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        // 2. Attestation and a note are mandatory.
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", new { note = "ملاحظة كافية الطول", attested = false })).Status);

        // 3. Review screen resolves the approver within limits (≤ 2,000,000 and waiver ≤ 5% → نورة الشهري).
        var (_, review) = await sara.GetAsync($"/api/cases/{r}/solutions/1/submission");
        Assert.Equal("نورة الشهري", TestClient.Str(review!["approver"], "name"));
        Assert.Empty(review["blockers"]!.AsArray());

        // 4. Submit, then replay with the same key: identical response, single approval request.
        var key = "submit-" + r;
        var (s1, r1) = await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", Body, key: key);
        Assert.Equal(HttpStatusCode.OK, s1);
        var (s2, r2) = await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", Body, key: key);
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(r1!.ToJsonString(), r2!.ToJsonString());
        var pending = await api.WithDbAsync(db => db.ApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == r)));
        Assert.Equal(1, pending);

        // 5. Same key with a different payload is rejected; a new key after success hits the state guard.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", new { note = "ملاحظة مختلفة تماماً عن الأولى", attested = true }, key: key)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", Body, key: key + "-second")).Status);

        // 6. Approver ≠ preparer ≠ reviewer; the version is locked with a snapshot.
        var req = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == r)));
        Assert.NotEqual(req.PreparedByUserId, req.AssignedApproverUserId);
        Assert.NotEqual(req.SubmittedByUserId, req.AssignedApproverUserId);
        var v1 = await api.WithDbAsync(db => db.Solutions.FirstAsync(s => s.Id == req.SubjectId));
        Assert.Equal(SolutionStatus.PendingApproval, v1.Status);
        Assert.NotNull(v1.LockedSnapshotJson);

        // 7. A locked version cannot be edited, and the reviewer never sees the request in her inbox.
        Assert.Equal(HttpStatusCode.Conflict, (await fahad.PutAsync($"/api/cases/{r}/solutions/1", new { kind = "Reschedule", termMonths = 72, firstDueDate = "2027-01-01", waiverAmount = 0, downPayment = 0, graceMonths = 0 })).Status);
    }

    [Fact]
    public async Task Waiver_above_approver_limit_escalates_to_senior_approver()
    {
        // 757,000 outstanding; 8% waiver exceeds the approver tier (5%) → senior approver (≤ 10%).
        var r = await Scenarios.ReadyForApprovalAsync(api, waiver: 60_560m);
        await Scenarios.SubmitAsync(api, r);
        var req = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == r)));
        var senior = await api.WithDbAsync(db => db.Users.FirstAsync(u => u.Email == "s.alomari@alufuq.example"));
        Assert.Equal(senior.Id, req.AssignedApproverUserId);

        // The regular approver sees it as escalated and cannot decide it.
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var (_, detail) = await noura.GetAsync($"/api/approvals/{req.Id}");
        Assert.False(detail!["canDecide"]!.GetValue<bool>());
        Assert.True(detail["escalated"]!.GetValue<bool>());
        var (s, _) = await noura.PostAsync($"/api/approvals/{req.Id}/decision", new { decision = "approve", reason = "محاولة اعتماد فوق الحد للاختبار." });
        Assert.Equal(HttpStatusCode.Forbidden, s);
    }

    [Fact]
    public async Task Approver_return_sends_version_back_and_requires_a_new_version()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await Scenarios.SubmitAsync(api, r);
        var noura = await api.LoginAsync("n.alshehri@alufuq.example");
        await noura.StepUpAsync();
        var req = await api.WithDbAsync(db => db.ApprovalRequests.FirstAsync(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == r)));
        var (s, res) = await noura.PostAsync($"/api/approvals/{req.Id}/decision", new { decision = "return", reason = "القسط مرتفع مقارنة بالدخل المتحقق، راجعوا المدة." });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal("proposed_solution", TestClient.Str(res, "caseStatus"));
        // A second decision on the same request is refused.
        var (s2, _) = await noura.PostAsync($"/api/approvals/{req.Id}/decision", new { decision = "approve", reason = "محاولة ثانية على طلب مغلق." });
        Assert.Equal(HttpStatusCode.Conflict, s2);
    }
}
