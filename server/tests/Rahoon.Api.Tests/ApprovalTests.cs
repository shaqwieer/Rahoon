using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>Separation of duties, approval routing and replay protection on RH-2026-004172 (seeded v2 in review).</summary>
[Collection(ApiCollection.Name)]
public sealed class ApprovalTests(ApiFixture api)
{
    private const string Ref = "RH-2026-004172";
    private static readonly object Body = new { note = "راجعت الحل مع كشف الراتب v2. القسط ضمن حد الاستقطاع.", attested = true, expectedStatus = "proposed_solution" };

    [Fact]
    public async Task Submission_rules_separation_of_duties_routing_and_idempotency()
    {
        // 1. The preparer (analyst فهد) cannot submit his own version.
        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        var (sPrep, _) = await fahad.PostAsync($"/api/cases/{Ref}/solutions/2/submit", Body);
        Assert.Equal(HttpStatusCode.Forbidden, sPrep);

        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");

        // 2. Attestation and a note are mandatory.
        var (sNoAttest, _) = await sara.PostAsync($"/api/cases/{Ref}/solutions/2/submit", new { note = "ملاحظة كافية الطول", attested = false });
        Assert.Equal(HttpStatusCode.BadRequest, sNoAttest);

        // 3. Review screen resolves the approver within limits (≤ 2,000,000 → معتمد نورة الشهري).
        var (_, review) = await sara.GetAsync($"/api/cases/{Ref}/solutions/2/submission");
        Assert.Equal("نورة الشهري", TestClient.Str(review!["approver"], "name"));
        Assert.Empty(review["blockers"]!.AsArray());

        // 4. Submit, then replay with the same key: identical response, single approval request.
        var (s1, r1) = await sara.PostAsync($"/api/cases/{Ref}/solutions/2/submit", Body, key: "submit-004172-v2");
        Assert.Equal(HttpStatusCode.OK, s1);
        var (s2, r2) = await sara.PostAsync($"/api/cases/{Ref}/solutions/2/submit", Body, key: "submit-004172-v2");
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(r1!.ToJsonString(), r2!.ToJsonString());
        var pending = await api.WithDbAsync(db => db.ApprovalRequests.CountAsync(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == Ref)));
        Assert.Equal(1, pending);

        // 5. Same key with a different payload is rejected; a new key after success hits the state guard.
        var (s3, _) = await sara.PostAsync($"/api/cases/{Ref}/solutions/2/submit", new { note = "ملاحظة مختلفة تماماً عن الأولى", attested = true }, key: "submit-004172-v2");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s3);
        var (s4, _) = await sara.PostAsync($"/api/cases/{Ref}/solutions/2/submit", Body, key: "submit-004172-v2-second");
        Assert.Equal(HttpStatusCode.Conflict, s4);

        // 6. Locked snapshot, approver assignment excludes preparer and reviewer.
        var req = await api.WithDbAsync(db => db.ApprovalRequests.Where(a => a.Status == ApprovalStatus.Pending && db.Cases.Any(c => c.Id == a.CaseId && c.Reference == Ref)).FirstAsync());
        Assert.NotEqual(req.PreparedByUserId, req.AssignedApproverUserId);
        Assert.NotEqual(req.SubmittedByUserId, req.AssignedApproverUserId);
        var v2 = await api.WithDbAsync(db => db.Solutions.FirstAsync(s => s.Id == req.SubjectId));
        Assert.Equal(SolutionStatus.PendingApproval, v2.Status);
        Assert.NotNull(v2.LockedSnapshotJson);

        // 7. A locked version cannot be edited.
        var (s5, _) = await fahad.PutAsync($"/api/cases/{Ref}/solutions/2", new { kind = "Reschedule", termMonths = 72, firstDueDate = "2026-11-01", waiverAmount = 0, downPayment = 0, graceMonths = 0 });
        Assert.Equal(HttpStatusCode.Conflict, s5);
    }
}
