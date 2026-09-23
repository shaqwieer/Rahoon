using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Referral;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.ReferralScenarios;

namespace Rahoon.Api.Tests;

/// <summary>B5 L25 / B10 J01–J04 and the owner referral view: a separate, approved, manual decision.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReferralTests(ApiFixture api)
{
    private static List<string> Reasons(System.Text.Json.Nodes.JsonNode? body) =>
        body?["reasons"]?.AsArray().Select(x => x!.GetValue<string>()).ToList() ?? [];

    [Fact]
    public async Task Referral_needs_readiness_notice_and_objection_period_then_a_different_approver_with_step_up()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await PlantDeclinedOfferAsync(api, r);
        var majed = await api.LoginAsync(Legal);

        // No notice yet: nothing to request.
        var (s0, b0) = await majed.PostAsync($"/api/cases/{r}/referral/request", new { reason = "استنفاد الحلول الودية ورفض البيع الطوعي." });
        Assert.Equal(HttpStatusCode.Conflict, s0);
        Assert.Equal("no_referral", TestClient.Str(b0, "code"));

        // The pre-referral notice goes to the owner (sandbox SMS + portal) and starts the objection period.
        var (sn, notice) = await majed.PostAsync($"/api/cases/{r}/referral/notice");
        Assert.True(sn == HttpStatusCode.OK, notice?.ToJsonString());
        Assert.Equal(Today.AddDays(notice!["periodDays"]!.GetValue<int>()).ToString("yyyy-MM-dd"), TestClient.Str(notice, "objectionEndsOn"));
        Assert.True(await api.WithDbAsync(db => db.OutboundMessages.AnyAsync(m => m.TemplateCode == ReferralEndpoints.NoticeTemplate && db.Cases.Any(c => c.Id == m.CaseId && c.Reference == r))));

        // Readiness blocks the request: objection period running and no evidence the voluntary sale was offered. The refusal is audited.
        var (s1, b1) = await majed.PostAsync($"/api/cases/{r}/referral/request", new { reason = "استنفاد الحلول الودية ورفض البيع الطوعي." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s1);
        Assert.Contains(Reasons(b1), x => x.Contains("انقضاء مهلة الاعتراض"));
        Assert.Contains(Reasons(b1), x => x.Contains("عرض البيع الطوعي"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "referral.request_blocked" && e.Blocked)));

        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/readiness/voluntary_sale_offered/evidence", new { note = "عُرض البيع الطوعي ورفضه المالك كتابياً." })).Status);
        await ElapseObjectionPeriodAsync(api, r);
        var (_, readiness) = await majed.GetAsync($"/api/cases/{r}/referral/readiness");
        Assert.Equal(8, readiness!["metCount"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/request", new { reason = "استنفاد الحلول الودية ورفض البيع الطوعي كتابياً." })).Status);

        // The approver must re-enter an OTP.
        var noura = await api.LoginAsync(Approver);
        var (sNoMfa, noMfa) = await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض." });
        Assert.Equal(HttpStatusCode.Forbidden, sNoMfa);
        Assert.Equal("step_up_required", TestClient.Str(noMfa, "code"));

        // The initiator cannot approve their own request even when holding the approve permission.
        await GrantRoleAsync(api, Legal, SystemRoles.Approver);
        try
        {
            await majed.StepUpAsync();
            var (sSelf, self) = await majed.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "أعتمد طلبي بنفسي للاختبار." });
            Assert.Equal(HttpStatusCode.Forbidden, sSelf);
            Assert.Equal("separation_of_duties", TestClient.Str(self, "code"));
            Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "referral.decision_blocked" && e.Blocked)));
        }
        finally { await RevokeRoleAsync(api, Legal, SystemRoles.Approver); }

        await noura.StepUpAsync();
        // A live offer blocks the decision (re-checked at decision time).
        await api.WithDbAsync(async db =>
        {
            var o = await db.Offers.FirstAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r));
            o.Status = OfferStatus.Sent; o.ValidUntil = Today.AddDays(5);
            return await db.SaveChangesAsync();
        });
        var (sLive, live) = await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sLive);
        Assert.Contains(Reasons(live), x => x.Contains("عرض قائم"));
        await api.WithDbAsync(async db =>
        {
            var o = await db.Offers.FirstAsync(x => db.Cases.Any(c => c.Id == x.CaseId && c.Reference == r));
            o.Status = OfferStatus.Declined; o.ValidUntil = Today.AddDays(-20);
            return await db.SaveChangesAsync();
        });

        // An open complaint or objection blocks it too; the case never moves.
        var complaintId = await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var complaint = new Complaint
            {
                OrganizationId = c.OrganizationId, Reference = "CMP-T-" + Guid.NewGuid().ToString("N")[..8], CaseId = c.Id, Type = ComplaintType.Objection,
                Subject = "اعتراض على الإحالة", Body = "أعترض على الإحالة لأن لدي عرض شراء.", SubmittedVia = "owner_portal", SubmittedByLabel = "المالك",
                SubmittedAt = DateTimeOffset.UtcNow, Status = ComplaintStatus.InReview, DueOn = Today.AddDays(5),
            };
            db.Complaints.Add(complaint);
            await db.SaveChangesAsync();
            return complaint.Id;
        });
        var (sC, blocked) = await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض." });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, sC);
        Assert.Contains(Reasons(blocked), x => x.Contains("شكوى"));
        Assert.Equal(CaseStatus.ProposedSolution, await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => c.Status).FirstAsync()));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "case.transition_blocked" && e.ToState == "judicial_referral")));

        await api.WithDbAsync(async db =>
        {
            (await db.Complaints.FirstAsync(x => x.Id == complaintId)).Status = ComplaintStatus.Resolved;
            return await db.SaveChangesAsync();
        });
        var (sOk, ok) = await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض دون اعتراض مفتوح." });
        Assert.True(sOk == HttpStatusCode.OK, ok?.ToJsonString());
        Assert.Equal("judicial_referral", TestClient.Str(ok, "caseStatus"));
    }

    [Fact]
    public async Task External_status_is_stored_verbatim_and_only_an_explicit_transition_moves_the_case()
    {
        var r = await ApprovedAsync(api);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var majed = await api.LoginAsync(Legal);

        var (sNoRef, noRef) = await majed.PostAsync($"/api/cases/{r}/referral/external-status", new { statusText = "تم استلام الطلب", source = "إشعار الجهة" });
        Assert.Equal(HttpStatusCode.Conflict, sNoRef);
        Assert.Equal("no_external_reference", TestClient.Str(noRef, "code"));
        Assert.Equal(HttpStatusCode.OK, (await majed.PutAsync($"/api/cases/{r}/referral/external-reference",
            new { authority = "الجهة المختصة بالتنفيذ", requestNumber = "EXT-JD-2026-0099001", source = "إشعار استلام من الجهة" })).Status);

        var before = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt }).FirstAsync());
        const string verbatim = "  «صدر محضر البيع» — بانتظار الإيداع\n(رقم الملف 7/1448)  ";
        var (s, added) = await majed.PostAsync($"/api/cases/{r}/referral/external-status", new { statusText = verbatim, source = "اتصال رسمي من الجهة", note = "إدخال يدوي" });
        Assert.True(s == HttpStatusCode.OK, added?.ToJsonString());
        Assert.Equal("judicial_referral", TestClient.Str(added, "caseStatus"));

        var stored = await api.WithDbAsync(db => db.ExternalStatusEntries.Where(e => db.Cases.Any(c => c.Id == e.CaseId && c.Reference == r)).OrderByDescending(e => e.ObservedAt).Select(e => e.StatusText).FirstAsync());
        Assert.Equal(verbatim, stored);
        var after = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == r).Select(c => new { c.Status, c.StatusChangedAt }).FirstAsync());
        Assert.Equal(before, after); // «صدر محضر البيع» is never mapped to an internal status
        var (_, overview) = await majed.GetAsync($"/api/cases/{r}/referral");
        Assert.Equal(verbatim, TestClient.Str(overview!["officialStatus"], "text"));
        Assert.Equal("manual", TestClient.Str(overview["integration"], "entryMode"));

        // Owner sees the verbatim text calmly, with rights; never internal trail or agent reports.
        var (so, mine) = await owner.GetAsync("/api/owner/referral");
        Assert.Equal(HttpStatusCode.OK, so);
        Assert.True(mine!["visible"]!.GetValue<bool>());
        Assert.Equal(verbatim, TestClient.Str(mine["officialStatus"], "text"));
        Assert.NotEmpty(mine["rights"]!.AsArray());
        Assert.DoesNotContain("InitiatedBy", mine.ToJsonString());
        Assert.DoesNotContain("أبلغ بها الوكيل", mine.ToJsonString());

        // Only the explicit, reasoned transition moves the case.
        var (st, moved) = await majed.PostAsync($"/api/cases/{r}/referral/external-sale-started", new { reason = "أبلغت الجهة ببدء إجراءات البيع.", expectedStatus = "judicial_referral" });
        Assert.True(st == HttpStatusCode.OK, moved?.ToJsonString());
        Assert.Equal("external_judicial_sale", TestClient.Str(moved, "status"));
    }

    [Fact]
    public async Task Owner_sees_nothing_before_the_notice_and_the_objection_right_after_it()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await PlantDeclinedOfferAsync(api, r);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, none) = await owner.GetAsync("/api/owner/referral");
        Assert.False(none!["visible"]!.GetValue<bool>());

        var majed = await api.LoginAsync(Legal);
        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/notice")).Status);
        var (_, view) = await owner.GetAsync("/api/owner/referral");
        Assert.True(view!["visible"]!.GetValue<bool>());
        Assert.True(view["notice"]!["objectionOpen"]!.GetValue<bool>());
        Assert.Null(view["officialStatus"]);
        Assert.Contains(view["actions"]!.AsArray(), a => TestClient.Str(a, "key") == "object" && a!["enabled"]!.GetValue<bool>());
        var (_, home) = await owner.GetAsync("/api/owner/journey");
        Assert.DoesNotContain("إحالة", home!["steps"]!.ToJsonString()); // referral is never an expected stage
    }

    [Fact]
    public async Task Seeded_referral_shows_canon_with_agent_result_unconfirmed()
    {
        var majed = await api.LoginAsync(Legal);
        var (s, o) = await majed.GetAsync("/api/cases/RH-2026-003511/referral");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal("judicial_referral", TestClient.Str(o, "caseStatus"));
        Assert.Equal(8, o!["readiness"]!["metCount"]!.GetValue<int>());
        Assert.Equal("قيد التنفيذ لدى الجهة المختصة", TestClient.Str(o["officialStatus"], "text"));
        Assert.Equal("EXT-JD-2026-•••8841", TestClient.Str(o["externalReference"], "requestNumberMasked"));
        Assert.Equal("Submitted", TestClient.Str(o["saleResult"], "status"));
        var preview = o["waterfallPreview"]!;
        Assert.Equal("agent_reported", TestClient.Str(preview, "basis"));
        Assert.Equal(737_200m, preview["net"]!.GetValue<decimal>());
        Assert.Equal(684_200m, preview["lenderShare"]!.GetValue<decimal>());
        Assert.Equal(53_000m, preview["ownerSurplus"]!.GetValue<decimal>());
        Assert.Contains(o["history"]!.AsArray(), h => TestClient.Str(h, "sourceKind") == "agent_reported" && TestClient.Str(h, "suffix") == "(غير مؤكد رسمياً بعد)");
        var (_, queue) = await majed.GetAsync("/api/referrals/exceptions");
        Assert.Contains(queue!["items"]!.AsArray(), i => TestClient.Str(i, "reference") == "EXC-2026-0001");
    }

    [Fact]
    public async Task Evidence_pack_is_numbered_hashed_and_exported_only_after_approval()
    {
        var r = await ReadyAsync(api);
        // Give the scenario's verified documents real versions and complete the identity/deed checks the field mapping reads.
        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            foreach (var d in await db.Documents.Where(d => d.CaseId == c.Id && (d.DocumentTypeKey == "title_deed" || d.DocumentTypeKey == "financing_contract")).ToListAsync())
            {
                var v = new Rahoon.Api.Modules.Documents.DocumentVersion
                {
                    OrganizationId = c.OrganizationId, DocumentId = d.Id, CaseId = c.Id, VersionNo = 1, FileName = d.DocumentTypeKey + ".pdf", ContentType = "application/pdf",
                    SizeBytes = 100, Sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), StorageKey = "test/none", UploadedByLabel = "test", UploadedAt = DateTimeOffset.UtcNow,
                };
                db.DocumentVersions.Add(v);
                d.CurrentVersionId = v.Id;
                d.VersionCount = 1;
            }
            (await db.Parties.FirstAsync(p => p.CaseId == c.Id && p.IsPrimary)).IdentityVerifiedAt = DateTimeOffset.UtcNow;
            (await db.Mortgages.FirstAsync(m => m.CaseId == c.Id)).DeedMatched = true;
            return await db.SaveChangesAsync();
        });
        var majed = await api.LoginAsync(Legal);
        var (sb, pack) = await majed.PostAsync($"/api/cases/{r}/referral/pack");
        Assert.True(sb == HttpStatusCode.OK, pack?.ToJsonString());
        var items = pack!["items"]!.AsArray();
        Assert.Equal("01", TestClient.Str(items[0], "n"));
        Assert.All(items, i => Assert.StartsWith("sha256:", TestClient.Str(i, "sha256")));
        Assert.Equal(0, pack["blockingCount"]!.GetValue<int>());

        var (sEarly, early) = await majed.GetAsync($"/api/cases/{r}/referral/pack/export?format=csv");
        Assert.Equal(HttpStatusCode.Conflict, sEarly);
        Assert.Equal("export_blocked", TestClient.Str(early, "code"));

        Assert.Equal(HttpStatusCode.OK, (await majed.PostAsync($"/api/cases/{r}/referral/request", new { reason = "استنفاد الحلول الودية ورفض البيع الطوعي كتابياً." })).Status);
        var noura = await api.LoginAsync(Approver);
        await noura.StepUpAsync();
        Assert.Equal(HttpStatusCode.OK, (await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض." })).Status);
        Assert.Equal("pack_locked", TestClient.Str((await majed.PostAsync($"/api/cases/{r}/referral/pack")).Body, "code"));
        var (sx, _) = await majed.GetAsync($"/api/cases/{r}/referral/pack/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, sx);
        var (sj, manifest) = await majed.GetAsync($"/api/cases/{r}/referral/pack/export?format=json");
        Assert.Equal(HttpStatusCode.OK, sj);
        Assert.Equal(TestClient.Str(pack, "manifestSha256"), TestClient.Str(manifest, "manifestSha256"));
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Type == "referral.pack_exported")));
    }

    [Fact]
    public async Task Exceptions_are_registered_and_resolved_with_a_note_without_moving_the_case()
    {
        var r = await ApprovedAsync(api);
        var majed = await api.LoginAsync(Legal);
        var (s, e) = await majed.PostAsync($"/api/cases/{r}/referral/exceptions", new
        {
            type = "status_mismatch", title = "عدم تطابق الحالة", description = "الجهة أبلغت بصدور المحضر والمنصة لم تستلم التحويل.",
            externalStateText = "صدر محضر البيع", dueOn = Today.AddDays(3).ToString("yyyy-MM-dd"),
        });
        Assert.True(s == HttpStatusCode.OK, e?.ToJsonString());
        var id = TestClient.Str(e, "id");
        Assert.Equal(HttpStatusCode.BadRequest, (await majed.PostAsync($"/api/referrals/exceptions/{id}/resolve", new { action = "record_and_wait", note = "" })).Status);
        var (_, esc) = await majed.PostAsync($"/api/referrals/exceptions/{id}/resolve", new { action = "escalate", note = "صُعّد للجهة عبر القناة اليدوية." });
        Assert.Equal("Pending", TestClient.Str(esc, "status"));
        var (_, done) = await majed.PostAsync($"/api/referrals/exceptions/{id}/resolve", new { action = "record_and_wait", note = "النتيجة مسجلة وفق المحضر؛ بانتظار إشعار التحويل." });
        Assert.Equal("Resolved", TestClient.Str(done, "status"));
        Assert.Equal("judicial_referral", TestClient.Str(done, "caseStatus"));
    }
}
