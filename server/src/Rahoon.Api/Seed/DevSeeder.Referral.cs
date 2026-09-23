using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Referral;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// B10 referral canon for RH-2026-003511 (فيصل ر.): offers exhausted, voluntary sale refused in writing, notice and
/// 15-day objection period, two-person decision, evidence pack, manual external reference with the official status
/// stored verbatim, agent «مكتب وكيل البيع «ج»» (ياسر الحمدان) with plan, updates and an agent-reported result
/// (760,000 − 22,800 costs) still awaiting official confirmation. Dates are shifted before the demo «today» (2026-09-23).
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedReferralAsync()
    {
        db.Templates.Add(new CommunicationTemplate
        {
            Code = ReferralEndpoints.NoticeTemplate, Title = "إشعار قبل الإحالة", Audience = "owner", VersionNo = 1, Status = TemplateStatus.Published,
            BodyAr = ReferralEndpoints.DefaultNoticeBody, BodySms = ReferralEndpoints.DefaultNoticeSms,
            Variables = ["{المصرف}", "{المهلة}", "{تاريخ_انتهاء_المهلة}"], UpdatedAt = DemoToday.AddDays(-90),
        });
        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-CLOSED-01", Title = "إغلاق الحالة", Audience = "owner", VersionNo = 1, Status = TemplateStatus.Published,
            BodyAr = "أُغلقت حالتك. مستندات الإغلاق (المخالصة وخطاب فك الرهن وملخص حالتك) متاحة في بوابتك للاطلاع والتنزيل.",
            BodySms = "رهون: أُغلقت حالتك ومستنداتها متاحة في بوابتك.", Variables = [], UpdatedAt = DemoToday.AddDays(-90),
        });

        var c = _cases["RH-2026-003511"];
        var org = c.OrganizationId;

        // Three offers between 2025-11 and 2026-06: two declined, the third unanswered (expired).
        (int v, string sent, string until, OfferStatus status, int term, string? reason)[] offers =
        [
            (1, "2025-11-15T10:00:00", "2025-11-25", OfferStatus.Declined, 120, "القسط أعلى من قدرتي الحالية."),
            (2, "2026-02-05T10:00:00", "2026-02-15", OfferStatus.Declined, 180, "لا أستطيع الالتزام بأي قسط قبل استقرار عملي."),
            (3, "2026-06-01T10:00:00", "2026-06-11", OfferStatus.Expired, 180, null),
        ];
        foreach (var o in offers)
        {
            var first = D(o.until).AddMonths(1);
            first = new DateOnly(first.Year, first.Month, 1);
            var s = new SolutionVersion
            {
                OrganizationId = org, CaseId = c.Id, VersionNo = o.v, Kind = SolutionKind.Reschedule,
                Status = o.status == OfferStatus.Declined ? SolutionStatus.Declined : SolutionStatus.Expired,
                OutstandingAtPreparation = 684_200m, TermMonths = o.term, FirstDueDate = first, WaiverAmount = o.v == 3 ? 8_000m : 0,
                PreparedByUserId = UserId("fahad"), PreparedAt = At(o.sent).AddDays(-4), ReviewedByUserId = UserId("sara"), LockedAt = At(o.sent).AddDays(-2),
                Justification = "إعادة جدولة لخفض القسط وفق الدخل المتحقق.", NetIncomeUsed = 14_000m,
            };
            Apply(s, SolutionCalculator.Compute(new SolutionInput(s.Kind, s.OutstandingAtPreparation, s.TermMonths, s.FirstDueDate, s.WaiverAmount, 0, 0, 14_000m, 0.55m)));
            db.Solutions.Add(s);
            db.Offers.Add(new Offer
            {
                OrganizationId = org, CaseId = c.Id, SolutionVersionId = s.Id, VersionNo = o.v, SentAt = At(o.sent), ValidUntil = D(o.until), Status = o.status,
                SentByUserId = UserId("noura"), RespondedAt = o.reason is null ? null : At(o.sent).AddDays(5), DeclineReason = o.reason,
            });
        }

        // Written refusal of the voluntary-sale option (recorded as legal evidence; the platform had no sale offer record).
        var refusal = await AddDocumentAsync(c, "external_official_document", "رفض المالك الكتابي لعرض البيع الطوعي", DocumentSource.Owner,
            [("sale-refusal-3511.pdf", "majed", "2026-07-03T11:00:00", ReviewStatus.Verified, "رفض كتابي موقّع من المالك.", null)], visibleTo: ["case_team", "legal"]);
        db.Set<ReferralChecklistEvidence>().Add(new ReferralChecklistEvidence
        {
            OrganizationId = org, CaseId = c.Id, Key = "voluntary_sale_offered", Note = "عُرض البيع الطوعي 2026-07-02 · رفضه المالك كتابياً",
            DocumentVersionId = refusal.CurrentVersionId, RecordedByUserId = UserId("majed"), RecordedAt = At("2026-07-03T11:05:00"),
        });

        var r = new JudicialReferral
        {
            OrganizationId = org, CaseId = c.Id, Status = ReferralStatus.HandedOff, InitiatedByUserId = UserId("majed"),
            Reason = "استنفاد الحلول الودية ورفض البيع الطوعي كتابياً.",
            NoticeSentAt = At("2026-07-20T10:00:00"), NoticeSentByUserId = UserId("majed"), NoticeTemplateCode = ReferralEndpoints.NoticeTemplate,
            ObjectionPeriodDays = 15, ObjectionEndsOn = D("2026-08-04"), RequestedAt = At("2026-08-25T11:00:00"),
            ApprovedByUserId = UserId("noura"), ApprovedAt = At("2026-09-01T10:00:00"), DecidedByUserId = UserId("noura"), DecidedAt = At("2026-09-01T10:00:00"),
            DecisionReason = "اكتملت الجاهزية 8 من 8 وانقضت مهلة الاعتراض دون اعتراض.",
            ExternalAuthority = "الجهة المختصة بالتنفيذ — جدة", ExternalRequestNumber = "EXT-JD-2026-0038841",
            ExternalReferenceSource = "إشعار استلام من الجهة المختصة", ExternalReferenceEnteredAt = At("2026-09-03T10:20:00"), ExternalReferenceEnteredByUserId = UserId("majed"),
            OfficialStatusText = "قيد التنفيذ لدى الجهة المختصة", OfficialStatusSource = "إشعار الجهة المختصة (إدخال يدوي)", OfficialStatusSyncedAt = At("2026-09-15T09:00:00"),
            IntegrationState = "unavailable", CreatedAt = At("2026-07-20T10:00:00"),
        };
        db.Referrals.Add(r);

        var org0 = _orgs["alufuq"];
        var noticeBody = ReferralEndpoints.DefaultNoticeBody.Replace("{المصرف}", org0.NameAr).Replace("{المهلة}", "15 يوماً").Replace("{تاريخ_انتهاء_المهلة}", "2026-08-04");
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = org, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "system", AuthorLabel = org0.NameAr, Body = noticeBody,
            TemplateKey = ReferralEndpoints.NoticeTemplate, At = At("2026-07-20T10:00:00"),
        });
        db.OutboundMessages.Add(new OutboundMessage
        {
            OrganizationId = org, CaseId = c.Id, Channel = MessageChannel.Sms, Destination = "0558883511",
            Body = ReferralEndpoints.DefaultNoticeSms.Replace("{المصرف}", org0.NameAr).Replace("{تاريخ_انتهاء_المهلة}", "2026-08-04"),
            TemplateCode = ReferralEndpoints.NoticeTemplate, Provider = "sandbox", Status = OutboundStatus.Simulated, CreatedAt = At("2026-07-20T10:00:00"),
        });

        // Evidence pack PKG-3511-01 (locked at approval, exported the same day).
        var docs = await db.Documents.Include(d => d.Versions).Where(d => d.CaseId == c.Id && (d.DocumentTypeKey == "title_deed" || d.DocumentTypeKey == "financing_contract")).ToListAsync();
        var deed = docs.First(d => d.DocumentTypeKey == "title_deed");
        var contract = docs.First(d => d.DocumentTypeKey == "financing_contract");
        var pack = new EvidencePack
        {
            OrganizationId = org, CaseId = c.Id, ReferralId = r.Id, Reference = "PKG-3511-01", SeqNo = 1, Status = EvidencePackStatus.Exported,
            ManifestSha256 = "", BuiltByUserId = UserId("majed"), BuiltAt = At("2026-08-24T15:00:00"), LockedAt = At("2026-09-01T10:00:00"),
            ExportedAt = At("2026-09-01T11:00:00"), ExportedByUserId = UserId("majed"),
            MappingsJson = JsonSerializer.Serialize(new[]
            {
                new FieldMapping("رقم العقد", "MF-21-7735…", "MF-21-7735…", "match", false),
                new FieldMapping("هوية المالك", "1•••••••11", "1•••••••11", "match", false),
                new FieldMapping("المديونية", "684,200.00", "684,200.00", "match", false),
                new FieldMapping("رقم الصك", "3••••••11", "3••••••11", "match", false),
                new FieldMapping("تاريخ الإشعار", "2026-07-20", "", "auto_converted", false),
                new FieldMapping("المدينة", "جدة", "جدة", "match", false),
            }, JsonOptions.Web),
        };
        (string title, string type, string src, string ver, string sha)[] items =
        [
            ("ملخص الحالة ومسارها الزمني", "generated", $"case:{c.Reference}", "مولَّد", ReferralService.Hash(new { c.Reference, stage = "judicial_referral" })),
            ("عقد التمويل وملاحقه", "document", $"document_version:{contract.CurrentVersionId}", "v1", $"sha256:{contract.Versions[0].Sha256}"),
            ("صك الملكية وشهادة الرهن", "document", $"document_version:{deed.CurrentVersionId}", "v1", $"sha256:{deed.Versions[0].Sha256}"),
            ("كشف المديونية المعتمد 2026-09-22", "record", "debt_snapshot:current", "2026-09-22", ReferralService.Hash(new { Total = 684_200m })),
            ("سجل العروض والردود", "generated", $"offers:{c.Reference}", "3 عروض", ReferralService.Hash(offers.Select(o => new { o.v, o.status }))),
            ("إثبات الإشعار المسبق", "record", $"referral_notice:{r.Id}", "2026-07-20", ReferralService.Hash(new { r.NoticeSentAt, r.ObjectionEndsOn })),
            ("سجل التواصل والإشعارات", "generated", $"messages:{c.Reference}", "1 رسالة", ReferralService.Hash(new { messages = 1 })),
        ];
        var seq = 0;
        foreach (var i in items)
            pack.Items.Add(new EvidencePackItem { OrganizationId = org, PackId = pack.Id, CaseId = c.Id, Seq = ++seq, Title = i.title, SourceType = i.type, SourceRef = i.src, VersionLabel = i.ver, Sha256 = i.sha, Verified = true });
        pack.ManifestSha256 = ReferralService.Hash(pack.Items.Select(i => new { i.Seq, i.Title, i.SourceType, i.SourceRef, i.VersionLabel, i.Sha256, i.Verified }));
        db.Set<EvidencePack>().Add(pack);
        r.EvidencePackExportedAt = pack.ExportedAt;
        r.EvidencePackHash = pack.ManifestSha256;

        // Official status observations (manual, verbatim) — never mapped to the platform status.
        (string at, string text, string src)[] official =
        [
            ("2026-09-03T10:14:00", "تم استلام طلب التنفيذ", "إشعار الجهة المختصة"),
            ("2026-09-10T12:30:00", "تكليف وكيل البيع: مكتب وكيل البيع «ج»", "إشعار الجهة المختصة"),
            ("2026-09-12T08:00:00", "إشعار المالك من الجهة وفق إجراءاتها", "إشعار الجهة المختصة"),
            ("2026-09-15T09:00:00", "قيد التنفيذ لدى الجهة المختصة", "إشعار الجهة المختصة (إدخال يدوي)"),
        ];
        foreach (var o in official)
            db.ExternalStatusEntries.Add(new ExternalStatusEntry
            {
                OrganizationId = org, ReferralId = r.Id, CaseId = c.Id, StatusText = o.text, Source = o.src, ObservedAt = At(o.at), EnteredByUserId = UserId("majed"),
                SourceKind = ExternalSourceKind.Manual, Kind = "status", OfficiallyConfirmed = true,
            });

        // Agent assignment (J05–J07).
        var agentOrg = _orgs["agent-j"];
        var asg = new ProviderAssignment
        {
            OrganizationId = org, Reference = "ASG-2026-0511", CaseId = c.Id, ProviderOrganizationId = agentOrg.Id, AssigneeUserId = UserId("yasser"),
            Type = AssignmentType.JudicialSale, Title = "بيع قضائي بتكليف من الجهة المختصة", PropertyLabel = "فيلا سكنية، حي الصفا، جدة",
            Status = AssignmentStatus.Submitted, DueOn = D("2026-10-15"),
            Scope = ["المعاينة وتوثيق حالة العقار", "رفع خطة البيع للجهة المختصة", "تسليم محضر البيع والأدلة"],
            SharedDocumentIds = [deed.Id, contract.Id], CreatedByUserId = UserId("majed"), CreatedAt = At("2026-09-10T12:00:00"),
        };
        db.Assignments.Add(asg);
        (string on, string text)[] plan =
        [
            ("2026-09-12", "معاينة العقار وتوثيق حالته"), ("2026-09-14", "رفع خطة البيع للجهة المختصة"),
            ("2026-09-18", "تنفيذ البيع وفق إجراءات الجهة"), ("2026-09-25", "تسليم محضر البيع والأدلة"),
        ];
        var p = 0;
        foreach (var m in plan)
            db.Set<SalePlanMilestone>().Add(new SalePlanMilestone { OrganizationId = org, AssignmentId = asg.Id, CaseId = c.Id, Seq = ++p, On = D(m.on), Text = m.text });

        var minutes = await AddDocumentAsync(c, "external_official_document", "محضر البيع", DocumentSource.Provider,
            [("محضر_البيع.pdf", "yasser", "2026-09-18T17:35:00", ReviewStatus.Pending, null, null)], visibleTo: ["case_team", "legal"]);
        var authorityNotice = await AddDocumentAsync(c, "external_official_document", "إشعار الجهة بالتكاليف", DocumentSource.Provider,
            [("إشعار_الجهة.pdf", "yasser", "2026-09-18T17:36:00", ReviewStatus.Pending, null, null)], visibleTo: ["case_team", "legal"]);
        var evidence = new List<Guid> { minutes.CurrentVersionId!.Value, authorityNotice.CurrentVersionId!.Value };

        (string at, string kind, string text, bool attach)[] updates =
        [
            ("2026-09-12T14:10:00", "inspection_done", "المعاينة مكتملة؛ العقار مشغول ويحتاج ترتيب إخلاء بالتنسيق.", false),
            ("2026-09-14T09:30:00", "plan_submitted", "رُفعت خطة البيع للجهة.", false),
            ("2026-09-18T17:40:00", "minutes_issued", "صدر محضر البيع — مرفق.", true),
        ];
        foreach (var u in updates)
            db.Set<AgentUpdate>().Add(new AgentUpdate
            {
                OrganizationId = org, AssignmentId = asg.Id, CaseId = c.Id, AuthorUserId = UserId("yasser"), AuthorLabel = $"ياسر الحمدان — {agentOrg.NameAr}",
                Text = u.text, Kind = u.kind, At = At(u.at), AttachmentVersionIds = u.attach ? evidence : [],
            });

        db.Set<SaleResult>().Add(new SaleResult
        {
            OrganizationId = org, CaseId = c.Id, AssignmentId = asg.Id, OfficialSalePrice = 760_000m, SaleMinutesDate = D("2026-09-18"), DeclaredCosts = 22_800m,
            EvidenceVersionIds = evidence, Status = SaleResultStatus.Submitted, Source = ExternalSourceKind.AgentReported,
            SubmittedByUserId = UserId("yasser"), SubmittedAt = At("2026-09-18T17:40:00"),
        });
        db.ExternalStatusEntries.Add(new ExternalStatusEntry
        {
            OrganizationId = org, ReferralId = r.Id, CaseId = c.Id, StatusText = "محضر البيع · 760,000.00 ر.س · تاريخ المحضر 2026-09-18",
            Source = $"أبلغ بها الوكيل — {agentOrg.NameAr}", ObservedAt = At("2026-09-18T17:40:00"), EnteredByUserId = UserId("yasser"),
            SourceKind = ExternalSourceKind.AgentReported, Kind = "agent_report", OfficiallyConfirmed = false,
        });

        db.Set<ReferralException>().Add(new ReferralException
        {
            OrganizationId = org, CaseId = c.Id, ReferralId = r.Id, Reference = "EXC-2026-0001", Type = "status_mismatch", Title = "عدم تطابق الحالة",
            Description = "الجهة: «صدر محضر البيع» · المنصة لم تستلم التحويل بعد. لا تنتقل الحالة إلى «بانتظار التسوية المالية» تلقائياً.",
            PlatformState = "إحالة قضائية", ExternalStateText = "صدر محضر البيع", OwnerUserId = UserId("majed"), DueOn = D("2026-09-28"),
            CreatedByUserId = UserId("majed"), CreatedAt = At("2026-09-21T10:00:00"),
        });

        Audit(org, c, "referral.notice_sent", "إرسال الإشعار المسبق قبل الإحالة للمالك", At("2026-07-20T10:00:00"), "majed",
            detail: "قالب TPL-PREREF-01 · مهلة الاعتراض 15 يوماً حتى 2026-08-04 · رسالة نصية (تجريبية) + البوابة");
        Audit(org, c, "referral.requested", "طلب اعتماد قرار الإحالة القضائية", At("2026-08-25T11:00:00"), "majed", reason: r.Reason);
        Audit(org, c, "case.transition", "حل مقترح ← إحالة قضائية", At("2026-09-01T10:00:00"), "noura", "proposed_solution", "judicial_referral",
            r.DecisionReason, detail: "اعتماد الإحالة القضائية", evidence: [$"referral:{r.Id}", "pack:PKG-3511-01"]);
        Audit(org, c, "referral.decision", "اعتماد قرار الإحالة القضائية (موافقتان)", At("2026-09-01T10:00:05"), "noura", reason: r.DecisionReason, detail: "تأكيد برمز التحقق · مقدم الطلب ≠ المعتمد");
        Audit(org, c, "referral.pack_exported", "تصدير حزمة الأدلة PKG-3511-01 (JSON)", At("2026-09-01T11:00:00"), "majed", detail: pack.ManifestSha256);
        Audit(org, c, "referral.external_reference", "تسجيل مرجع الجهة الخارجي", At("2026-09-03T10:20:00"), "majed", detail: "الجهة المختصة بالتنفيذ — جدة · EXT-JD-2026-•••8841 · إدخال يدوي");
        Audit(org, c, "referral.agent_assigned", $"تكليف وكيل البيع — {agentOrg.NameAr}", At("2026-09-10T12:00:00"), "majed", detail: "ASG-2026-0511 · مستندات مشتركة: 2");
        Audit(org, c, "agent.result_submitted", "إرسال نتيجة البيع (أبلغ بها الوكيل)", At("2026-09-18T17:40:00"), "yasser",
            detail: "ثمن البيع 760,000.00 · التكاليف المعلنة 22,800.00 · أدلة 2 · لا توزيع ولا انتقال تلقائي", role: "وكيل بيع");
        await db.SaveChangesAsync();
    }
}
