using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Seed;

public sealed partial class DevSeeder
{
    private static readonly string[] FirstNames =
    [
        "محمد", "أحمد", "خالد", "فهد", "سعود", "عبدالرحمن", "ناصر", "بندر", "تركي", "سلمان", "ماجد", "عمر", "يوسف", "إبراهيم", "صالح",
        "نورة", "سارة", "منيرة", "هيفاء", "ريم", "لمى", "مها", "العنود", "جواهر", "أمل", "هند", "دلال", "شهد", "رهف", "لطيفة",
    ];
    private static readonly string[] FamilyNames =
    [
        "العتيبي", "القحطاني", "الدوسري", "الشهري", "الغامدي", "الزهراني", "الحربي", "المطيري", "الشمري", "العنزي", "السبيعي", "الرشيد",
        "المالكي", "القرني", "العسيري", "البلوي", "الجهني", "الحارثي", "السهلي", "الخالدي",
    ];
    private static readonly (string City, string[] Districts)[] Cities =
    [
        ("الرياض", ["حي النرجس", "حي الملقا", "حي العارض", "حي الياسمين", "حي قرطبة"]),
        ("جدة", ["حي الروضة", "حي السلامة", "حي الصفا", "حي أبحر"]),
        ("مكة", ["حي العزيزية", "حي الشوقية"]),
        ("الدمام", ["حي الشاطئ", "حي الفيصلية"]),
        ("الخبر", ["حي العليا", "حي الراكة"]),
        ("المدينة", ["حي قباء", "حي العوالي"]),
        ("أبها", ["حي المنسك", "حي الخالدية"]),
    ];
    private static readonly string[] PropertyTypes = ["فيلا سكنية", "شقة سكنية", "دور سكني", "دوبلكس"];

