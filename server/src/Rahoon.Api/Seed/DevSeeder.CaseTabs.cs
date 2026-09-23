using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Imports;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// B3 case tabs (L06–L12), L04 import and C01 cancellation demo data. Aligns the anchor case
/// RH-2026-004172 with the design canon (working-notes.md, B3 spec). All data is fictional.
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedCaseTabsAsync()
    {
        var c = _cases["RH-2026-004172"];
        var org = c.OrganizationId;

        // ── L06 parties & contact preferences ──
        var primary = await db.Parties.FirstAsync(p => p.CaseId == c.Id && p.IsPrimary);
        primary.EmploymentStatus = "موظف · قطاع خاص";
        var occupant = factory.NewParty(org, c.Id, PartyRole.Occupant, "نورة عبدالرحمن السبيعي", null, null);
        occupant.Relation = "زوجة المالك · ساكنة في العقار";
        occupant.IsContractParty = false;
        occupant.ContactAllowed = false;
        occupant.Notes = "لا تُشارك معها بيانات مالية";
        var rep = factory.NewParty(org, c.Id, PartyRole.InformalRepresentative, "سلمان عبدالله السبيعي", null, "0551234599");
        rep.Relation = "ابن المالك";
        rep.IsContractParty = false;
        rep.Notes = "يحضر المكالمات بطلب المالك";
        db.Parties.AddRange(occupant, rep);
        var access = await db.OwnerAccesses.FirstAsync(o => o.CaseId == c.Id);
        access.ContactHours = "9 ص – 5 م، أيام العمل";
        access.AllowedChannels = ["platform", "sms"];
        access.CommunicationNeeds = "يطلب المالك أن تكون المكالمات بحضور ابنه (ممثل غير نظامي). لا تُشارك بيانات مالية معه دون تفويض موثق.";

        // ── L07 finance (canon: contract 2021-05-10, 1,450,000, 240 months, 176 remaining; partial payment 2026-05) ──
        var fin = await db.FinancingContracts.FirstAsync(f => f.CaseId == c.Id);
        fin.ContractDate = D("2021-05-10");
        fin.OriginalAmount = 1_450_000.00m;
        fin.OriginalTermMonths = 240;
        fin.RemainingTermMonths = 176;
        fin.FirstOverdueDate = D("2026-02-01");
        var snapshot = await db.DebtSnapshots.FirstAsync(d => d.CaseId == c.Id && d.IsCurrent);
        snapshot.SyncStatus = SyncStatus.Ok;
        var may = await db.InstallmentHistory.FirstAsync(h => h.CaseId == c.Id && h.Month == "2026-05");
        may.Status = InstallmentHistoryStatus.Partial;
        may.AmountPaid = 5_000.00m;

        // ── L08/L09 property & mortgage ──
        var property = await db.Properties.FirstAsync(p => p.CaseId == c.Id);
        property.YearBuilt = 2019;
        property.OccupancyNote = "يسكنها المالك وأسرته";
        var mortgage = await db.Mortgages.FirstAsync(m => m.CaseId == c.Id);
        mortgage.RegisteredOn = D("2021-05-12");
        mortgage.OtherEncumbrances = "لا توجد";
        mortgage.InsuranceValidUntil = D("2027-05-11");
        mortgage.DeedMatched = true;
        mortgage.DeedMatchedOn = D("2026-08-29");
        mortgage.VerificationSource = "مستند مرفوع + مراجعة القانونية";
        mortgage.LegalNote = "الرهن مسجل بالدرجة الأولى ولا توجد قيود لاحقة. لا يوجد ما يمنع التسوية أو البيع الطوعي بموافقة المالك.";
        mortgage.LegalReviewedAt = At("2026-08-28T13:00:00");
        mortgage.LegalReviewedByUserId = UserId("majed");

        // ── L10 documents: salary certificate, internal legal memo, open ID renewal request ──
        db.DocumentTypes.AddRange(
            new DocumentType { Key = "salary_certificate", NameAr = "تعريف بالراتب", Icon = "work", ValidityDays = 90, Sensitive = true },
            new DocumentType { Key = "legal_memo", NameAr = "مذكرة المراجعة القانونية", Icon = "policy", Sensitive = true });
        await AddDocumentAsync(c, "salary_certificate", "تعريف بالراتب", DocumentSource.Owner,
            [("salary-certificate.pdf", "owner", "2026-09-19T21:10:00", ReviewStatus.Verified, "مطابق لكشف الراتب v2.", null)],
            validUntil: D("2026-12-18"), visibleToOwner: true, visibleTo: ["case_team", "approver"]);
        var memo = await AddDocumentAsync(c, "legal_memo", "مذكرة المراجعة القانونية", DocumentSource.Legal,
            [("legal-review-memo.pdf", "majed", "2026-08-28T13:00:00", ReviewStatus.Verified, null, null)], visibleTo: ["legal", "case_team"]);
        memo.Internal = true;
        var nationalId = await db.Documents.FirstAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == "national_id");
        db.DocumentRequests.Add(new DocumentRequest
        {
            OrganizationId = org, CaseId = c.Id, DocumentId = nationalId.Id, DocumentTypeKey = "national_id", RequestedFrom = "owner", DueOn = D("2026-10-03"),
            OwnerMessage = "نحتاج صورة حديثة من هويتك الوطنية لأن الصورة الحالية تنتهي صلاحيتها قريباً. يمكنك رفعها حتى 2026-10-03. إن واجهت صعوبة، اختر «أحتاج مساعدة».",
            Channels = ["sms", "portal"], RequestedByUserId = UserId("sara"), CreatedAt = At("2026-09-23T09:40:00"),
        });

        // ── L11 valuation (canon: 1,650,000 by «مكتب تقييم معتمد «ب»», range 1.58M–1.72M, inspection 2026-09-04 10:00) ──
        var report = await db.ValuationReports.FirstAsync(r => r.CaseId == c.Id);
        report.RangeLow = 1_580_000m;
        report.RangeHigh = 1_720_000m;
        report.Methodology = "المقارنة (5 صفقات) + التكلفة";
        report.ComparablesCount = 5;
        report.InspectionDate = D("2026-09-04");
        var valuationDoc = await db.Documents.FirstAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == "valuation_report");
        report.DocumentVersionId = valuationDoc.CurrentVersionId;
        var asg = await db.Assignments.FirstAsync(a => a.CaseId == c.Id);
        asg.InspectionAt = At("2026-09-04T10:00:00");
        var evidence = new List<string> { $"assignment:{asg.Reference}" };
        Audit(org, c, "mortgage.legal_review", "اكتمال المراجعة القانونية للرهن", At("2026-08-28T13:00:00"), "majed", reason: mortgage.LegalNote, detail: "مطابقة الصك: متحقق 2026-08-29");
        Audit(org, c, "assignment.created", "إنشاء التكليف", At("2026-08-30T10:00:00"), "fahad", detail: "مهلة 10 أيام", evidence: evidence);
        Audit(org, c, "assignment.message", "سؤال من المقيّم: موعد المعاينة", At("2026-08-31T09:15:00"), "valuer", detail: "رد خالد ز. خلال 3 ساعات", evidence: evidence);
        Audit(org, c, "assignment.inspection", "المعاينة بحضور المالك", At("2026-09-04T10:00:00"), "valuer", evidence: evidence);
        Audit(org, c, "valuation.accepted", "اعتماد تقرير التقييم v1", At("2026-09-10T16:00:00"), "fahad", detail: "القيمة السوقية 1,650,000.00 ر.س · صالح حتى 2026-12-09",
            evidence: ["valuation:v1", $"assignment:{asg.Reference}"]);
        Audit(org, c, "assignment.access_expired", "انتهاء وصول المقيّم", At("2026-09-17T13:44:00"), null, detail: "آلي (7 أيام بعد التسليم)", evidence: evidence);

        // ── L12 analysis (canon: income 32,400 from salary statement v2, other obligations 2,100, DSR 46.5% vs 55%) ──
        var analysis = await db.Analyses.FirstAsync(a => a.CaseId == c.Id);
        var salary = await db.Documents.FirstAsync(d => d.CaseId == c.Id && d.DocumentTypeKey == "salary_statement");
        analysis.IncomeDocumentVersionId = salary.CurrentVersionId;
        analysis.OtherObligations = 2_100.00m;
        analysis.CircumstanceIndicators =
        [
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.Indicator("work_history", "انخفاض الدخل 28% منذ 2026-01 (تغيير جهة العمل)")),
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.Indicator("home", "العقار سكن رئيسي للأسرة")),
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.Indicator("handshake", "المالك متعاون ويرد خلال يومين في المتوسط")),
        ];
        analysis.OptionNotes =
        [
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.OptionNote("reschedule", "إعادة جدولة", true, "ممكنة ضمن حد الاستقطاع بمدة ≥ 80 شهراً")),
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.OptionNote("waiver", "تنازل عن الغرامات", true, "ضمن صلاحية المعتمد (1.42%)")),
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.OptionNote("reduced_payoff", "سداد مخفض", false, "لا سيولة مصرّح بها")),
            AnalysisEndpoints.Serialize(new AnalysisEndpoints.OptionNote("voluntary_sale", "بيع طوعي", false, "لم يطلبه المالك؛ لا يُعرض إلا بموافقته")),
        ];

        // ── Other cases: a report under review and a pending legal review (RH-2026-004155, valuation stage) ──
        var c4155 = _cases["RH-2026-004155"];
        db.ValuationReports.Add(new ValuationReport
        {
            OrganizationId = org, CaseId = c4155.Id, VersionNo = 1, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = 1_240_000.00m,
            RangeLow = 1_190_000m, RangeHigh = 1_290_000m, Methodology = "المقارنة (4 صفقات)", ComparablesCount = 4,
            InspectionDate = D("2026-09-16"), ReportDate = D("2026-09-20"), ValidUntil = D("2026-12-19"), Status = ValuationStatus.UnderReview,
        });

        // ── C01: a pending cancellation request (RH-2026-003655, paused) awaiting نورة الشهري ──
        var c3655 = _cases["RH-2026-003655"];
        var cancel = new ApprovalRequest
        {
            OrganizationId = org, CaseId = c3655.Id, Subject = ApprovalSubject.Cancellation, SubjectId = c3655.Id, SubjectVersionNo = 0,
            Title = $"اعتماد إلغاء الحالة {c3655.Reference}", PreparedByUserId = UserId("sara"), SubmittedByUserId = UserId("sara"),
            SubmittedAt = At("2026-09-22T12:30:00"), SubmitterNote = "سُدد التمويل بالكامل من جهة خارجية وفق خطاب المخالصة الوارد، ولا حاجة لاستمرار الحالة.",
            SubmitterAttested = true, AssignedApproverUserId = UserId("noura"), RequiredTier = "case.cancel_approve", DueOn = D("2026-09-24"),
            Evidence = ["status:paused"],
        };
        db.ApprovalRequests.Add(cancel);
        db.Tasks.Add(new CaseTask
        {
            OrganizationId = org, CaseId = c3655.Id, Title = $"اعتماد إلغاء الحالة {c3655.Reference}", AssigneeUserId = UserId("noura"), DueOn = D("2026-09-24"),
            Kind = "approval", Link = $"/cases/{c3655.Reference}?cancellation={cancel.Id}", CreatedByUserId = UserId("sara"),
        });
        Audit(org, c3655, "cancellation.requested", "طلب إلغاء الحالة", At("2026-09-22T12:30:00"), "sara", reason: cancel.SubmitterNote,
            detail: "أُسند للاعتماد إلى نورة الشهري · المهلة 2026-09-24 · تأكيد برمز التحقق", evidence: [$"cancellation:{cancel.Id}"]);
        await db.SaveChangesAsync();

        // ── L04: a validated import batch with row errors and duplicates (step 2 of 3) ──
        // The future-date error row is relative to the real clock so the batch stays 3 ready / 1 duplicate / 5 errors over time.
        var future = clock.TodayRiyadh.AddMonths(3).ToString("yyyy-MM-dd");
        var csv = $"""
            contract_number,owner_name,national_id,mobile,region,city,district,property_type,principal,profit,late_fees,other_fees,outstanding_amount,arrears_amount,arrears_installments,first_overdue_date
            MF-93-1100231,بندر سالم الرويلي,1023456789,0551100231,الرياض,الرياض,حي الملقا,شقة سكنية,540000.00,31000.00,6000.00,0.00,577000.00,24000.00,3,2026-06-01
            MF-93-1100232,غادة فهد المطيري,1034567890,0551100232,مكة المكرمة,جدة,حي الروضة,فيلا سكنية,1210000.00,88000.00,14000.00,0.00,1312000.00,61000.00,5,2026-04-01
            MF-91-0042118,حمد ناصر العجمي,104567890,0551100233,الشرقية,الدمام,حي الشاطئ,دور سكني,690000.00,40000.00,9000.00,0.00,739000.00,30000.00,4,2026-05-01
            MF-87-2215004,ريم خالد الحربي,1056789012,0551100234,الرياض,الرياض,حي العارض,فيلا سكنية,1100000.00,95000.00,5000.00,0.00,1.2 مليون,44000.00,4,2026-05-01
            MF-88-3317406,عبدالله محمد السبيعي,1098734542,0551234581,الرياض,الرياض,حي النرجس,فيلا سكنية,1121840.00,144420.00,18300.00,0.00,1284560.00,96420.00,7,2026-02-01
            MF-90-1180092,سعود عبدالله القحطاني,1067890123,0551100236,عسير,أبها,حي المنسك,فيلا سكنية,760000.00,41000.00,7000.00,0.00,808000.00,36000.00,4,{future}
            MF-92-0019007,لمى سعد الشهري,1078901234,0551100237,Riyad,الرياض,حي الياسمين,شقة سكنية,430000.00,22000.00,3000.00,0.00,455000.00,18000.00,3,2026-06-01
            MF-89-4471550,ماجد عمر الزهراني,1089012345,66 214 81,الرياض,الرياض,حي قرطبة,شقة سكنية,380000.00,19000.00,2000.00,0.00,401000.00,15000.00,3,2026-06-01
            MF-94-2200117,هند علي الدوسري,1090123456,0551100239,المدينة المنورة,المدينة,حي قباء,شقة سكنية,470000.00,26000.00,4000.00,0.00,500000.00,21000.00,3,2026-06-01
            """;
        var batch = await new ImportService(db, factory, clock).ValidateAsync(org, UserId("sara"), "محفظة_سبتمبر_2026.csv", csv);
        batch.CreatedAt = At("2026-09-23T11:20:00");
        Audit(org, null, "import.validated", $"رفع ملف استيراد {batch.FileName}", At("2026-09-23T11:20:00"), "sara",
            detail: $"{batch.TotalRows} صفاً · جاهز {batch.ReadyCount} · تكرار محتمل {batch.DuplicateCount} · أخطاء {batch.ErrorCount}");
        await db.SaveChangesAsync();
    }
}
