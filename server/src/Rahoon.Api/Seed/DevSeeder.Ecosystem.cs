using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Modules.Ecosystem;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Seed;

/// <summary>
/// B9 fictional data: provider registry and licences, each institution's directory, 12-month assignment history
/// (performance is computed from it), provider invoices, workflow versions, billing plans and conditional
/// integration settings. Historical assignments carry a past AccessExpiresAt so they never grant data access.
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedEcosystemAsync()
    {
        // ── Additional provider organizations (roles copied from the templates, like SeedRoles) ──
        ProviderOrg("broker-a", "دار الوسطاء «أ»", "Brokers House A", "دأ", "الرياض");
        ProviderOrg("broker-c", "مكتب الوساطة «ج»", "Brokerage Office C", "وج", "الرياض");
        ProviderOrg("valuer-w", "مكتب التقييم «و»", "Valuation Office W", "تو", "الرياض");
        ProviderOrg("valuer-h", "مكتب التقييم «ح»", "Valuation Office H", "تح", "جدة");
        ProviderOrg("inspect-z", "فحص «ز» الهندسي", "Z Engineering Inspection", "فز", "الرياض");
        ProviderOrg("valuer-e", "مكتب التقييم «هـ»", "Valuation Office E", "ته", "الدمام");
        ProviderOrg("inspect-t", "شركة الفحص «ط»", "T Inspection Co", "فط", "الرياض");
        await db.SaveChangesAsync();
        User("hatem", "h.alrashid@valuer-b.example", "حاتم الرشيد", "0550000303", (_orgs["valuer-b"], SystemRoles.ProviderAdmin, "مسؤول مقدم الخدمة"));
        User("sami", "s.alharbi@broker-a.example", "سامي الحربي", "0550000304", (_orgs["broker-a"], SystemRoles.ProviderAgent, "وسيط"));
        User("nader", "n.alomari@valuer-e.example", "نادر العمري", "0550000305", (_orgs["valuer-e"], SystemRoles.ProviderAdmin, "مسؤول التسجيل"));
        User("tariq", "t.alsaadi@inspect-t.example", "طارق السعدي", "0550000306", (_orgs["inspect-t"], SystemRoles.ProviderAdmin, "مسؤول التسجيل"));
        await db.SaveChangesAsync();

        // ── Platform registry (PA16) + licences (V06) ──
        var today = D("2026-09-23");
        (string code, ProviderType type, string app, ProviderRegistrationStatus status, DateOnly? licence, string licNo)[] registry =
        [
            ("valuer-b", ProviderType.Valuer, "PRV-APP-0012", ProviderRegistrationStatus.Accepted, D("2027-06-30"), "1100884471"),
            ("valuer-w", ProviderType.Valuer, "PRV-APP-0019", ProviderRegistrationStatus.Accepted, D("2026-10-31"), "1100772290"),
            ("valuer-h", ProviderType.Valuer, "PRV-APP-0021", ProviderRegistrationStatus.SuspendedLicense, D("2026-08-31"), "1100661802"),
            ("broker-a", ProviderType.Broker, "PRV-APP-0015", ProviderRegistrationStatus.Accepted, D("2027-03-31"), "1200553318"),
            ("broker-c", ProviderType.Broker, "PRV-APP-0026", ProviderRegistrationStatus.Accepted, D("2026-12-31"), "1200441127"),
            ("broker-d", ProviderType.Broker, "PRV-APP-0031", ProviderRegistrationStatus.Accepted, today.AddDays(20), "1200339905"),
            ("inspect-z", ProviderType.Inspection, "PRV-APP-0017", ProviderRegistrationStatus.Accepted, D("2027-01-31"), "1300227764"),
        ];
        foreach (var r in registry)
        {
            var org = _orgs[r.code];
            var p = new ProviderProfile
            {
                ProviderOrganizationId = org.Id, ApplicationRef = r.app, LegalName = org.NameAr, ProviderType = r.type, City = org.City, CrNumber = "10108" + r.licNo[^5..],
                Status = r.status, CurrentStep = 6, CompletedSteps = [1, 2, 3, 4, 5, 6], RepresentativeName = "الممثل النظامي", TeamCount = 3, TeamIndividuallyLicensed = true,
                BillingIbanMasked = "SA•• •••• •••• 4410", IndependenceDeclared = true, DataProtectionSigned = true,
                SubmittedAt = DemoToday.AddMonths(-10), DecidedAt = DemoToday.AddMonths(-10).AddDays(3), AcceptedAt = DemoToday.AddMonths(-10).AddDays(3),
                DecidedByUserId = UserId("hessa"), DecisionMessage = "اكتملت المراجعة.", CreatedAt = DemoToday.AddMonths(-11),
                SuspendedReason = r.status == ProviderRegistrationStatus.SuspendedLicense ? "انتهى ترخيص المزاولة 2026-08-31" : null,
            };
            db.Set<ProviderProfile>().Add(p);
            SeedLicense(p, LicenseRules.PracticeLicense, r.licNo, r.licence, LicenseReviewStatus.Approved);
            SeedLicense(p, LicenseRules.Insurance, "INS-" + r.licNo[^4..], (r.licence ?? today).AddMonths(-2), LicenseReviewStatus.Approved);
            SeedLicense(p, LicenseRules.CommercialRegister, p.CrNumber, D("2027-12-31"), LicenseReviewStatus.Approved);
        }

        // V06b sample: PRV-APP-0044 submitted, insurance expiring in 14 days, CR missing, data-protection agreement unsigned.
        var e = new ProviderProfile
        {
            ProviderOrganizationId = _orgs["valuer-e"].Id, ApplicationRef = "PRV-APP-0044", LegalName = "مكتب التقييم «هـ»", ProviderType = ProviderType.Valuer, City = "الدمام",
            CrNumber = "2050114471", Status = ProviderRegistrationStatus.Submitted, CurrentStep = 6, CompletedSteps = [1, 2, 3, 4, 5, 6],
            RepresentativeName = "نادر العمري", TeamCount = 3, TeamIndividuallyLicensed = true, BillingIbanMasked = "SA•• •••• •••• 7702", IndependenceDeclared = true,
            DataProtectionSigned = false, SubmittedAt = At("2026-09-21T11:20:00"), LastSavedAt = At("2026-09-21T11:20:00"), CreatedAt = At("2026-09-14T09:00:00"),
        };
        db.Set<ProviderProfile>().Add(e);
        SeedLicense(e, LicenseRules.PracticeLicense, "1100924471", D("2028-02-01"), LicenseReviewStatus.Pending);
        SeedLicense(e, LicenseRules.Insurance, "INS-0044", D("2026-10-07"), LicenseReviewStatus.Pending);
        SeedLicense(e, LicenseRules.CommercialRegister, "2050114471", D("2027-05-30"), LicenseReviewStatus.Pending);

        // V06a sample: draft at step 3, autosaved.
        var t = new ProviderProfile
        {
            ProviderOrganizationId = _orgs["inspect-t"].Id, ApplicationRef = "PRV-APP-0047", LegalName = "شركة الفحص «ط»", ProviderType = ProviderType.Inspection, City = "الرياض",
            Status = ProviderRegistrationStatus.Draft, CurrentStep = 3, CompletedSteps = [1, 2], RepresentativeName = "طارق السعدي",
            StepDataJson = """{"1":{"legalName":"شركة الفحص «ط»","city":"الرياض"},"2":{"representativeName":"طارق السعدي"}}""",
            LastSavedAt = At("2026-09-23T14:02:00"), CreatedAt = At("2026-09-22T10:00:00"),
        };
        db.Set<ProviderProfile>().Add(t);
        SeedLicense(t, LicenseRules.PracticeLicense, "1300554471", D("2028-02-01"), LicenseReviewStatus.Pending);
        SeedLicense(t, LicenseRules.Insurance, "INS-0047", D("2026-10-07"), LicenseReviewStatus.Pending);
        await db.SaveChangesAsync();

        // ── Institution directories (V05): each institution curates its own list ──
        var alufuq = _orgs["alufuq"];
        var sunbula = _orgs["sunbula"];
        (string org, string provider, ProviderType type, decimal? fee, decimal? rate)[] directory =
        [
            ("alufuq", "valuer-b", ProviderType.Valuer, 3_500m, null), ("alufuq", "valuer-w", ProviderType.Valuer, 3_500m, null),
            ("alufuq", "valuer-h", ProviderType.Valuer, 3_500m, null), ("alufuq", "broker-a", ProviderType.Broker, null, 0.02m),
            ("alufuq", "broker-c", ProviderType.Broker, null, 0.025m), ("alufuq", "broker-d", ProviderType.Broker, null, 0.02m),
            ("alufuq", "inspect-z", ProviderType.Inspection, 2_000m, null), ("sunbula", "valuer-b", ProviderType.Valuer, 4_200m, null),
        ];
        foreach (var d in directory)
            db.Set<InstitutionProvider>().Add(new InstitutionProvider
            {
                OrganizationId = _orgs[d.org].Id, ProviderOrganizationId = _orgs[d.provider].Id, ProviderType = d.type, FrameworkFee = d.fee, CommissionRate = d.rate,
                AddedByUserId = d.org == "alufuq" ? UserId("layla") : UserId("maha"), CreatedAt = DemoToday.AddMonths(-9),
            });

        // ── 12-month assignment history → computed performance (V05 / V07); «ب» at alufuq also has the canonical ASG-2026-0418 ──
        (string org, string provider, AssignmentType type, int delivered, int late, int reworked, int avgDays)[] history =
        [
            ("alufuq", "valuer-b", AssignmentType.Valuation, 24, 1, 1, 9), ("sunbula", "valuer-b", AssignmentType.Valuation, 23, 1, 3, 9),
            ("alufuq", "valuer-w", AssignmentType.Valuation, 25, 3, 2, 10), ("alufuq", "valuer-h", AssignmentType.Valuation, 19, 5, 3, 13),
            ("alufuq", "broker-a", AssignmentType.Brokerage, 38, 3, 0, 46), ("alufuq", "broker-c", AssignmentType.Brokerage, 21, 2, 0, 58),
            ("alufuq", "broker-d", AssignmentType.Brokerage, 12, 2, 0, 52), ("alufuq", "inspect-z", AssignmentType.Inspection, 20, 0, 0, 6),
        ];
        var alufuqCases = _cases.Values.Where(c => c.OrganizationId == alufuq.Id).OrderBy(c => c.Reference).ToList();
        var sunbulaCases = _cases.Values.Where(c => c.OrganizationId == sunbula.Id).OrderBy(c => c.Reference).ToList();
        var reserved = new Dictionary<(string, int), string> // invoice samples reuse these references
        {
            [("alufuq:valuer-b", 21)] = "ASG-2026-0830", [("alufuq:valuer-b", 22)] = "ASG-2026-0842", [("alufuq:valuer-b", 23)] = "ASG-2026-0851",
            [("sunbula:valuer-b", 22)] = "ASG-2026-0859",
        };
        var refs = new Dictionary<string, ProviderAssignment>();
        var seq = 600;
        foreach (var h in history)
        {
            var lender = _orgs[h.org];
            var cases = h.org == "alufuq" ? alufuqCases : sunbulaCases;
            var fee = directory.First(d => d.org == h.org && d.provider == h.provider).fee;
            for (var i = 0; i < h.delivered; i++)
            {
                var delivered = At("2026-09-20T12:00:00").AddDays(-(h.delivered - i) * 330 / h.delivered);
                var created = delivered.AddDays(-h.avgDays);
                var isLate = i < h.late;
                var due = DateOnly.FromDateTime(delivered.ToOffset(TimeSpan.FromHours(3)).DateTime).AddDays(isLate ? -2 : 1);
                var reference = reserved.GetValueOrDefault(($"{h.org}:{h.provider}", i)) ?? $"ASG-2026-{seq++:D4}";
                var a = new ProviderAssignment
                {
                    OrganizationId = lender.Id, Reference = reference, CaseId = cases[i % cases.Count].Id, ProviderOrganizationId = _orgs[h.provider].Id,
                    Type = h.type, Title = h.type switch { AssignmentType.Valuation => "تقييم عقار", AssignmentType.Brokerage => "وساطة بيع طوعي", _ => "فحص فني" },
                    PropertyLabel = "عقار سكني", Status = h.type == AssignmentType.Brokerage ? AssignmentStatus.Closed : AssignmentStatus.Accepted, DueOn = due,
                    FeesLabel = fee is { } f ? $"{f:N2} ر.س" : "عمولة من سعر البيع", FeeAmount = fee, CreatedByUserId = h.org == "alufuq" ? UserId("fahad") : UserId("maha"),
                    DeliveredAt = delivered, AccessExpiresAt = delivered.AddDays(7), CreatedAt = created,
                };
                db.Assignments.Add(a);
                refs[reference] = a;
                if (i >= h.late && i < h.late + h.reworked)
                    db.AssignmentSubmissions.Add(new AssignmentSubmission
                    {
                        OrganizationId = lender.Id, AssignmentId = a.Id, VersionNo = 1, Status = SubmissionStatus.Returned, SubmittedAt = delivered.AddDays(-3),
                        SubmittedByUserId = UserId(h.provider == "valuer-b" ? "omar" : "hatem"), ReturnNotes = ["المقارنات غير كافية"],
                    });
            }
        }
        await db.SaveChangesAsync();

        // ── Provider invoices (V07) ──
        (string number, string asg, string lender, decimal amount, string issued, ProviderInvoiceStatus status, string? rejection, string? payRef)[] invoices =
        [
            ("INV-B-2026-114", "ASG-2026-0842", "alufuq", 3_500m, "2026-09-12", ProviderInvoiceStatus.Paid, null, "TRX-99120034"),
            ("INV-B-2026-118", "ASG-2026-0851", "alufuq", 3_500m, "2026-09-15", ProviderInvoiceStatus.Approved, null, null),
            ("INV-B-2026-121", "ASG-2026-0859", "sunbula", 4_200m, "2026-09-19", ProviderInvoiceStatus.UnderReview, null, null),
            ("INV-B-2026-109", "ASG-2026-0830", "alufuq", 3_800m, "2026-08-30", ProviderInvoiceStatus.Rejected, "مبلغ مختلف عن الاتفاقية", null),
        ];
        foreach (var inv in invoices)
        {
            var a = refs[inv.asg];
            var decider = inv.lender == "alufuq" ? UserId("reem") : UserId("maha");
            db.Set<ProviderInvoice>().Add(new ProviderInvoice
            {
                Number = inv.number, AssignmentId = a.Id, AssignmentReference = a.Reference, LenderOrganizationId = _orgs[inv.lender].Id,
                ProviderOrganizationId = _orgs["valuer-b"].Id, Amount = inv.amount, IssuedOn = D(inv.issued), Status = inv.status,
                CreatedByUserId = UserId("hatem"), SubmittedByUserId = UserId("hatem"), SubmittedAt = At(inv.issued + "T10:00:00"),
                DecidedByUserId = inv.status is ProviderInvoiceStatus.UnderReview ? null : decider,
                DecidedAt = inv.status is ProviderInvoiceStatus.UnderReview ? null : At(inv.issued + "T15:00:00").AddDays(2),
                RejectionReason = inv.rejection, PaymentReference = inv.payRef, PaidAt = inv.payRef is null ? null : At(inv.issued + "T12:00:00").AddDays(6),
                PaidRecordedByUserId = inv.payRef is null ? null : UserId("aziz"), CreatedAt = At(inv.issued + "T09:00:00"),
            });
        }

        // ── Workflow designer (PA14): v5 active since 2026-07-01, v6 draft with the compliance-review rule ──
        foreach (var lender in new[] { alufuq, sunbula })
        {
            var stages = WorkflowModel.DefaultStages();
            db.Set<WorkflowVersion>().Add(new WorkflowVersion
            {
                InstitutionOrganizationId = lender.Id, VersionNo = 5, Status = WorkflowVersionStatus.Active, EffectiveFrom = D("2026-07-01"),
                StagesJson = WorkflowModel.Serialize(stages), ChangeSummary = "الإصدار النافذ", CreatedByUserId = UserId("ahmad"),
                SubmittedByUserId = UserId("ahmad"), SubmittedAt = At("2026-06-25T10:00:00"), ApprovedByUserId = UserId("hessa"), ApprovedAt = At("2026-06-28T12:00:00"),
                CreatedAt = At("2026-06-20T10:00:00"),
            });
        }
        var v6 = WorkflowModel.DefaultStages();
        var approval = v6.First(s => s.Key == "internal_approval");
        approval.ChangedInVersion = 6;
        approval.Rules.Add(new WorkflowRule
        {
            Id = "waiver-3-compliance", Label = "جديد: التنازل > 3% ← مراجعة الامتثال", AddedInVersion = 6, Action = "add_compliance_review",
            Condition = new RuleCondition { Field = "waiver_percent", Operator = ">", Value = 3 },
        });
        db.Set<WorkflowVersion>().Add(new WorkflowVersion
        {
            InstitutionOrganizationId = alufuq.Id, VersionNo = 6, Status = WorkflowVersionStatus.Draft, StagesJson = WorkflowModel.Serialize(v6),
            BasedOnVersionNo = 5, ChangeSummary = "إضافة مراجعة الامتثال للتنازل فوق 3%", CreatedByUserId = UserId("ahmad"), CreatedAt = At("2026-09-21T10:00:00"),
        });

        // ── Billing (PA17): plans are data; usage = live active-case count ──
        (string key, string name, decimal? price, int? limit, string label)[] plans =
        [
            ("basic", "الأساسية", 18_000m, 500, "حتى 500 حالة نشطة"),
            ("enterprise", "المؤسسية", 42_000m, 3_000, "حتى 3,000 حالة نشطة"),
            ("custom", "المخصصة", null, null, "أكثر من 3,000 حالة"),
        ];
        var order = 0;
        foreach (var p in plans)
            db.Set<BillingPlan>().Add(new BillingPlan { Key = p.key, NameAr = p.name, MonthlyPrice = p.price, ActiveCaseLimit = p.limit, LimitLabel = p.label, SortOrder = order++, CreatedAt = DemoToday.AddYears(-1) });
        Org("almada", "شركة المدى للتمويل", "Almada Finance", "مد", OrganizationKind.Lender, "الرياض", ["almada.example"]);
        await db.SaveChangesAsync();
        (string org, string? plan, bool trial)[] subs = [("alufuq", "enterprise", false), ("sunbula", "basic", false), ("alwaha", "enterprise", false), ("almada", null, true)];
        foreach (var s in subs)
            db.Set<InstitutionSubscription>().Add(new InstitutionSubscription { InstitutionOrganizationId = _orgs[s.org].Id, PlanKey = s.plan, Trial = s.trial, StartedOn = D("2026-01-01"), CreatedAt = DemoToday.AddMonths(-9) });
        (string number, string org, string plan, int active, decimal amount, string issued, string due, PlatformInvoiceStatus status, string? payRef)[] bills =
        [
            ("BIL-2026-09-003", "alufuq", "enterprise", 1_248, 42_000m, "2026-09-01", "2026-09-30", PlatformInvoiceStatus.Issued, null),
            ("BIL-2026-09-006", "sunbula", "basic", 412, 18_000m, "2026-09-01", "2026-09-30", PlatformInvoiceStatus.Paid, "TRX-77310045"),
            ("BIL-2026-09-004", "alwaha", "enterprise", 2_104, 42_000m, "2026-09-01", "2026-09-18", PlatformInvoiceStatus.Issued, null),
        ];
        foreach (var b in bills)
            db.Set<PlatformInvoice>().Add(new PlatformInvoice
            {
                Number = b.number, InstitutionOrganizationId = _orgs[b.org].Id, PlanKey = b.plan, Period = "2026-09", ActiveCases = b.active, Amount = b.amount,
                IssuedOn = D(b.issued), DueOn = D(b.due), Status = b.status, PaymentReference = b.payRef, PaidAt = b.payRef is null ? null : At("2026-09-10T10:00:00"),
                RecordedByUserId = b.payRef is null ? null : UserId("ahmad"), CreatedAt = At(b.issued + "T08:00:00"),
            });

        // ── Conditional licensed integrations (X01/X02): no institution has an enabled licensed provider ──
        db.Set<InstitutionIntegration>().Add(new InstitutionIntegration { InstitutionOrganizationId = alufuq.Id, Capability = IntegrationKeys.LicensedSigning, Mode = "disabled", Health = "unavailable", LastUpdateAt = DemoToday, CreatedAt = DemoToday });
        db.Set<InstitutionIntegration>().Add(new InstitutionIntegration { InstitutionOrganizationId = alufuq.Id, Capability = IntegrationKeys.LicensedPayment, Mode = "simulated", Health = "available", ProviderLabel = "بوابة دفع مرخّصة (مكان محجوز)", LastUpdateAt = DemoToday, CreatedAt = DemoToday });
        await db.SaveChangesAsync();
    }

    private void ProviderOrg(string code, string ar, string en, string initials, string city)
    {
        Org(code, ar, en, initials, OrganizationKind.ServiceProvider, city, [$"{code}.example"]);
        var org = _orgs[code];
        foreach (var t in SystemRoles.Templates.Where(t => t.Kind == OrganizationKind.ServiceProvider))
        {
            var role = new Role { OrganizationId = org.Id, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
            role.Permissions.AddRange(t.Permissions.Distinct().Select(p => new RolePermission { PermissionKey = p, Grant = p == P.UserManage ? PermissionGrant.Conditional : PermissionGrant.Allow }));
            db.Roles.Add(role);
            _roles[(org.Id, t.Key)] = role;
        }
    }

    private void SeedLicense(ProviderProfile p, string kind, string? number, DateOnly? expires, LicenseReviewStatus review) =>
        db.Set<ProviderLicense>().Add(new ProviderLicense
        {
            ProviderProfileId = p.Id, ProviderOrganizationId = p.ProviderOrganizationId, Kind = kind, Number = number, ExpiresOn = expires,
            FileName = $"{kind}.pdf", ContentType = "application/pdf", SizeBytes = 2048, UploadedAt = (p.SubmittedAt ?? p.CreatedAt).AddDays(-1),
            UploadedByUserId = p.ProviderOrganizationId == _orgs["valuer-e"].Id ? UserId("nader") : p.ProviderOrganizationId == _orgs["inspect-t"].Id ? UserId("tariq") : UserId("hatem"), ReviewStatus = review,
        });
}