    /// <summary>
    /// Synthetic portfolio matching the design's anchor numbers: 1,248 active cases distributed by
    /// state (P0 portfolio), 42 overdue, 147 closed in the last 90 days, 38 managed by سارة.
    /// </summary>
    private async Task SeedBulkCasesAsync()
    {
        var rnd = new Random(20260923);
        var org = _orgs["alufuq"];
        var today = new DateOnly(2026, 9, 23);

        // Extra case managers so the portfolio is spread across a realistic team.
        var extra = new[] { ("abeer", "a.alrasheed@alufuq.example", "عبير الرشيد"), ("nasser", "n.alqahtani@alufuq.example", "ناصر القحطاني"), ("lama", "l.alanazi@alufuq.example", "لمى العنزي") };
        foreach (var (key, email, name) in extra)
            User(key, email, name, "05500009" + (_users.Count % 90).ToString("D2"), (org, SystemRoles.CaseManager, "مدير حالات"));
        await db.SaveChangesAsync();
        var managerIds = await db.Memberships.Where(m => m.OrganizationId == org.Id && m.Roles.Any(r => r.Role!.Key == SystemRoles.CaseManager) && m.Status == MembershipStatus.Active)
            .Select(m => new { m.Id, m.UserId }).ToListAsync();
        var sara = managerIds.First(m => m.UserId == UserId("sara"));
        var others = managerIds.Where(m => m.UserId != UserId("sara")).ToList();
        var khaled = await db.Memberships.FirstAsync(m => m.UserId == UserId("khaled") && m.OrganizationId == org.Id);
        others.Add(new { khaled.Id, khaled.UserId });

        // Target distribution (active) minus canonical cases already seeded.
        var dist = new (CaseStatus Status, int Total, int Overdue, int MedianDays)[]
        {
            (CaseStatus.AwaitingData, 96, 0, 3), (CaseStatus.Verification, 188, 14, 6), (CaseStatus.Valuation, 141, 11, 9),
            (CaseStatus.ProposedSolution, 122, 0, 5), (CaseStatus.InternalApproval, 37, 3, 2), (CaseStatus.AwaitingCustomer, 164, 9, 7),
            (CaseStatus.Negotiation, 58, 0, 5), (CaseStatus.ActiveSettlement, 312, 0, 60), (CaseStatus.VoluntarySale, 18, 0, 20),
            (CaseStatus.JudicialReferral, 9, 0, 30), (CaseStatus.ExternalJudicialSale, 4, 0, 45), (CaseStatus.AwaitingReconciliation, 61, 5, 4),
            (CaseStatus.Paused, 38, 0, 14),
        };
        var canonical = _cases.Values.Where(c => c.OrganizationId == org.Id).ToList();
        var saraSlots = 38 - canonical.Count(c => c.AssignedManagerId == sara.Id);
        var docExpiring = 19 - 1; // RH-2026-004172 already has an ID expiring in 12 days
        var number = 1000;
        var batch = 0;

        foreach (var (status, total, overdueTotal, median) in dist)
        {
            var existing = canonical.Where(c => c.Status == status).ToList();
            var overdue = overdueTotal - existing.Count(c => c.StageDueOn < today);
            for (var i = 0; i < total - existing.Count; i++)
            {
                var first = FirstNames[rnd.Next(FirstNames.Length)];
                var family = FamilyNames[rnd.Next(FamilyNames.Length)];
                var (city, districts) = Cities[rnd.Next(Cities.Length)];
                var district = districts[rnd.Next(districts.Length)];
                var type = PropertyTypes[rnd.Next(PropertyTypes.Length)];
                var principal = Math.Round((decimal)(350_000 + rnd.NextDouble() * 1_700_000) / 100m) * 100m;
                var profit = Math.Round(principal * (decimal)(0.04 + rnd.NextDouble() * 0.05), 2);
                var late = status == CaseStatus.ActiveSettlement ? 0 : Math.Round((decimal)(rnd.Next(0, 30)) * 1000m, 2);
                var arrearsN = status == CaseStatus.ActiveSettlement ? 0 : rnd.Next(3, 13);
                var installment = Math.Round(principal / 150m, 2);
                var assignSara = saraSlots > 0 && rnd.NextDouble() < 0.06;
                var mgr = assignSara ? sara : others[rnd.Next(others.Count)];
                if (assignSara) saraSlots--;
                var daysInStage = Math.Max(0, (int)Math.Round(median + (rnd.NextDouble() - 0.5) * median));
                DateOnly? due = null;
                var sla = await SlaDaysAsync(org.Id, status);
                if (sla is { } days)
                {
                    due = overdue > 0 ? today.AddDays(-rnd.Next(1, 6)) : today.AddDays(rnd.Next(1, days + 3));
                    if (overdue > 0) overdue--;
                }
                var openedDaysAgo = status == CaseStatus.AwaitingData && batch < 36 ? rnd.Next(0, 22) : rnd.Next(25, 240);
                batch++;

                var c = new Case
                {
                    OrganizationId = org.Id, Reference = $"RH-2026-{number++:D6}", Status = status, City = city, Region = RegionOf(city),
                    OpenedOn = today.AddDays(-openedDaysAgo), AssignedManagerId = mgr.Id, CreatedByUserId = mgr.UserId, DraftStep = 6,
                    StatusChangedAt = DemoToday.AddDays(-daysInStage), StageDueOn = due, OutstandingAmount = principal + profit + late,
                    OutstandingAsOf = At("2026-09-23T06:00:00"), OutstandingSource = "نظام التمويل الأساسي",
                    ArrearsAmount = arrearsN * installment, ArrearsInstallments = arrearsN, ArrearsSince = today.AddMonths(-arrearsN - 1),
                    CreatedAt = DemoToday.AddDays(-openedDaysAgo),
                };
                if (status == CaseStatus.Paused) { c.StatusBeforePause = CaseStatus.Verification; c.PauseReason = "بانتظار مستند من جهة خارجية"; c.SlaPausedAt = c.StatusChangedAt; }
                db.Cases.Add(c);
                var nid = "1" + rnd.Next(0, 999_999_999).ToString("D9");
                var party = factory.NewParty(org.Id, c.Id, PartyRole.OwnerBorrower, $"{first} {FamilyNames[rnd.Next(FamilyNames.Length)]} {family}", nid,
                    "05" + rnd.Next(0, 99_999_999).ToString("D8"), primary: true);
                db.Parties.Add(party);
                db.Properties.Add(new Property { OrganizationId = org.Id, CaseId = c.Id, Type = type, City = city, District = district, ShortLabel = $"{type}، {district}، {city}", Occupancy = OccupancyKind.OwnerFamily });
                db.FinancingContracts.Add(new FinancingContract { OrganizationId = org.Id, CaseId = c.Id, ContractNumber = $"MF-{rnd.Next(10, 99)}-{rnd.Next(1_000_000, 9_999_999)}", OriginalInstallment = installment });
                db.DebtSnapshots.Add(new DebtSnapshot
                {
                    OrganizationId = org.Id, CaseId = c.Id, Source = "نظام التمويل الأساسي", AsOf = At("2026-09-23T06:00:00"),
                    Principal = principal, Profit = profit, LateFees = late, Total = principal + profit + late, RecordedByUserId = mgr.UserId,
                });
                if (docExpiring > 0 && status is CaseStatus.Verification or CaseStatus.Valuation or CaseStatus.ProposedSolution && rnd.NextDouble() < 0.1)
                {
                    docExpiring--;
                    db.Documents.Add(new CaseDocument
                    {
                        OrganizationId = org.Id, CaseId = c.Id, DocumentTypeKey = "national_id", Name = "صورة الهوية الوطنية", Source = DocumentSource.Owner,
                        Status = DocumentStatus.Verified, ValidUntil = today.AddDays(rnd.Next(3, 29)), VisibleToOwner = true,
                    });
                }
                if (number % 200 == 0) await db.SaveChangesAsync();
            }
        }

        // 147 closed within 90 days and a few cancelled — not active.
        for (var i = 0; i < 150; i++)
        {
            var closed = i < 147;
            var c = new Case
            {
                OrganizationId = org.Id, Reference = $"RH-2026-{number++:D6}", Status = closed ? CaseStatus.Closed : CaseStatus.Cancelled,
                City = "الرياض", Region = "الرياض", OpenedOn = today.AddDays(-rnd.Next(120, 400)), AssignedManagerId = others[rnd.Next(others.Count)].Id,
                CreatedByUserId = UserId("sara"), DraftStep = 6, StatusChangedAt = DemoToday.AddDays(-rnd.Next(1, 89)),
                ClosedAt = closed ? DemoToday.AddDays(-rnd.Next(1, 89)) : null, CancelReason = closed ? null : "سداد كامل قبل بدء المعالجة",
                OutstandingAmount = 0, OutstandingAsOf = At("2026-09-23T06:00:00"), OutstandingSource = "نظام التمويل الأساسي",
            };
            db.Cases.Add(c);
            db.Parties.Add(factory.NewParty(org.Id, c.Id, PartyRole.OwnerBorrower, $"{FirstNames[rnd.Next(FirstNames.Length)]} {FamilyNames[rnd.Next(FamilyNames.Length)]}", null, null, primary: true));
        }
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO cases.reference_counters (key, value) VALUES ('case:2026', 5200) ON CONFLICT (key) DO NOTHING");
    }

