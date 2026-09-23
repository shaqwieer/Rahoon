using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// B7 demo data: provider assignments for «مكتب تقييم معتمد «ب»» (عمر العنزي), institution admin
/// configuration in flight (pending invitation, approval-limit v5, template in compliance review) and
/// platform administration (applications, temporary access, defaults). All fictional.
/// </summary>
public sealed partial class DevSeeder
{
    /// <summary>Dev-only deterministic staff invitation link: /invite/staff-demo-badr (see docs/progress/backend-B7.md).</summary>
    public const string DemoStaffInviteToken = "staff-demo-badr";

    private async Task SeedProviderAdminAsync()
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO cases.reference_counters (key, value) VALUES ('asg:2026', 880), ('app:2026', 30) ON CONFLICT (key) DO NOTHING");
        await SeedProviderAssignmentsAsync();
        await SeedInstitutionAdminAsync();
        await SeedPlatformAdminAsync();
        await db.SaveChangesAsync();
    }

    // ───────── V01–V04 ─────────

    private async Task SeedProviderAssignmentsAsync()
    {
        db.DocumentTypes.Add(new DocumentType { Key = "building_plan", NameAr = "مخطط البناء", Icon = "architecture" });
        db.DocumentTypes.Add(new DocumentType { Key = "report_template", NameAr = "نموذج التقرير المطلوب", Icon = "assignment" });

        var omar = UserId("omar");
        var valuer = _orgs["valuer-b"].Id;

        // ASG-2026-0871 — in progress, inspection confirmed tomorrow (V02 sample).
        var c4155 = _cases["RH-2026-004155"];
        var deed4155 = await db.Documents.FirstAsync(d => d.CaseId == c4155.Id && d.DocumentTypeKey == "title_deed");
        var plan = await AddDocumentAsync(c4155, "building_plan", "مخطط البناء", DocumentSource.Lender, [("plan.pdf", "fahad", "2026-09-21T13:00:00", ReviewStatus.Verified, null, null)]);
        var template = await AddDocumentAsync(c4155, "report_template", "نموذج التقرير المطلوب", DocumentSource.Lender, [("report-template.pdf", "fahad", "2026-09-19T10:00:00", ReviewStatus.Verified, null, null)]);
        var a0871 = new ProviderAssignment
        {
            OrganizationId = c4155.OrganizationId, Reference = "ASG-2026-0871", CaseId = c4155.Id, ProviderOrganizationId = valuer, AssigneeUserId = omar,
            Type = AssignmentType.Valuation, Title = "تقييم دور سكني", PropertyLabel = "حي العارض، الرياض", Status = AssignmentStatus.InProgress,
            DueOn = new DateOnly(2026, 9, 26), InspectionAt = At("2026-09-24T10:00:00"), InspectionConfirmed = true, InspectionContact = "سلطان ح. (المالك) عبر المنصة",
            Scope = ["نوع التقييم — قيمة سوقية · للعقار السكني", "المنهجية المطلوبة — المقارنة + التكلفة"], FeesLabel = "حسب الاتفاقية الإطارية",
            SharedDocumentIds = [deed4155.Id, plan.Id, template.Id], CreatedByUserId = UserId("fahad"), CreatedAt = At("2026-09-19T10:00:00"),
        };
        db.Assignments.Add(a0871);
        db.AssignmentMessages.Add(new AssignmentMessage
        {
            OrganizationId = a0871.OrganizationId, AssignmentId = a0871.Id, AuthorUserId = omar, AuthorLabel = "عمر العنزي · مكتب تقييم معتمد «ب»", AuthorSide = "provider",
            Body = "هل يتوفر مخطط البناء المعتمد؟ نحتاجه لمطابقة المساحة.", At = At("2026-09-21T10:30:00"),
        });
        db.AssignmentMessages.Add(new AssignmentMessage
        {
            OrganizationId = a0871.OrganizationId, AssignmentId = a0871.Id, AuthorUserId = UserId("fahad"), AuthorLabel = "فهد العتيبي · مصرف الأفق", AuthorSide = "lender",
            Body = "أُضيف المخطط إلى مستندات التكليف.", At = At("2026-09-21T13:05:00"),
        });
        Audit(c4155.OrganizationId, c4155, "assignment.created", "تكليف مكتب تقييم معتمد «ب» · ASG-2026-0871", At("2026-09-19T10:00:00"), "fahad",
            detail: "تقييم · التسليم 2026-09-26 · مستندات مشاركة 3");

        // ASG-2026-0864 — v1 returned with itemised notes (V04 returned state).
        var c3988 = _cases["RH-2026-003988"];
        var deed3988 = await db.Documents.FirstAsync(d => d.CaseId == c3988.Id && d.DocumentTypeKey == "title_deed");
        var report = await AddDocumentAsync(c3988, "valuation_report", "تقرير التقييم — ASG-2026-0864", DocumentSource.Provider,
            [("تقرير_التقييم_v1.pdf", "omar", "2026-09-18T16:00:00", ReviewStatus.Rejected, "صفقات المقارنة أقدم من 12 شهراً (2 من 5). · ينقص ذكر حالة الإشغال.", null)],
            visibleTo: ["case_team", "approver"]);
        var a0864 = new ProviderAssignment
        {
            OrganizationId = c3988.OrganizationId, Reference = "ASG-2026-0864", CaseId = c3988.Id, ProviderOrganizationId = valuer, AssigneeUserId = omar,
            Type = AssignmentType.Valuation, Title = "تقييم شقة سكنية", PropertyLabel = "حي الروضة، جدة", Status = AssignmentStatus.Returned,
            DueOn = new DateOnly(2026, 9, 20), InspectionAt = At("2026-09-15T11:00:00"), InspectionConfirmed = true, InspectionContact = "منيرة ع. (المالكة) عبر المنصة",
            Scope = ["نوع التقييم — إعادة تقييم · قيمة سوقية", "المنهجية المطلوبة — المقارنة"], FeesLabel = "حسب الاتفاقية الإطارية",
            SharedDocumentIds = [deed3988.Id], CreatedByUserId = UserId("fahad"), CreatedAt = At("2026-09-10T09:00:00"),
        };
        db.Assignments.Add(a0864);
        db.AssignmentSubmissions.Add(new AssignmentSubmission
        {
            OrganizationId = a0864.OrganizationId, AssignmentId = a0864.Id, VersionNo = 1, MarketValue = 742_000.00m, RangeLow = 720_000m, RangeHigh = 765_000m,
            InspectionDate = new DateOnly(2026, 9, 15), Methodology = "أسلوب المقارنة بالمبيعات", ComparablesCount = 5, ReportDocumentVersionId = report.CurrentVersionId,
            Checklist = ["comparables_12m", "photos_no_faces", "independence"], IndependenceDeclared = true, Status = SubmissionStatus.Returned,
            SubmittedAt = At("2026-09-18T16:00:00"), SubmittedByUserId = omar,
            ReturnNotes = ["صفقات المقارنة أقدم من 12 شهراً (2 من 5). يرجى تحديثها.", "ينقص ذكر حالة الإشغال."],
            ReviewedByUserId = UserId("fahad"), ReviewedAt = At("2026-09-22T12:00:00"), ResubmitDueOn = new DateOnly(2026, 9, 27),
        });
        Audit(c3988.OrganizationId, c3988, "valuation.returned", "إعادة تقرير ASG-2026-0864 v1 للتعديل", At("2026-09-22T12:00:00"), "fahad",
            reason: "صفقات المقارنة أقدم من 12 شهراً (2 من 5). · ينقص ذكر حالة الإشغال.", detail: "مهلة إعادة التسليم 2026-09-27");

        // ASG-2026-0880 — new, from the second lender (the inbox is cross-lender, each assignment isolated).
        var c5101 = _cases["RH-2026-005101"];
        var deed5101 = await db.Documents.Where(d => d.CaseId == c5101.Id && d.DocumentTypeKey == "title_deed").Select(d => (Guid?)d.Id).FirstOrDefaultAsync();
        db.Assignments.Add(new ProviderAssignment
        {
            OrganizationId = c5101.OrganizationId, Reference = "ASG-2026-0880", CaseId = c5101.Id, ProviderOrganizationId = valuer,
            Type = AssignmentType.Valuation, Title = "تقييم فيلا سكنية", PropertyLabel = "حي أبحر، جدة", Status = AssignmentStatus.New,
            DueOn = new DateOnly(2026, 10, 3), InspectionContact = "مشعل ع. (المالك) عبر المنصة",
            Scope = ["نوع التقييم — قيمة سوقية · للعقار السكني", "المنهجية المطلوبة — المقارنة + التكلفة"], FeesLabel = "3,200.00 ر.س", FeeAmount = 3_200m,
            SharedDocumentIds = deed5101 is { } d5 ? [d5] : [], CreatedByUserId = UserId("maha"), CreatedAt = At("2026-09-23T09:00:00"),
        });
    }

    // ───────── A01–A07 ─────────

    private async Task SeedInstitutionAdminAsync()
    {
        var alufuq = _orgs["alufuq"];

        // Second institution admin so maker-checker (role changes, limits) has a distinct approver (B7 C5).
        User("saud", "s.alrashed@alufuq.example", "سعود الراشد", "0550000113", (alufuq, SystemRoles.OrgAdmin, "المدير التنفيذي للمخاطر"));
        await db.SaveChangesAsync();

        // بدر السالم: invited, not yet accepted — no password, MFA not enrolled; deterministic dev link.
        var badr = _users["badr"];
        badr.PasswordHash = null;
        badr.MfaEnrolled = false;
        var badrMembership = await db.Memberships.IgnoreQueryFilters().Include(m => m.Roles).FirstAsync(m => m.UserId == badr.Id && m.OrganizationId == alufuq.Id);
        db.Invitations.Add(new Invitation
        {
            OrganizationId = alufuq.Id, Email = badr.Email, FullName = badr.FullName, Phone = badr.Phone, RoleId = badrMembership.Roles[0].RoleId, TeamId = badrMembership.TeamId,
            TokenHash = Tokens.Sha256(DemoStaffInviteToken), InvitedByUserId = UserId("layla"), CreatedAt = DemoToday.AddDays(-0.5), ExpiresAt = At("2026-09-30T23:59:00"),
        });

        // A04: v5 proposal pending a second admin.
        var policy = new ApprovalLimitPolicy
        {
            OrganizationId = alufuq.Id, VersionNo = 5, Status = "pending_approval", EffectiveFrom = new DateOnly(2026, 10, 1),
            ChangeSummary = "رفع حد «المعتمد» من 2,000,000 إلى 2,500,000 ر.س.", ProposedByUserId = UserId("layla"), CreatedAt = At("2026-09-23T09:42:00"),
        };
        policy.Tiers.AddRange(
        [
            new() { Rank = 0, RoleKey = SystemRoles.CreditAnalyst, LevelLabel = "محلل / مدير حالات", SolutionKinds = ["Reschedule"], SolutionKindsLabel = "إعادة جدولة", CanApprove = false, EscalateToLabel = "إعداد فقط، لا اعتماد" },
            new() { Rank = 1, RoleKey = SystemRoles.Approver, LevelLabel = "معتمد", SolutionKinds = ["Reschedule", "GracePeriod"], SolutionKindsLabel = "إعادة جدولة، سماح", MaxAmount = 2_500_000m, MaxWaiverPercent = 0.05m, EscalateToLabel = "معتمد أعلى" },
            new() { Rank = 2, RoleKey = SystemRoles.SeniorApprover, LevelLabel = "معتمد أول", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", MaxAmount = 5_000_000m, MaxWaiverPercent = 0.10m, EscalateToLabel = "لجنة المخاطر" },
            new() { Rank = 3, RoleKey = SystemRoles.RiskCommittee, LevelLabel = "لجنة المخاطر", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", EscalateToLabel = "—" },
        ]);
        db.ApprovalLimitPolicies.Add(policy);
        Audit(alufuq.Id, null, "limits.proposed", "تعديل حد موافقة (مقترح)", At("2026-09-23T09:42:00"), "layla", detail: "v5 بانتظار الموافقة");

        // A05: institution copy of a base template waiting for compliance.
        db.Templates.Add(new CommunicationTemplate
        {
            OrganizationId = alufuq.Id, Code = "TPL-DOCREQ-01", Title = "طلب مستند", Audience = "owner", VersionNo = 3, Status = TemplateStatus.PendingCompliance,
            BodyAr = "نحتاج منك {المستند} لمتابعة حالتك. يمكنك رفعه من صفحة المستندات حتى {المهلة}، وإن كان لديك سؤال فاكتب لنا من صفحة الحالة.",
            BodyEn = "We need your {document} to continue with your case. You can upload it from the documents page until {deadline}. If you have a question, write to us from your case page.",
            Variables = ["{المستند}", "{المهلة}"], LastEditedByUserId = UserId("layla"), UpdatedAt = At("2026-09-22T14:00:00"),
        });
    }

    // ───────── PA01–PA13 ─────────

    private async Task SeedPlatformAdminAsync()
    {
        (string key, string name, string min, string note, decimal? value, bool editable)[] rules =
        [
            (PlatformRuleKeys.SeparationOfDuties, "فصل المُعِدّ والمعتمد", "إلزامي", "لا يمكن للمنشأة تعطيله", null, false),
            (PlatformRuleKeys.ReferralApprovals, "الإحالة القضائية", "موافقتان + القانونية", "يمكن إضافة مستوى", 2m, false),
            (PlatformRuleKeys.ClosureApprovals, "الإغلاق", "المالية + معتمد", "يمكن إضافة مستوى", 2m, false),
            (PlatformRuleKeys.MaxWaiverWithoutCommittee, "حد التنازل الأقصى بلا لجنة", "10%", "المنشأة تحدد أقل", 0.10m, true),
            (PlatformRuleKeys.ValuationMaxValidityDays, "صلاحية تقرير التقييم", "≤ 90 يوماً", "المنشأة تحدد أقل", 90m, true),
            (PlatformRuleKeys.OwnerResponseMinDays, "مهلة رد المالك الدنيا", "7 أيام", "لا تقل عن ذلك (افتراض)", 7m, true),
        ];
        var order = 0;
        foreach (var r in rules)
            db.Set<PlatformDefaultRule>().Add(new PlatformDefaultRule
            {
                Key = r.key, NameAr = r.name, MinimumLabel = r.min, Note = r.note, Value = r.value, Editable = r.editable, SortOrder = order++, UpdatedAt = DemoToday.AddDays(-90),
            });

        // PA12: complete the retention table; all durations remain drafts pending legal confirmation (A-12).
        var existing = await db.RetentionPolicies.ToListAsync();
        foreach (var p in existing)
            p.AfterAction = p.DataCategory switch
            {
                "ملف الحالة والمستندات" => "أرشفة مشفرة ثم حذف",
                "سجل التدقيق" => "لا تُحذف قبل المدة",
                "وصول مقدم الخدمة" => "سحب آلي",
                _ => "حذف آمن مع سجل",
            };
        (string cat, string period, string after)[] more =
        [
            ("مستندات الهوية", "حتى الإغلاق + 5 سنوات (افتراض)", "حذف آمن مع سجل"),
            ("رسائل التواصل", "حتى الإغلاق + 5 سنوات (افتراض)", "حذف"),
            ("وصول المالك بعد الإغلاق", "90 يوماً (افتراض)", "سحب الوصول وإبقاء النسخ"),
        ];
        foreach (var m in more)
            db.RetentionPolicies.Add(new RetentionPolicy { DataCategory = m.cat, Period = m.period, Basis = "افتراض — يتطلب تأكيداً قانونياً", State = "draft", AfterAction = m.after });

        // PA02: applications at each stage; the next public request gets APP-2026-0031.
        (string reference, string name, string type, string contact, string email, ApplicationStatus status, string verification, string created)[] apps =
        [
            ("APP-2026-0017", "مصرف الواحة", "بنك", "طارق البقمي", "t.albuqami@alwaha.example", ApplicationStatus.Approved, "مكتمل", "2026-08-20T10:00:00"),
            ("APP-2026-0019", "شركة المدى للتمويل العقاري", "تمويل عقاري", "نجلاء السديري", "n.alsudairi@almada-finance.example", ApplicationStatus.InReview, "مستندات الترخيص مرفوعة", "2026-09-08T11:00:00"),
            ("APP-2026-0021", "بنك الساحل", "بنك", "حمد الكبيسي", "h.alkubaisi@sahil-bank.example", ApplicationStatus.MoreInfoRequested, "ينقص خطاب التفويض", "2026-09-14T09:30:00"),
            ("APP-2026-0022", "شركة ركيزة للتمويل", "تمويل", "لمى الخالدي", "l.alkhaldi@rakeeza.example", ApplicationStatus.New, "جديد", "2026-09-21T15:10:00"),
        ];
        foreach (var a in apps)
            db.InstitutionApplications.Add(new InstitutionApplication
            {
                Reference = a.reference, OrgName = a.name, OrgType = a.type, ContactName = a.contact, ContactEmail = a.email, JobTitle = "مدير التحصيل",
                PortfolioSize = "500 – 2,000 حالة", Status = a.status, VerificationNote = a.verification, CreatedAt = At(a.created), ConsentAt = At(a.created),
                ReviewedByUserId = a.status == ApplicationStatus.New ? null : UserId("hessa"),
                CreatedOrganizationId = a.status == ApplicationStatus.Approved ? _orgs["alwaha"].Id : null,
                DecidedAt = a.status == ApplicationStatus.Approved ? At("2026-09-01T12:00:00") : null,
            });

        // PA06: one pending temporary-access request awaiting both approvals.
        var c3988 = _cases["RH-2026-003988"];
        db.TempAccessRequests.Add(new TempAccessRequest
        {
            OrganizationId = c3988.OrganizationId, CaseId = c3988.Id, MaskedCaseId = Mask.OpaqueCaseId(c3988.Id, PlatformMasking.Salt(config)),
            RequesterUserId = UserId("rana"), Reason = "SUP-2026-1201 — قالب رسالة الرفض لا يعرض السبب.", SupportTicketRef = "SUP-2026-1201",
            DurationMinutes = 120, Status = TempAccessStatus.Pending, CreatedAt = At("2026-09-23T10:40:00"),
        });

        // PA09: base templates beyond the three used by the lender flows.
        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-VISIT-01", Title = "تأكيد موعد زيارة", Audience = "owner", VersionNo = 1, Status = TemplateStatus.Published,
            BodyAr = "نؤكد موعد الزيارة يوم {الموعد}. إن لم يناسبك الموعد يمكنك طلب تغييره حتى {المهلة} أو الكتابة لنا.",
            BodySms = "رهون: موعد الزيارة {الموعد}. لتغييره ادخل من رابط الحالة.", Variables = ["{الموعد}", "{المهلة}"], UpdatedAt = DemoToday.AddDays(-40),
        });
        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-BREACH-01", Title = "مساعدة في الأقساط", Audience = "owner", VersionNo = 2, Status = TemplateStatus.Published,
            BodyAr = "لاحظنا تأخر قسطين. لديك حتى {المهلة} للتصحيح، ويمكنك طلب مكالمة لنبحث معك عن حل.",
            Variables = ["{المهلة}"], UpdatedAt = DemoToday.AddDays(-20),
        });
        Audit(_orgs["platform"].Id, null, "template.published", "نشر قالب", At("2026-09-22T17:05:00"), "hessa", detail: "TPL-REJECT-02 v4");
    }
}
