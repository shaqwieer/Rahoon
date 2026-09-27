using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.ReferralScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// B10 J05–J07 and F01–F04 (L26): agent scope and agent-reported result, official confirmation, reconciliation with
/// three distinct people, the canon waterfall (760,000 − 22,800 = 737,200 → 684,200 + 53,000), traceable closure and
/// the owner's closure documents.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class JudicialClosureTests(ApiFixture api)
{
    private static List<string> Reasons(JsonNode? body) => body?["reasons"]?.AsArray().Select(x => x!.GetValue<string>()).ToList() ?? [];

    [Fact]
    public async Task Judicial_sale_to_traceable_closure_with_agent_scope_three_people_and_canon_waterfall()
    {
        var r = await ApprovedAsync(api, debt: 684_200m);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var majed = await api.LoginAsync(Legal);
        Assert.Equal(HttpStatusCode.OK, (await majed.PutAsync($"/api/cases/{r}/referral/external-reference",
            new { authority = "الجهة المختصة بالتنفيذ", requestNumber = "EXT-JD-2026-0077123", source = "إشعار استلام من الجهة" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/external-sale-started", new { reason = "أبلغت الجهة ببدء إجراءات البيع.", expectedStatus = "judicial_referral" })).Status);

        // ── Hand-off to the judicial sale agent (J05) ──
        var (_, agents) = await majed.GetAsync("/api/referrals/agents");
        var agentOrgId = TestClient.Str(agents!.AsArray().First(a => TestClient.Str(a, "nameAr") == "مكتب وكيل البيع «ج»"), "id");
        var yasserId = await api.WithDbAsync(db => db.Users.Where(u => u.Email == Agent).Select(u => u.Id).FirstAsync());
        var (sA, assigned) = await majed.PostAsync($"/api/cases/{r}/referral/agent-assignments", new { agentOrganizationId = agentOrgId, dueOn = Today.AddDays(30).ToString("yyyy-MM-dd"), assigneeUserId = yasserId });
        Assert.True(sA == HttpStatusCode.OK, assigned?.ToJsonString());
        var asg = TestClient.Str(assigned, "reference");

        var yasser = await api.LoginAsync(Agent);
        var (_, list) = await yasser.GetAsync("/api/agent/assignments");
        var refs = list!["items"]!.AsArray().Select(i => TestClient.Str(i, "reference")).ToList();
        Assert.Contains(asg, refs);
        Assert.DoesNotContain("ASG-2026-0418", refs); // the valuer's assignment for another provider
        Assert.All(refs, x => Assert.True(x == asg || x == "ASG-2026-0511", x)); // only JudicialSale assignments to this agent office
        // Scoped: no lender endpoints, no other case's files, no negotiation or complaints in the DTO.
        Assert.Equal(HttpStatusCode.Forbidden, (await yasser.GetAsync($"/api/cases/{r}")).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await yasser.GetAsync("/api/cases/RH-2026-004172/referral")).Status);
        var foreignVersion = await api.WithDbAsync(db => db.DocumentVersions.Where(v => db.Cases.Any(c => c.Id == v.CaseId && c.Reference == "RH-2026-004172")).Select(v => v.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await yasser.GetAsync($"/api/agent/assignments/{asg}/documents/{foreignVersion}/file")).Status);
        var (_, detail) = await yasser.GetAsync($"/api/agent/assignments/{asg}");
        Assert.Null(detail!["negotiation"]);
        Assert.DoesNotContain("خالد سعد", detail.ToJsonString()); // owner's name is not shared with the agent
        Assert.Equal(2, detail["documents"]!.AsArray().Count); // deed + contract shared at hand-off
        var valuer = await api.LoginAsync("o.alanazi@valuer-b.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await valuer.GetAsync("/api/agent/assignments")).Status);

        // ── Agent updates, evidence and result (J06–J07): agent-reported, no state change, no distribution ──
        Assert.Equal(HttpStatusCode.OK, (await yasser.PostAsync($"/api/agent/assignments/{asg}/plan", new { milestones = new[] { new { on = Today.ToString("yyyy-MM-dd"), text = "معاينة العقار وتوثيق حالته" } } })).Status);
        Assert.Equal(HttpStatusCode.OK, (await yasser.PostAsync($"/api/agent/assignments/{asg}/updates", new { text = "المعاينة مكتملة؛ العقار مشغول.", kind = "inspection_done" })).Status);
        var (sE, ev) = await yasser.UploadAsync($"/api/agent/assignments/{asg}/evidence", "محضر_البيع.pdf", new Dictionary<string, string> { ["label"] = "محضر البيع" });
        Assert.True(sE == HttpStatusCode.OK, ev?.ToJsonString());
        var evidenceId = TestClient.Str(ev, "versionId");
        Assert.Equal(HttpStatusCode.OK, (await yasser.GetAsync($"/api/agent/assignments/{asg}/documents/{evidenceId}/file")).Status);
        var (sNoEvidence, _) = await yasser.PostAsync($"/api/agent/assignments/{asg}/result/submit");
        Assert.Equal(HttpStatusCode.Conflict, sNoEvidence); // nothing saved yet
        Assert.Equal(HttpStatusCode.OK, (await yasser.PutAsync($"/api/agent/assignments/{asg}/result",
            new { officialSalePrice = 760_000m, saleMinutesDate = Today.ToString("yyyy-MM-dd"), declaredCosts = 22_800m, evidenceVersionIds = new[] { evidenceId } })).Status);
        var statusBefore = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt }).FirstAsync());
        var (sR, submitted) = await yasser.PostAsync($"/api/agent/assignments/{asg}/result/submit");
        Assert.True(sR == HttpStatusCode.OK, submitted?.ToJsonString());
        Assert.Equal(statusBefore, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt }).FirstAsync()));
        Assert.False(await api.WithDbAsync(db => db.Set<Rahoon.Api.Modules.Closure.Distribution>().AnyAsync(d => db.Cases.Any(c => c.Id == d.CaseId && c.Reference == r))));

        // Recording the external result needs official confirmation first.
        var (sEarly, early) = await majed.PostAsync($"/api/cases/{r}/referral/external-result", new { reason = "صدر محضر البيع ووصل التحويل.", expectedStatus = "external_judicial_sale" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sEarly);
        Assert.Contains(Reasons(early), x => x.Contains("لم تُؤكَّد"));
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/sale-result/confirm",
            new { confirmationSource = "محضر البيع الرسمي من الجهة", officialConfirmationDate = Today.ToString("yyyy-MM-dd") })).Status);
        // Delivery ends the agent's working access (read-only), then access lapses entirely.
        Assert.Equal(HttpStatusCode.Conflict, (await yasser.PostAsync($"/api/agent/assignments/{asg}/updates", new { text = "تحديث بعد التسليم." })).Status);
        await api.WithDbAsync(async db =>
        {
            var a = await db.Assignments.FirstAsync(x => x.Reference == asg);
            a.AccessExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.NotFound, (await yasser.GetAsync($"/api/agent/assignments/{asg}")).Status);

        var (sRes, res) = await majed.PostAsync($"/api/cases/{r}/referral/external-result", new { reason = "صدر محضر البيع ووصل التحويل.", expectedStatus = "external_judicial_sale" });
        Assert.True(sRes == HttpStatusCode.OK, res?.ToJsonString());
        Assert.Equal("awaiting_reconciliation", TestClient.Str(res, "status"));

        // ── F01 reconciliation: an unexplained difference blocks submission and closure ──
        var reem = await api.LoginAsync(Finance);
        var (sRec, rec) = await reem.PostAsync($"/api/cases/{r}/reconciliation", new { });
        Assert.True(sRec == HttpStatusCode.OK, rec?.ToJsonString());
        Assert.Equal(737_200m, rec!["expectedAmount"]!.GetValue<decimal>());
        var (_, wrong) = await reem.PostAsync($"/api/cases/{r}/reconciliation/lines", new { kind = "receipt", label = "المستلم فعلياً", amount = 736_200m, reference = "TRX-T" + Random.Shared.Next(1000000, 9999999), valueDate = Today.ToString("yyyy-MM-dd") });
        Assert.Equal(1_000m, wrong!["difference"]!.GetValue<decimal>());
        var (sUnbal, unbal) = await reem.PostAsync($"/api/cases/{r}/reconciliation/submit", new { note = "للتدقيق" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sUnbal);
        Assert.Equal("unbalanced", TestClient.Str(unbal, "code"));
        var (sClose0, close0) = await reem.PostAsync($"/api/cases/{r}/closure/request", new { note = "طلب إغلاق قبل موازنة المطابقة." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sClose0);
        Assert.Contains(Reasons(close0), x => x.Contains("غير معتمدة") || x.Contains("فرق"));
        var wrongLine = TestClient.Str(wrong["lines"]!.AsArray().First(l => TestClient.Str(l, "kind") == "receipt"), "id");
        Assert.Equal(HttpStatusCode.OK, (await reem.DeleteAsync($"/api/cases/{r}/reconciliation/lines/{wrongLine}")).Status);
        var (_, balanced) = await reem.PostAsync($"/api/cases/{r}/reconciliation/lines", new { kind = "receipt", label = "المستلم فعلياً", amount = 737_200m, reference = "TRX-T" + Random.Shared.Next(1000000, 9999999), valueDate = Today.ToString("yyyy-MM-dd") });
        Assert.Equal(0m, balanced!["difference"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{r}/reconciliation/submit", new { note = "مطابقة صفرية الفرق." })).Status);

        // Preparer ≠ reviewer ≠ approver.
        var (sSelf, self) = await reem.PostAsync($"/api/cases/{r}/reconciliation/review", new { decision = "approve", reason = "دققت عملي بنفسي." });
        Assert.Equal(HttpStatusCode.Forbidden, sSelf);
        Assert.Equal("separation_of_duties", TestClient.Str(self, "code"));
        var aziz = await api.LoginAsync(Checker);
        Assert.Equal(HttpStatusCode.OK, (await aziz.PostAsync($"/api/cases/{r}/reconciliation/review", new { decision = "approve", reason = "المراجع البنكية مطابقة." })).Status);
        await GrantRoleAsync(api, Checker, SystemRoles.Approver);
        try
        {
            await aziz.StepUpAsync();
            var (sRev, rev) = await aziz.PostAsync($"/api/cases/{r}/reconciliation/approve", new { decision = "approve", reason = "أعتمد ما دققته بنفسي." });
            Assert.Equal(HttpStatusCode.Forbidden, sRev);
            Assert.Equal("separation_of_duties", TestClient.Str(rev, "code"));
        }
        finally { await RevokeRoleAsync(api, Checker, SystemRoles.Approver); }
        var noura = await api.LoginAsync(Approver);
        Assert.Equal("step_up_required", TestClient.Str((await noura.PostAsync($"/api/cases/{r}/reconciliation/approve", new { decision = "approve", reason = "مطابقة صفرية الفرق." })).Body, "code"));
        await noura.StepUpAsync();
        Assert.Equal(HttpStatusCode.OK, (await noura.PostAsync($"/api/cases/{r}/reconciliation/approve", new { decision = "approve", reason = "مطابقة صفرية الفرق." })).Status);

        // ── F02 waterfall equals the canon ──
        var (sD, dist) = await reem.PostAsync($"/api/cases/{r}/distribution", new { surplusDestinationMasked = "IBAN ••••4410" });
        Assert.True(sD == HttpStatusCode.OK, dist?.ToJsonString());
        Assert.Equal(760_000m, dist!["salePrice"]!.GetValue<decimal>());
        Assert.Equal(22_800m, dist["procedureCosts"]!.GetValue<decimal>());
        Assert.Equal(737_200m, dist["netProceeds"]!.GetValue<decimal>());
        Assert.Equal(684_200m, dist["lenderShare"]!.GetValue<decimal>());
        Assert.Equal(53_000m, dist["ownerSurplus"]!.GetValue<decimal>());
        Assert.Equal(0m, dist["otherFees"]!.GetValue<decimal>());
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{r}/distribution/submit")).Status);
        Assert.Equal("separation_of_duties", TestClient.Str((await reem.PostAsync($"/api/cases/{r}/distribution/check", new { decision = "approve", reason = "أدقق توزيعي." })).Body, "code"));
        Assert.Equal(HttpStatusCode.OK, (await aziz.PostAsync($"/api/cases/{r}/distribution/check", new { decision = "approve", reason = "الشلال مطابق للكشف." })).Status);
        Assert.Equal(HttpStatusCode.OK, (await noura.PostAsync($"/api/cases/{r}/distribution/approve", new { decision = "approve", reason = "التوزيع مطابق." })).Status);
        var (_, approvedDist) = await reem.GetAsync($"/api/cases/{r}/distribution");
        var lines = approvedDist!["distribution"]!["lines"]!.AsArray().Select(l => new { lineId = TestClient.Str(l, "id"), txnRef = "TRX-E" + Random.Shared.Next(1000000, 9999999), executedOn = Today.ToString("yyyy-MM-dd") }).ToArray();
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{r}/distribution/execute", new { lines })).Status);

        // ── F03 documents: closure is refused until they are ready ──
        var (sDocs, docsBlocked) = await reem.PostAsync($"/api/cases/{r}/closure/request", new { note = "مطابقة صفرية والتوزيع منفذ بمراجع." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sDocs);
        Assert.Contains(Reasons(docsBlocked), x => x.Contains("خطاب المخالصة النهائية"));
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{r}/closure/documents/init")).Status);
        var (_, docs) = await reem.GetAsync($"/api/cases/{r}/closure/documents");
        string DocId(string type) => TestClient.Str(docs!["items"]!.AsArray().First(d => TestClient.Str(d, "type") == type), "id");
        Assert.Equal(HttpStatusCode.OK, (await reem.UploadAsync($"/api/cases/{r}/closure/documents/{DocId("final_clearance")}/file", "clearance.pdf")).Status);
        Assert.Equal(HttpStatusCode.OK, (await majed.UploadAsync($"/api/cases/{r}/closure/documents/{DocId("lien_release_letter")}/file", "lien-release.pdf")).Status);
        Assert.Equal(HttpStatusCode.OK, (await reem.PostAsync($"/api/cases/{r}/closure/documents/owner-summary")).Status);
        var (_, ownerBefore) = await owner.GetAsync("/api/owner/closure");
        Assert.Empty(ownerBefore!["documents"]!.AsArray()); // nothing published before closure

        // ── F04 traceable closure: second person, MFA, every figure with a source ──
        var (sReq, req) = await reem.PostAsync($"/api/cases/{r}/closure/request", new { note = "مطابقة صفرية الفرق، والتوزيع منفذ بمراجع." });
        Assert.True(sReq == HttpStatusCode.OK, req?.ToJsonString());
        var (_, trace) = await noura.GetAsync($"/api/cases/{r}/closure/trace");
        Assert.Equal(0, trace!["missingSources"]!.GetValue<int>());
        Assert.Contains(trace["items"]!.AsArray(), t => TestClient.Str(t, "sourceType") == "official" && t!["amount"]!.GetValue<decimal>() == 760_000m);
        // The reconciliation reviewer (finance holds case.close) cannot also approve the closure.
        await aziz.StepUpAsync();
        Assert.Equal("separation_of_duties", TestClient.Str((await aziz.PostAsync($"/api/cases/{r}/closure/decision", new { decision = "approve", reason = "أعتمد الإغلاق الذي دققته.", traceAcknowledged = true })).Body, "code"));
        Assert.Equal(HttpStatusCode.BadRequest, (await noura.PostAsync($"/api/cases/{r}/closure/decision", new { decision = "approve", reason = "مطابقة صفرية الفرق.", traceAcknowledged = false })).Status);
        var (sClose, closed) = await noura.PostAsync($"/api/cases/{r}/closure/decision", new { decision = "approve", reason = "مطابقة صفرية الفرق، والتوزيع منفذ بمراجع.", traceAcknowledged = true });
        Assert.True(sClose == HttpStatusCode.OK, closed?.ToJsonString());
        Assert.Equal("closed", TestClient.Str(closed, "caseStatus"));

        // Owner: the three closure documents become visible and downloadable; the portal turns read-only.
        var (_, ownerAfter) = await owner.GetAsync("/api/owner/closure");
        Assert.True(ownerAfter!["closed"]!.GetValue<bool>());
        var ownerDocs = ownerAfter["documents"]!.AsArray();
        Assert.Equal(3, ownerDocs.Count);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/owner/files/{TestClient.Str(ownerDocs[0], "fileVersionId")}")).Status);
        var (sRo, ro) = await owner.PostAsync("/api/owner/messages", new { body = "شكراً لكم على الإغلاق." });
        Assert.Equal(HttpStatusCode.Forbidden, sRo);
        Assert.Equal("case_closed_read_only", TestClient.Str(ro, "code"));

        // After the read-only window (90 days, assumption) the owner session lapses.
        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            c.ClosedAt = DateTimeOffset.UtcNow.AddDays(-91);
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync("/api/owner/closure")).Status);

        // Every step above appended to the lender's hash chain in order (blocked attempts included); agent actions land in the case's chain.
        var orgId = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "alufuq").Select(o => (Guid?)o.Id).FirstAsync());
        // Verify the segment appended since this case was created (the org-wide check is in WorkflowTests; under
        // EnsureCreated the tampering test can alter the org's first event because the append-only trigger lives in the migration).
        var segmentOk = await api.WithDbAsync(async db =>
        {
            var first = await db.AuditEvents.Where(e => e.OrganizationId == orgId && e.CaseReference == r).MinAsync(e => e.Seq);
            var rows = await db.AuditEvents.Where(e => e.OrganizationId == orgId && e.Seq >= first).OrderBy(e => e.Seq).ToListAsync();
            var prev = await db.AuditEvents.Where(e => e.OrganizationId == orgId && e.Seq < first).OrderByDescending(e => e.Seq).Select(e => e.Hash).FirstAsync();
            foreach (var e in rows)
            {
                if (e.PrevHash != prev || Rahoon.Api.Modules.Audit.AuditLog.ComputeHash(e) != e.Hash) return false;
                prev = e.Hash;
            }
            return rows.Count > 20;
        });
        Assert.True(segmentOk);
        var agentOrg = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "agent-j").Select(o => (Guid?)o.Id).FirstAsync());
        Assert.False(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.OrganizationId == agentOrg && e.CaseReference == r)));
    }
}
