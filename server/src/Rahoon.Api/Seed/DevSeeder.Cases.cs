using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Seed;

public sealed partial class DevSeeder
{
    private readonly Dictionary<string, Case> _cases = new();

    private async Task SeedCanonicalCasesAsync()
    {
        // RH-2026-004172 — the anchor case used throughout the design (working-notes.md canon).
        var c = await BuildCaseAsync(new CaseSpec("RH-2026-004172", "alufuq", "عبدالله محمد السبيعي", "1098734542", "0551234581",
            "الرياض", "حي النرجس", "فيلا سكنية · دوران", CaseStatus.ProposedSolution, "sara",
            1_284_560.00m, 1_121_840.00m, 144_420.00m, 18_300.00m, 96_420.00m, 7, "2026-02-01", "2026-08-14", "MF-88-3317406", 13_774.29m,
            "2026-09-25", "2026-09-10T11:00:00", ShortLabel: "فيلا سكنية، حي النرجس، الرياض"));
        _cases[c.Reference] = c;
        await SeedAnchorCaseDetailsAsync(c);

        var specs = new[]
        {
            new CaseSpec("RH-2026-003988", "alufuq", "منيرة عبدالرحمن العتيبي", "1087321988", "0559871238", "جدة", "حي الروضة", "شقة سكنية",
                CaseStatus.AwaitingCustomer, "khaled", 742_118.40m, 690_000.00m, 42_118.40m, 10_000.00m, 58_300.00m, 5, "2026-04-01", "2026-07-02", "MF-72-1190233", 11_660.00m,
                "2026-09-20", "2026-09-06T10:00:00"),
            new CaseSpec("RH-2026-004090", "alufuq", "نوف سعد الحارثي", "1076654090", "0556610090", "مكة", "حي العزيزية", "شقة سكنية",
                CaseStatus.InternalApproval, "sara", 1_120_450.00m, 1_030_000.00m, 78_450.00m, 12_000.00m, 71_400.00m, 6, "2026-03-01", "2026-07-20", "MF-81-5540912", 11_900.00m,
                "2026-09-23", "2026-09-18T09:00:00"),
            new CaseSpec("RH-2026-004012", "alufuq", "عائشة فهد القرشي", "1065544012", "0554404012", "الرياض", "حي الملقا", "فيلا سكنية",
                CaseStatus.Negotiation, "sara", 2_240_000.00m, 2_050_000.00m, 160_000.00m, 30_000.00m, 204_000.00m, 11, "2025-10-01", "2026-06-11", "MF-66-2204012", 18_545.45m,
                "2026-09-25", "2026-09-16T13:00:00"),
            new CaseSpec("RH-2026-004155", "alufuq", "سلطان حمد الدوسري", "1054434155", "0553304155", "الرياض", "حي العارض", "دور سكني",
                CaseStatus.Valuation, "sara", 968_300.00m, 900_000.00m, 58_300.00m, 10_000.00m, 42_000.00m, 4, "2026-05-01", "2026-08-25", "MF-90-4415501", 10_500.00m,
                "2026-09-26", "2026-09-12T10:00:00"),
            new CaseSpec("RH-2026-004201", "alufuq", "شركة ر. للمقاولات", "7001234201", "0552204201", "الدمام", "حي الشاطئ", "مبنى تجاري سكني",
                CaseStatus.Verification, "sara", 3_410_000.00m, 3_150_000.00m, 220_000.00m, 40_000.00m, 297_000.00m, 9, "2025-12-01", "2026-09-01", "MF-55-7720011", 33_000.00m,
                "2026-09-29", "2026-09-15T10:00:00", Kind: Modules.Cases.PartyKind.Organization, ShortLabel: "مبنى تجاري سكني، حي الشاطئ، الدمام"),
            new CaseSpec("RH-2026-004233", "alufuq", "هيفاء قاسم الزهراني", "1043324233", "0552234233", "الخبر", "حي العليا", "فيلا سكنية",
                CaseStatus.AwaitingData, "sara", 1_875_000.00m, 1_740_000.00m, 115_000.00m, 20_000.00m, 128_000.00m, 8, "2026-01-01", "2026-09-17", "MF-47-3342330", 16_000.00m,
                "2026-10-01", "2026-09-17T12:00:00"),
            new CaseSpec("RH-2026-003870", "alufuq", "ماجد تركي الشمري", "1032213870", "0551123870", "المدينة", "حي قباء", "شقة سكنية",
                CaseStatus.ActiveSettlement, "sara", 612_900.00m, 580_000.00m, 32_900.00m, 0.00m, 0.00m, 0, "2025-11-01", "2026-03-02", "MF-38-8870012", 9_800.00m,
                null, "2026-07-01T10:00:00"),
            new CaseSpec("RH-2026-003702", "alufuq", "تركي بندر العسيري", "1021103702", "0550013702", "أبها", "حي المنسك", "فيلا سكنية",
                CaseStatus.AwaitingReconciliation, "sara", 455_210.75m, 430_000.00m, 25_210.75m, 0.00m, 0.00m, 0, "2025-08-01", "2025-12-10", "MF-29-1103702", 8_400.00m,
                "2026-09-22", "2026-09-14T10:00:00"),
            new CaseSpec("RH-2026-003655", "alufuq", "ياسر كمال الغامدي", "1019993655", "0559993655", "جدة", "حي السلامة", "شقة سكنية",
                CaseStatus.Paused, "sara", 830_600.00m, 780_000.00m, 42_600.00m, 8_000.00m, 70_000.00m, 7, "2026-02-01", "2026-05-20", "MF-24-9936550", 10_000.00m,
                null, "2026-08-30T10:00:00"),
            new CaseSpec("RH-2026-003511", "alufuq", "فيصل راشد البلوي", "1018883511", "0558883511", "جدة", "حي الصفا", "فيلا سكنية",
                CaseStatus.JudicialReferral, "sara", 684_200.00m, 640_000.00m, 36_200.00m, 8_000.00m, 120_000.00m, 12, "2025-06-01", "2025-11-03", "MF-21-7735110", 10_000.00m,
                null, "2026-09-01T10:00:00"),
        };
        foreach (var s in specs)
        {
            var x = await BuildCaseAsync(s);
            _cases[x.Reference] = x;
            await AddDocumentAsync(x, "title_deed", "صك الملكية", DocumentSource.Lender, [("deed.pdf", "sara", s.OpenedOn + "T10:00:00", x.Status >= CaseStatus.Valuation ? ReviewStatus.Verified : ReviewStatus.Pending, null, null)]);
            await AddDocumentAsync(x, "financing_contract", "عقد التمويل", DocumentSource.CoreSystem, [("contract.pdf", "core", s.OpenedOn + "T10:05:00", x.Status >= CaseStatus.Valuation ? ReviewStatus.Verified : ReviewStatus.Pending, null, null)]);
        }

        // 004090: pending approval assigned to نورة الشهري, due today (list shows «بانتظار قرار نورة الشهري»).
        var c4090 = _cases["RH-2026-004090"];
        var v = new SolutionVersion
        {
            OrganizationId = c4090.OrganizationId, CaseId = c4090.Id, VersionNo = 1, Kind = SolutionKind.Reschedule, Status = SolutionStatus.PendingApproval,
            OutstandingAtPreparation = 1_120_450.00m, TermMonths = 96, FirstDueDate = new DateOnly(2026, 11, 1), WaiverAmount = 12_000m,
            PreparedByUserId = UserId("fahad"), PreparedAt = At("2026-09-17T15:00:00"), ReviewedByUserId = UserId("sara"), LockedAt = At("2026-09-18T09:00:00"),
            Justification = "تمديد المدة لخفض القسط ضمن القدرة المتحققة.", NetIncomeUsed = 26_000m,
        };
        var fig = SolutionCalculator.Compute(new SolutionInput(v.Kind, v.OutstandingAtPreparation, v.TermMonths, v.FirstDueDate, v.WaiverAmount, 0, 0, 26_000m, 0.55m));
        Apply(v, fig);
        db.Solutions.Add(v);
        db.ApprovalRequests.Add(new ApprovalRequest
        {
            OrganizationId = c4090.OrganizationId, CaseId = c4090.Id, Subject = ApprovalSubject.Solution, SubjectId = v.Id, SubjectVersionNo = 1,
            Title = "اعتماد الحل v1", PreparedByUserId = UserId("fahad"), SubmittedByUserId = UserId("sara"), SubmittedAt = At("2026-09-18T09:00:00"),
            SubmitterNote = "الحل ضمن القدرة المتحققة والتنازل محصور في غرامات التأخير.", SubmitterAttested = true,
            AssignedApproverUserId = UserId("noura"), RequiredTier = "approver", Amount = fig.RescheduledAmount, WaiverPercent = fig.WaiverPercent,
            DueOn = new DateOnly(2026, 9, 23), Evidence = ["الحل v1 (نسخة مقفلة)", "تحليل القدرة على السداد", "تقرير التقييم v1"],
        });

        // Tasks for سارة (7 open, «بحاجة لإجرائك»).
        var org = _orgs["alufuq"].Id;
        (string title, string caseRef, string due, string kind)[] tasks =
        [
            ("إرسال الحل v2 للموافقة الداخلية", "RH-2026-004172", "2026-09-25", "solution_review"),
            ("متابعة رد المالكة على العرض", "RH-2026-003988", "2026-09-20", "follow_up"),
            ("مراجعة رد على العرض المقابل", "RH-2026-004012", "2026-09-25", "negotiation"),
            ("مراجعة السجل التجاري المرفوع", "RH-2026-004201", "2026-09-29", "document_review"),
            ("تجديد صورة الهوية قبل انتهائها", "RH-2026-004172", "2026-10-05", "document_request"),
            ("استكمال بيانات العقار", "RH-2026-004233", "2026-10-01", "data"),
            ("مراجعة جدولة الأقساط المطابقة", "RH-2026-003870", "2026-09-30", "payments"),
        ];
        foreach (var t in tasks)
            db.Tasks.Add(new CaseTask
            {
                OrganizationId = org, CaseId = _cases[t.caseRef].Id, Title = t.title, AssigneeUserId = UserId("sara"),
                DueOn = D(t.due), Kind = t.kind, CreatedByUserId = UserId("sara"), Link = $"/cases/{t.caseRef}",
            });
        db.Tasks.Add(new CaseTask
        {
            OrganizationId = org, CaseId = c.Id, Title = "جدولة مكالمة مع المالك", AssigneeUserId = UserId("khaled"),
            DueOn = D("2026-09-27"), Kind = "call", CreatedByUserId = UserId("sara"), Link = $"/cases/{c.Reference}",
        });
        await db.SaveChangesAsync();
    }

