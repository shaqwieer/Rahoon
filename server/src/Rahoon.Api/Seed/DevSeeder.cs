using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// Fictional demo data for local development (names, IDs, amounts are invented; see
/// docs/demo-roles.md). Refuses to run outside Development. Idempotent: skips if seeded.
/// </summary>
public sealed partial class DevSeeder(
    RahoonDbContext db,
    IPasswordHasher<User> hasher,
    CaseFactory factory,
    IDocumentStorage storage,
    IClock clock,
    IConfiguration config,
    IHostEnvironment env,
    ILogger<DevSeeder> log)
{
    private readonly Dictionary<string, Organization> _orgs = new();
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<(Guid Org, string Role), Role> _roles = new();
    private readonly List<Modules.Audit.AuditEvent> _audit = [];

    public static readonly DateTimeOffset DemoToday = new(2026, 9, 23, 7, 0, 0, TimeSpan.Zero);

    public async Task SeedAsync()
    {
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
            throw new InvalidOperationException("Demo seeding is only allowed in Development/Testing.");
        using var _ = db.Request.BeginSystemScope();
        if (await db.Organizations.AnyAsync()) { log.LogInformation("Seed skipped: data already present."); return; }

        await SeedPlatformCatalogAsync();
        SeedOrganizations();
        await db.SaveChangesAsync();
        SeedRoles();
        await db.SaveChangesAsync();
        SeedUsers();
        await db.SaveChangesAsync();
        SeedInstitutionConfig(_orgs["alufuq"]);
        SeedInstitutionConfig(_orgs["sunbula"]);
        await db.SaveChangesAsync();

        await SeedCanonicalCasesAsync();
        await SeedCaseTabsAsync();
        if (config.GetValue("Seed:Bulk", true)) await SeedBulkCasesAsync();
        else await db.Database.ExecuteSqlRawAsync("INSERT INTO cases.reference_counters (key, value) VALUES ('case:2026', 5200) ON CONFLICT (key) DO NOTHING");
        await SeedOtherTenantAsync();
        await SeedProviderAdminAsync();
        await FlushAuditAsync();
        log.LogInformation("Seed complete.");
    }

    private string DemoPassword => config["Seed:DemoPassword"] is { Length: >= 10 } p ? p : "Rahoon-Demo-2026!";

    // ───────── Platform catalog ─────────

    private async Task SeedPlatformCatalogAsync()
    {
        (string key, string ar, string icon, int? validity, bool sensitive)[] types =
        [
            ("title_deed", "صك الملكية", "description", null, false),
            ("national_id", "صورة الهوية الوطنية", "badge", null, true),
            ("financing_contract", "عقد التمويل", "contract", null, false),
            ("salary_statement", "كشف الراتب لآخر 3 أشهر", "request_quote", 90, true),
            ("bank_statement", "كشف حساب بنكي", "account_balance", 90, true),
            ("valuation_report", "تقرير التقييم", "analytics", 90, false),
            ("poa", "وكالة موثقة", "gavel", null, false),
            ("commercial_registration", "السجل التجاري", "store", 365, false),
            ("property_photos", "صور العقار", "photo_library", null, false),
            ("payment_proof", "إثبات السداد", "receipt_long", null, false),
            ("hardship_evidence", "مستند إثبات الظرف", "health_and_safety", null, true),
            ("signed_agreement", "الاتفاق الموقع", "handshake", null, false),
            ("sale_consent", "موافقة المالك على البيع", "sell", null, false),
            ("final_clearance", "المخالصة النهائية", "task", null, false),
            ("lien_release_letter", "خطاب فك الرهن", "lock_open", null, false),
            ("external_official_document", "مستند رسمي خارجي", "account_balance", null, false),
        ];
        foreach (var t in types)
            db.DocumentTypes.Add(new DocumentType { Key = t.key, NameAr = t.ar, Icon = t.icon, ValidityDays = t.validity, Sensitive = t.sensitive });

        (string key, string ar, string state, string note)[] integrations =
        [
            (IntegrationKeys.Sms, "بوابة الرسائل النصية", IntegrationState.Simulated, "بيئة تجريبية: تُسجَّل الرسائل ولا تُرسل. لا يوجد عقد مع مزوّد."),
            (IntegrationKeys.Email, "البريد الإلكتروني", IntegrationState.Simulated, "بيئة تجريبية: تُسجَّل الرسائل ولا تُرسل."),
            (IntegrationKeys.NationalIdentity, "مزوّد الهوية الوطنية الرقمية", IntegrationState.Unavailable, "نمط محجوز يتطلب تأكيد التكامل. التحقق الحالي: الدعوة + آخر 4 أرقام + رمز جوال."),
            (IntegrationKeys.LicensedSigning, "التوقيع الإلكتروني المرخّص", IntegrationState.Unavailable, "لا مزوّد مرخّص متعاقد. القبول داخل المنصة سجل موافقة وليس توقيعاً ملزماً."),
            (IntegrationKeys.LicensedPayment, "الدفع المرخّص", IntegrationState.Unavailable, "لا بوابة دفع. المدفوعات تُسجَّل يدوياً مع صانع ومدقق."),
            (IntegrationKeys.CoreBanking, "نظام التمويل الأساسي", IntegrationState.Unavailable, "لا اتصال مباشر. الأرقام تُدخل يدوياً أو باستيراد ملف مع ذكر المصدر والوقت."),
            (IntegrationKeys.JudicialChannel, "القناة القضائية الرسمية", IntegrationState.Unavailable, "لا قناة معتمدة. تُدخل الإحالة والمرجع والحالة الرسمية يدوياً وحرفياً."),
            (IntegrationKeys.RealEstateRegistry, "السجل العقاري", IntegrationState.Unavailable, "لا تكامل. مطابقة الصك يدوية من القانونية."),
        ];
        foreach (var i in integrations)
            db.IntegrationSettings.Add(new IntegrationSetting { Key = i.key, NameAr = i.ar, State = i.state, Note = i.note, UpdatedAt = DemoToday });

        (string cat, string period, string basis, string state)[] retention =
        [
            ("ملف الحالة والمستندات", "10 سنوات بعد الإغلاق", "افتراض — يتطلب تأكيداً قانونياً", "pending_legal"),
            ("سجل التدقيق", "10 سنوات", "افتراض — يتطلب تأكيداً قانونياً", "pending_legal"),
            ("وصول مقدم الخدمة", "التكليف + 7 أيام", "A-11", "effective"),
            ("سجلات الجلسات", "12 شهراً", "افتراض", "pending_legal"),
        ];
        foreach (var r in retention)
            db.RetentionPolicies.Add(new RetentionPolicy { DataCategory = r.cat, Period = r.period, Basis = r.basis, State = r.state });

        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-OFFER-01", Title = "عرض إعادة جدولة", Audience = "owner", VersionNo = 3, Status = TemplateStatus.Published,
            BodyAr = "أرسلنا لك عرضاً جديداً بقسط شهري {القسط} ريال لمدة {المدة}. يمكنك مراجعته والرد حتى {تاريخ_الصلاحية}، وإن كان لديك سؤال فاكتب لنا.",
            BodyEn = "We have sent you a new offer with a monthly installment of {installment} SAR for {term}. You can review and respond until {expiry}. If you have a question, write to us.",
            BodySms = "رهون: عرض جديد بانتظارك حتى {تاريخ_الصلاحية}. ادخل من رابط الدعوة للمراجعة.",
            Variables = ["{القسط}", "{المدة}", "{تاريخ_الصلاحية}", "{اسم_المسؤول}"], UpdatedAt = DemoToday.AddDays(-52),
        });
        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-DOCREQ-01", Title = "طلب مستند", Audience = "owner", VersionNo = 2, Status = TemplateStatus.Published,
            BodyAr = "نحتاج منك {المستند} لمتابعة حالتك. يمكنك رفعه من صفحة المستندات حتى {المهلة}، وإن واجهت صعوبة فاكتب لنا.",
            Variables = ["{المستند}", "{المهلة}"], UpdatedAt = DemoToday.AddDays(-80),
        });
        db.Templates.Add(new CommunicationTemplate
        {
            Code = "TPL-REJECT-02", Title = "إعادة طلب مستند", Audience = "owner", VersionNo = 4, Status = TemplateStatus.Published,
            BodyAr = "لم نتمكن من قبول {المستند}: {السبب}. يمكنك رفع نسخة جديدة حتى {المهلة}.",
            Variables = ["{المستند}", "{السبب}", "{المهلة}"], UpdatedAt = DemoToday.AddDays(-30),
        });
        await db.SaveChangesAsync();
    }

    // ───────── Organizations, roles, users ─────────

    private void SeedOrganizations()
    {
        Org("alufuq", "مصرف الأفق", "Alufuq Bank", "أف", OrganizationKind.Lender, "الرياض", ["alufuq.example"]);
        Org("sunbula", "شركة السنبلة للتمويل", "Sunbula Finance", "سن", OrganizationKind.Lender, "جدة", ["sunbula.example"]);
        Org("alwaha", "مصرف الواحة", "Alwaha Bank", "وح", OrganizationKind.Lender, "الدمام", ["alwaha.example"]);
        Org("valuer-b", "مكتب تقييم معتمد «ب»", "Certified Valuer B", "تق", OrganizationKind.ServiceProvider, "الرياض", ["valuer-b.example"]);
        Org("broker-d", "مكتب وساطة عقارية «د»", "Brokerage D", "وس", OrganizationKind.ServiceProvider, "الرياض", ["broker-d.example"]);
        Org("agent-j", "مكتب وكيل البيع «ج»", "Sale Agent J", "وك", OrganizationKind.JudicialAgent, "جدة", ["agent-j.example"]);
        Org("platform", "منصة رهون", "Rahoon Platform", "ره", OrganizationKind.Platform, "الرياض", ["rahoon.example"]);
    }

    private void Org(string code, string ar, string en, string initials, OrganizationKind kind, string city, List<string> domains)
    {
        var o = new Organization { ShortCode = code, NameAr = ar, NameEn = en, Initials = initials, Kind = kind, City = city, AllowedEmailDomains = domains, CreatedAt = DemoToday.AddYears(-1) };
        db.Organizations.Add(o);
        _orgs[code] = o;
    }

    private void SeedRoles()
    {
        foreach (var org in _orgs.Values)
        {
            foreach (var t in SystemRoles.Templates.Where(t => t.Kind == org.Kind))
            {
                var role = new Role { OrganizationId = org.Id, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
                role.Permissions.AddRange(t.Permissions.Distinct().Select(p => new RolePermission
                {
                    PermissionKey = p,
                    Grant = p is P.SolutionApprove or P.PiiReveal or P.UserManage ? PermissionGrant.Conditional : PermissionGrant.Allow,
                }));
                db.Roles.Add(role);
                _roles[(org.Id, t.Key)] = role;
            }
        }
    }

    private void SeedUsers()
    {
        var alufuq = _orgs["alufuq"];
        User("sara", "s.alqahtani@alufuq.example", "سارة القحطاني", "0550000101", (alufuq, SystemRoles.CaseManager, "مديرة حالات"), (_orgs["sunbula"], SystemRoles.CreditAnalyst, "محللة ائتمان"));
        User("fahad", "f.alotaibi@alufuq.example", "فهد العتيبي", "0550000102", (alufuq, SystemRoles.CreditAnalyst, "محلل ائتمان"));
        User("noura", "n.alshehri@alufuq.example", "نورة الشهري", "0550000103", (alufuq, SystemRoles.Approver, "معتمدة"));
        User("majed", "m.alharbi@alufuq.example", "ماجد الحربي", "0550000104", (alufuq, SystemRoles.Legal, "القانونية"));
        User("reem", "r.aldosari@alufuq.example", "ريم الدوسري", "0550000105", (alufuq, SystemRoles.Finance, "المالية"));
        User("khaled", "k.alzahrani@alufuq.example", "خالد الزهراني", "0550000106", (alufuq, SystemRoles.CaseOfficer, "موظف حالة"));
        User("layla", "l.alghamdi@alufuq.example", "ليلى الغامدي", "0550000107", (alufuq, SystemRoles.OrgAdmin, "مسؤولة المنشأة"));
        User("aziz", "a.alshammari@alufuq.example", "عبدالعزيز الشمري", "0550000108", (alufuq, SystemRoles.Finance, "المالية"));
        User("salman", "s.alomari@alufuq.example", "سلمان العمري", "0550000109", (alufuq, SystemRoles.SeniorApprover, "معتمد أول"));
        User("hind", "h.almutairi@alufuq.example", "هند المطيري", "0550000110", (alufuq, SystemRoles.Compliance, "الامتثال · مراجِعة شكاوى"));
        User("mansour", "m.alqarni@alufuq.example", "منصور القرني", "0550000111", (alufuq, SystemRoles.Auditor, "مدقق"));
        User("badr", "b.alsalem@alufuq.example", "بدر السالم", "0550000112", (alufuq, SystemRoles.CaseManager, "مدير حالات"));
        User("maha", "m.alshahrani@sunbula.example", "مها الشهراني", "0550000201", (_orgs["sunbula"], SystemRoles.CaseManager, "مديرة حالات"));
        User("omar", "o.alanazi@valuer-b.example", "عمر العنزي", "0550000301", (_orgs["valuer-b"], SystemRoles.ProviderAgent, "مقيّم"));
        User("waleed", "w.alqahtani@broker-d.example", "وليد القحطاني", "0550000302", (_orgs["broker-d"], SystemRoles.ProviderAgent, "وسيط"));
        User("yasser", "y.alhamdan@agent-j.example", "ياسر الحمدان", "0550000401", (_orgs["agent-j"], SystemRoles.JudicialAgent, "وكيل بيع"));
        User("ahmad", "a.almutairi@rahoon.example", "أحمد المطيري", "0550000501", (_orgs["platform"], SystemRoles.PlatformOps, "مسؤول عمليات"));
        User("rana", "r.alsubaie@rahoon.example", "رنا السبيعي", "0550000502", (_orgs["platform"], SystemRoles.PlatformSupport, "دعم تقني"));
        User("faisal", "f.aldossary@rahoon.example", "فيصل الدوسري", "0550000503", (_orgs["platform"], SystemRoles.PlatformAuditor, "مدقق"));
        User("hessa", "h.alotaibi@rahoon.example", "حصة العتيبي", "0550000504", (_orgs["platform"], SystemRoles.PlatformCompliance, "الامتثال"));

        // Team structure (A02)
        var riyadh = new Team { OrganizationId = alufuq.Id, NameAr = "التحصيل — الرياض" };
        var jeddah = new Team { OrganizationId = alufuq.Id, NameAr = "التحصيل — جدة" };
        var risk = new Team { OrganizationId = alufuq.Id, NameAr = "المخاطر" };
        db.Teams.AddRange(riyadh, jeddah, risk);
        foreach (var m in db.ChangeTracker.Entries<Membership>().Select(e => e.Entity).Where(m => m.OrganizationId == alufuq.Id))
        {
            var key = _users.First(u => u.Value.Id == m.UserId).Key;
            m.TeamId = key switch { "noura" or "salman" => risk.Id, "badr" => jeddah.Id, _ => riyadh.Id };
        }
    }

    private void User(string key, string email, string name, string phone, params (Organization Org, string Role, string Title)[] memberships)
    {
        var u = new User { Email = email, FullName = name, Phone = phone, MfaEnrolled = true, CreatedAt = DemoToday.AddMonths(-6) };
        u.PasswordHash = hasher.HashPassword(u, DemoPassword);
        db.Users.Add(u);
        _users[key] = u;
        foreach (var (org, role, title) in memberships)
        {
            var m = new Membership
            {
                OrganizationId = org.Id, UserId = u.Id, Title = title, CreatedAt = DemoToday.AddMonths(-6),
                Status = key == "badr" ? MembershipStatus.Invited : MembershipStatus.Active,
            };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = _roles[(org.Id, role)].Id });
            db.Memberships.Add(m);
        }
    }

    private Guid UserId(string key) => _users[key].Id;

    // ───────── Institution configuration (A03, A04, A06) ─────────

    private void SeedInstitutionConfig(Organization org)
    {
        (CaseStatus status, int days, string note, bool pause)[] sla =
        [
            (CaseStatus.AwaitingData, 5, "تذكير يوم 3", false),
            (CaseStatus.Verification, 5, "تصعيد لمدير الفريق", false),
            (CaseStatus.Valuation, 10, "يُحسب من إسناد المقيّم", false),
            (CaseStatus.ProposedSolution, 5, "افتراض — يتطلب تأكيد المنتج", false),
            (CaseStatus.InternalApproval, 3, "تذكير المعتمد يوم 2", false),
            (CaseStatus.AwaitingCustomer, 10, "توقف عند شكوى مفتوحة", true),
            (CaseStatus.Negotiation, 5, "افتراض — يتطلب تأكيد المنتج", true),
            (CaseStatus.AwaitingReconciliation, 5, "تصعيد للمالية", false),
        ];
        foreach (var s in sla)
            db.SlaRules.Add(new SlaRule { OrganizationId = org.Id, Status = s.status.ToString(), BusinessDays = s.days, RuleNote = s.note, PausesOnOpenComplaint = s.pause });

        (string type, string? before, string uploader, int? validity, string? note, string[] visible, bool summary)[] rules =
        [
            ("title_deed", "Verification", "lender", null, null, ["case_team", "legal"], false),
            ("national_id", "Verification", "owner", null, "90 يوماً قبل الانتهاء", ["case_team"], false),
            ("financing_contract", "Verification", "lender", null, null, ["case_team", "legal"], false),
            ("salary_statement", "ProposedSolution", "owner", 90, null, ["case_team", "approver"], false),
            ("valuation_report", "ProposedSolution", "provider", 90, null, ["case_team", "approver", "owner"], true),
            ("poa", null, "owner", null, "حسب الوكالة", ["case_team", "legal"], false),
            ("payment_proof", "ActiveSettlement", "owner_or_finance", null, null, ["case_team", "finance", "owner"], false),
        ];
        foreach (var r in rules)
            db.DocumentRules.Add(new DocumentRule
            {
                OrganizationId = org.Id, DocumentTypeKey = r.type, RequiredBeforeStatus = r.before, Uploader = r.uploader,
                ValidityDays = r.validity, ValidityNote = r.note, VisibleTo = [.. r.visible], OwnerSummaryOnly = r.summary,
            });

        var layla = _users["layla"].Id;
        var policy = new ApprovalLimitPolicy
        {
            OrganizationId = org.Id, VersionNo = 4, Status = "effective", EffectiveFrom = new DateOnly(2026, 7, 1),
            ChangeSummary = "الإصدار النافذ", ProposedByUserId = layla, ApprovedByUserId = layla, ApprovedAt = DemoToday.AddDays(-84),
        };
        policy.Tiers.AddRange(
        [
            new() { Rank = 0, RoleKey = SystemRoles.CreditAnalyst, LevelLabel = "محلل / مدير حالات", SolutionKinds = ["Reschedule"], SolutionKindsLabel = "إعادة جدولة", CanApprove = false, EscalateToLabel = "إعداد فقط، لا اعتماد" },
            new() { Rank = 1, RoleKey = SystemRoles.Approver, LevelLabel = "معتمد", SolutionKinds = ["Reschedule", "GracePeriod"], SolutionKindsLabel = "إعادة جدولة، سماح", MaxAmount = 2_000_000m, MaxWaiverPercent = 0.05m, EscalateToLabel = "معتمد أعلى" },
            new() { Rank = 2, RoleKey = SystemRoles.SeniorApprover, LevelLabel = "معتمد أول", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", MaxAmount = 5_000_000m, MaxWaiverPercent = 0.10m, EscalateToLabel = "لجنة المخاطر" },
            new() { Rank = 3, RoleKey = SystemRoles.RiskCommittee, LevelLabel = "لجنة المخاطر", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", MaxAmount = null, MaxWaiverPercent = null, EscalateToLabel = "—" },
        ]);
        db.ApprovalLimitPolicies.Add(policy);
    }
}
