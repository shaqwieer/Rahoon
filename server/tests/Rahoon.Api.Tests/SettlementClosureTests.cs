using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 1A-2 step 7 (L26, settlement path): the seeded RH-2026-003702 is closed by three different people — ريم prepared
/// and submitted the reconciliation (seed), عبدالعزيز reviews it, نورة approves it with a step-up — then ريم requests the
/// closure and نورة decides it. The preparer, the reviewer and the requester are refused where they would decide their own
/// work, and the owner receives the closure documents (D14). `JudicialClosureTests` covers the judicial path.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SettlementClosureTests(ApiFixture api)
{
    private const string Ref = "RH-2026-003702";
    private const string Reem = "r.aldosari@alufuq.example";     // finance: preparer
    private const string Aziz = "a.alshammari@alufuq.example";   // finance: reviewer
    private const string Noura = "n.alshehri@alufuq.example";    // approver

    [Fact]
    public async Task Seeded_settlement_closes_with_three_people_a_step_up_and_the_owner_gets_the_documents()
    {
        var reem = await api.LoginAsync(Reem);
        var (_, start) = await reem.GetAsync($"/api/cases/{Ref}/closure");
        Assert.Equal("awaiting_reconciliation", TestClient.Str(start, "caseStatus"));
        Assert.Equal("Submitted", TestClient.Str(start!["reconciliation"], "status"));
        Assert.Equal(0m, start["reconciliation"]!["difference"]!.GetValue<decimal>());

        // Reconciliation: the preparer can't review it; another finance user does.
        var (sSelf, _) = await reem.PostAsync($"/api/cases/{Ref}/reconciliation/review", new { decision = "approve", reason = "راجعت المطابقة." });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);
        var aziz = await api.LoginAsync(Aziz);
        Assert.Equal(HttpStatusCode.OK, (await aziz.PostAsync($"/api/cases/{Ref}/reconciliation/review",
            new { decision = "approve", reason = "طابقت التحويلين مع كشف الحساب؛ الفرق صفر." })).Status);

        // Approval needs a third person and a step-up.
        var noura = await api.LoginAsync(Noura);
        var (sNoStep, noStep) = await noura.PostAsync($"/api/cases/{Ref}/reconciliation/approve", new { decision = "approve", reason = "المطابقة صفرية الفرق." });
        Assert.Equal(HttpStatusCode.Forbidden, sNoStep);
        Assert.Equal("step_up_required", TestClient.Str(noStep, "code"));
        await noura.StepUpAsync();
        Assert.Equal(HttpStatusCode.OK, (await noura.PostAsync($"/api/cases/{Ref}/reconciliation/approve", new { decision = "approve", reason = "المطابقة صفرية الفرق." })).Status);

        // The owner's final summary still blocks the closure request until it is generated.
        var (sBlocked, blocked) = await reem.PostAsync($"/api/cases/{Ref}/closure/request", new { note = "المطابقة صفرية الفرق. المستندات مكتملة." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sBlocked);
        Assert.NotEmpty(blocked!["reasons"]!.AsArray());
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{Ref}/closure/documents/owner-summary")).Status);
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{Ref}/closure/request", new { note = "المطابقة صفرية الفرق. المستندات مكتملة." })).Status);

        // The requester and the reviewer can't decide; the approver does, with the trace acknowledged.
        await reem.StepUpAsync();
        var (sReq, _) = await reem.PostAsync($"/api/cases/{Ref}/closure/decision", new { decision = "approve", reason = "أعتمد الإغلاق بعد المراجعة.", traceAcknowledged = true });
        Assert.Equal(HttpStatusCode.Forbidden, sReq);
        await aziz.StepUpAsync();
        var (sRev, _) = await aziz.PostAsync($"/api/cases/{Ref}/closure/decision", new { decision = "approve", reason = "أعتمد الإغلاق بعد المراجعة.", traceAcknowledged = true });
        Assert.Equal(HttpStatusCode.Forbidden, sRev);
        var (sDone, done) = await noura.PostAsync($"/api/cases/{Ref}/closure/decision", new { decision = "approve", reason = "المصادر قابلة للتتبع والمستندات مكتملة.", traceAcknowledged = true });
        Assert.True(sDone == HttpStatusCode.OK, TestClient.Raw(done));
        Assert.Equal("closed", TestClient.Str(done, "caseStatus"));
        Assert.True(done!["publishedToOwner"]!.GetValue<int>() >= 3);

        // D14: the owner sees the closure documents.
        var owner = api.Client();
        var token = $"demo-{Ref}";
        var (_, sent) = await owner.PostAsync("/api/auth/owner/verify-id", new { token, idLast4 = "3702" });
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/auth/owner/verify-otp", new { token, code = TestClient.Str(sent, "sandboxCode") })).Status);
        var (sOwner, closure) = await owner.GetAsync("/api/owner/closure");
        Assert.Equal(HttpStatusCode.OK, sOwner);
        Assert.True(closure!["closed"]!.GetValue<bool>());
        var titles = TestClient.Raw(closure["documents"]);
        Assert.Contains("خطاب المخالصة النهائية", titles);
        Assert.Contains("خطاب فك الرهن", titles);

        // Every refusal was audited, and the chain still verifies.
        await api.WithDbAsync(async db =>
        {
            var org = await db.Cases.Where(c => c.Reference == Ref).Select(c => c.OrganizationId).FirstAsync();
            Assert.True(await db.AuditEvents.AnyAsync(e => e.CaseReference == Ref && e.Type == "reconciliation.review_blocked"));
            Assert.True(await db.AuditEvents.AnyAsync(e => e.CaseReference == Ref && e.Type == "closure.decision_blocked"));
            Assert.Null(await AuditLog.VerifyChainAsync(db, org));
            return 0;
        });
    }
}