    private static void Apply(SolutionVersion v, SolutionFigures f)
    {
        v.RescheduledAmount = f.RescheduledAmount;
        v.InstallmentAmount = f.InstallmentAmount;
        v.FinalInstallmentAmount = f.FinalInstallmentAmount;
        v.LastDueDate = f.LastDueDate;
        v.WaiverPercent = f.WaiverPercent;
        v.Dsr = f.Dsr;
    }

    private async Task SeedAnchorCaseDetailsAsync(Case c)
    {
        var org = c.OrganizationId;
        c.StageDueOn = new DateOnly(2026, 9, 25);

        // Documents (9): eight verified + the national ID expiring in 12 days.
        await AddDocumentAsync(c, "title_deed", "صك الملكية", DocumentSource.Lender, [("deed-3xxxxx18.pdf", "sara", "2026-08-15T10:00:00", ReviewStatus.Verified, "طابق الصك بيانات العقار.", null)], visibleTo: ["case_team", "legal"]);
        await AddDocumentAsync(c, "national_id", "صورة الهوية الوطنية", DocumentSource.Owner, [("id.jpg.pdf", "owner", "2026-08-20T19:40:00", ReviewStatus.Verified, null, null)],
            validUntil: new DateOnly(2026, 10, 5), visibleToOwner: true);
        await AddDocumentAsync(c, "financing_contract", "عقد التمويل", DocumentSource.CoreSystem, [("MF-88-3317.pdf", "core", "2026-08-14T09:10:00", ReviewStatus.Verified, null, null)], visibleTo: ["case_team", "legal"]);
        await AddDocumentAsync(c, "salary_statement", "كشف الراتب لآخر 3 أشهر", DocumentSource.Owner,
        [
            ("salary-v1.pdf", "owner", "2026-09-02T20:10:00", ReviewStatus.Rejected, "الصفحة الثانية غير مقروءة.", "الصورة غير واضحة في الصفحة الثانية. نحتاج نسخة أوضح."),
            ("salary-v2.pdf", "owner", "2026-09-19T21:00:00", ReviewStatus.Verified, "متحقق 2026-09-20.", null),
        ], validUntil: new DateOnly(2026, 12, 18), visibleToOwner: true, visibleTo: ["case_team", "approver"]);
        await AddDocumentAsync(c, "valuation_report", "تقرير التقييم", DocumentSource.Provider, [("valuation-ASG-2026-0418.pdf", "valuer", "2026-09-10T13:44:00", ReviewStatus.Verified, null, null)],
            validUntil: new DateOnly(2026, 12, 9), visibleTo: ["case_team", "approver", "owner"]);
        await AddDocumentAsync(c, "property_photos", "صور العقار", DocumentSource.Provider, [("photos.pdf", "valuer", "2026-09-10T13:45:00", ReviewStatus.Verified, null, null)]);
        await AddDocumentAsync(c, "bank_statement", "كشف حساب بنكي", DocumentSource.Owner, [("bank.pdf", "owner", "2026-09-19T21:05:00", ReviewStatus.Verified, null, null)], visibleToOwner: true, visibleTo: ["case_team", "approver"]);
        await AddDocumentAsync(c, "hardship_evidence", "مستند إثبات الظرف", DocumentSource.Owner, [("hardship.pdf", "owner", "2026-08-22T18:00:00", ReviewStatus.Verified, "انخفاض الدخل بعد تغيير جهة العمل.", null)], visibleToOwner: true);
        await AddDocumentAsync(c, "poa", "إقرار المالك بالتواصل عبر المنصة", DocumentSource.Owner, [("consent-contact.pdf", "owner", "2026-08-20T19:45:00", ReviewStatus.Verified, null, null)], visibleToOwner: true);

        // Valuation via provider assignment (access expired 7 days after delivery).
        var asg = new ProviderAssignment
        {
            OrganizationId = org, Reference = "ASG-2026-0418", CaseId = c.Id, ProviderOrganizationId = _orgs["valuer-b"].Id, AssigneeUserId = UserId("omar"),
            Type = AssignmentType.Valuation, Title = "تقييم فيلا سكنية", PropertyLabel = "فيلا سكنية، حي النرجس، الرياض",
            Status = AssignmentStatus.Accepted, DueOn = new DateOnly(2026, 9, 12), InspectionAt = At("2026-09-06T10:00:00"), InspectionConfirmed = true,
            InspectionContact = "المالك", Scope = ["تقييم القيمة السوقية", "معاينة داخلية وخارجية", "صور العقار"], FeesLabel = "3,500.00 ر.س", FeeAmount = 3_500m,
            CreatedByUserId = UserId("fahad"), DeliveredAt = At("2026-09-10T13:44:00"), AccessExpiresAt = At("2026-09-17T13:44:00"),
            CreatedAt = At("2026-08-30T10:00:00"),
        };
        db.Assignments.Add(asg);
        db.ValuationReports.Add(new ValuationReport
        {
            OrganizationId = org, CaseId = c.Id, AssignmentId = asg.Id, VersionNo = 1, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = 1_650_000.00m,
            RangeLow = 1_590_000m, RangeHigh = 1_710_000m, Methodology = "أسلوب المقارنة بالمبيعات", ComparablesCount = 6,
            InspectionDate = new DateOnly(2026, 9, 6), ReportDate = new DateOnly(2026, 9, 10), ValidUntil = new DateOnly(2026, 12, 9),
            Status = ValuationStatus.Accepted, ReviewedByUserId = UserId("fahad"), ReviewedAt = At("2026-09-10T16:00:00"),
        });

        db.Analyses.Add(new AffordabilityAnalysis
        {
            OrganizationId = org, CaseId = c.Id, NetMonthlyIncome = 32_400.00m, IncomeSourceLabel = "كشف الراتب v2", IncomeVerifiedOn = new DateOnly(2026, 9, 20),
            OtherObligations = 0, DsrLimit = 0.55m, Completed = true, PreparedByUserId = UserId("fahad"),
            CircumstanceIndicators = ["انخفاض الدخل بعد تغيير جهة العمل (مستند إثبات الظرف)", "الأسرة تسكن العقار", "لا التزامات تمويلية أخرى"],
            OptionNotes = ["إعادة الجدولة ممكنة ضمن حد الاستقطاع عند 84 شهراً", "البيع الطوعي غير مطروح — المالك يرغب في الاحتفاظ بالعقار"],
        });

        // Solutions v1 (returned) and v2 (handed to case manager for review).
        var v1 = new SolutionVersion
        {
            OrganizationId = org, CaseId = c.Id, VersionNo = 1, Kind = SolutionKind.Reschedule, Status = SolutionStatus.Returned,
            OutstandingAtPreparation = 1_284_560.00m, TermMonths = 60, FirstDueDate = new DateOnly(2026, 11, 1), WaiverAmount = 0,
            PreparedByUserId = UserId("fahad"), PreparedAt = At("2026-09-12T11:00:00"), NetIncomeUsed = 32_400m,
            Justification = "إعادة جدولة على 60 شهراً دون تنازل.", ReturnReason = "القسط يتجاوز القدرة وفق كشف الراتب v2",
            ReviewedByUserId = UserId("sara"),
        };
        Apply(v1, SolutionCalculator.Compute(new SolutionInput(v1.Kind, v1.OutstandingAtPreparation, 60, v1.FirstDueDate, 0, 0, 0, 32_400m, 0.55m)));
        var v2 = new SolutionVersion
        {
            OrganizationId = org, CaseId = c.Id, VersionNo = 2, Kind = SolutionKind.Reschedule, Status = SolutionStatus.InReview,
            OutstandingAtPreparation = 1_284_560.00m, TermMonths = 84, FirstDueDate = new DateOnly(2026, 11, 1), WaiverAmount = 18_300.00m,
            PreparedByUserId = UserId("fahad"), PreparedAt = At("2026-09-22T16:05:00"), NetIncomeUsed = 32_400m, BasedOnVersionId = v1.Id,
            Justification = "تمديد المدة إلى 84 شهراً يخفض القسط إلى ما دون حد الاستقطاع وفق كشف الراتب v2. التنازل محصور في غرامات التأخير دون المساس بالأصل.",
        };
        Apply(v2, SolutionCalculator.Compute(new SolutionInput(v2.Kind, v2.OutstandingAtPreparation, 84, v2.FirstDueDate, 18_300m, 0, 0, 32_400m, 0.55m)));
        db.Solutions.AddRange(v1, v2);

        // History (C10 / workspace activity).
        Audit(org, c, "owner.invitation_accepted", "قبول الدعوة والتحقق من هوية المالك", At("2026-08-20T19:31:00"), "owner");
        Audit(org, c, "document.uploaded", "رفع صورة الهوية الوطنية v1", At("2026-08-20T19:40:00"), "owner");
        Audit(org, c, "case.transition", "بانتظار البيانات ← تحقق", At("2026-08-16T10:00:00"), "sara", "awaiting_data", "verification", "اكتملت البيانات الإلزامية");
        Audit(org, c, "document.verified", "تم التحقق من صك الملكية", At("2026-08-29T11:00:00"), "majed", detail: "طابق الصك بيانات العقار.");
        Audit(org, c, "case.transition", "تحقق ← تقييم", At("2026-08-29T12:00:00"), "sara", "verification", "valuation", "صك وهوية وعقد متحقق منها");
        Audit(org, c, "pii.reveal", "كشف رقم الهوية كاملاً", At("2026-09-02T11:20:00"), "sara", reason: "مطابقة مع الصك", detail: "مدة الكشف 60 ثانية");
        Audit(org, c, "document.rejected", "رفض كشف الراتب v1", At("2026-09-03T10:00:00"), "sara", detail: "الصفحة الثانية غير مقروءة.");
        Audit(org, c, "valuation.received", "استلام تقرير التقييم v1", At("2026-09-10T13:44:00"), "valuer", detail: "القيمة السوقية 1,650,000.00 ر.س · أُغلق وصول المقدم بعد 7 أيام");
        Audit(org, c, "case.transition", "تقييم ← حل مقترح", At("2026-09-10T16:05:00"), "fahad", "valuation", "proposed_solution", "تقرير تقييم صالح ومراجعة قانونية مكتملة",
            evidence: ["valuation:v1"]);
        Audit(org, c, "solution.returned", "إعادة الحل v1 للتعديل", At("2026-09-15T12:00:00"), "sara", reason: "القسط يتجاوز القدرة وفق كشف الراتب v2");
        Audit(org, c, "case.transition_blocked", "محاولة انتقال محجوبة: حل مقترح ← موافقة داخلية", At("2026-09-18T09:30:00"), "fahad",
            "proposed_solution", "internal_approval", blocked: true, detail: "المانع: كشف الراتب v1 مرفوض. لم يُنفذ الانتقال.");
        Audit(org, c, "document.verified", "قبول كشف الراتب v2", At("2026-09-20T10:00:00"), "sara");
        Audit(org, c, "solution.created", "إنشاء الحل v2", At("2026-09-22T16:05:00"), "fahad", detail: "تغييرات: المدة، القسط، التنازل (3)");
        Audit(org, c, "solution.handed_for_review", "تسليم الحل v2 لمديرة الحالة للمراجعة", At("2026-09-22T16:06:00"), "fahad");

        db.Messages.Add(new CaseMessage
        {
            OrganizationId = org, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "lender", AuthorUserId = UserId("khaled"), AuthorLabel = "خالد الزهراني",
            Body = "مرحباً أ. عبدالله، استلمنا كشف الراتب المحدّث وشكراً لك. نعمل على إعداد خيار مناسب وسنعرضه عليك قريباً.", At = At("2026-09-20T11:00:00"),
        });
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = org, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "owner", AuthorLabel = "عبدالله م.",
            Body = "شكراً لكم. أفضّل أن يكون موعد القسط بعد العاشر من كل شهر لأن راتبي ينزل في التاسع.", At = At("2026-09-20T19:12:00"),
        });
    }
}