    private async Task<int?> SlaDaysAsync(Guid orgId, CaseStatus status)
    {
        var rule = await db.SlaRules.AsNoTracking().FirstOrDefaultAsync(r => r.OrganizationId == orgId && r.Status == status.ToString());
        if (rule is null) rule = db.ChangeTracker.Entries<Modules.Administration.SlaRule>().Select(e => e.Entity).FirstOrDefault(r => r.OrganizationId == orgId && r.Status == status.ToString());
        return rule?.BusinessDays;
    }

    /// <summary>A second lender with its own cases — used to demonstrate and test tenant isolation.</summary>
    private async Task SeedOtherTenantAsync()
    {
        var specs = new[]
        {
            new CaseSpec("RH-2026-005101", "sunbula", "مشعل عبدالله الجهني", "1099915101", "0557775101", "جدة", "حي أبحر", "فيلا سكنية",
                CaseStatus.Valuation, "maha", 1_450_000.00m, 1_360_000.00m, 80_000.00m, 10_000.00m, 90_000.00m, 6, "2026-03-01", "2026-08-02", "SN-10-5101001", 12_000m,
                "2026-10-02", "2026-09-14T10:00:00"),
            new CaseSpec("RH-2026-005102", "sunbula", "أمل صالح الحربي", "1099925102", "0557775102", "الطائف", "حي الحوية", "شقة سكنية",
                CaseStatus.Verification, "maha", 610_000.00m, 580_000.00m, 25_000.00m, 5_000.00m, 30_000.00m, 3, "2026-06-01", "2026-09-05", "SN-10-5102002", 7_500m,
                "2026-09-30", "2026-09-10T10:00:00"),
        };
        foreach (var s in specs) _cases[s.Reference] = await BuildCaseAsync(s);
        await db.SaveChangesAsync();
    }
}
