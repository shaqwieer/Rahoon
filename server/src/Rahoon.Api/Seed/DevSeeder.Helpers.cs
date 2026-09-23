using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Seed;

public sealed partial class DevSeeder
{
    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso + "+03:00").ToUniversalTime();
    private static DateOnly D(string iso) => DateOnly.Parse(iso);

    private sealed record CaseSpec(
        string Reference, string Org, string OwnerFullName, string NationalId, string Phone, string City, string District,
        string PropertyType, CaseStatus Status, string Manager, decimal Outstanding, decimal Principal, decimal Profit, decimal LateFees,
        decimal Arrears, int ArrearsInstallments, string ArrearsSince, string OpenedOn, string ContractNo, decimal OriginalInstallment,
        string? StageDue, string StatusChangedAt, string? Region = null, PartyKind Kind = PartyKind.Individual, string? ShortLabel = null);

    private async Task<Case> BuildCaseAsync(CaseSpec s)
    {
        var org = _orgs[s.Org];
        var manager = await db.Memberships.IgnoreQueryFilters().FirstAsync(m => m.UserId == UserId(s.Manager) && m.OrganizationId == org.Id);
        var c = new Case
        {
            OrganizationId = org.Id, Reference = s.Reference, Status = s.Status, StatusChangedAt = At(s.StatusChangedAt),
            City = s.City, Region = s.Region ?? RegionOf(s.City), OpenedOn = D(s.OpenedOn), AssignedManagerId = manager.Id,
            CreatedByUserId = manager.UserId, DraftStep = 6, StageDueOn = s.StageDue is null ? null : D(s.StageDue),
            OutstandingAmount = s.Outstanding, OutstandingAsOf = At("2026-09-22T18:40:00"), OutstandingSource = "نظام التمويل الأساسي",
            ArrearsAmount = s.Arrears, ArrearsInstallments = s.ArrearsInstallments, ArrearsSince = D(s.ArrearsSince),
            CreatedAt = At(s.OpenedOn + "T09:00:00"),
        };
        if (s.Status == CaseStatus.Paused) { c.StatusBeforePause = CaseStatus.Verification; c.PauseReason = "بانتظار مستند من جهة خارجية"; c.SlaPausedAt = c.StatusChangedAt; }
        db.Cases.Add(c);

        var party = factory.NewParty(org.Id, c.Id, PartyRole.OwnerBorrower, s.OwnerFullName, s.NationalId, s.Phone, primary: true, kind: s.Kind);
        party.EmploymentStatus = s.Kind == PartyKind.Individual ? "موظف حكومي" : null;
        db.Parties.Add(party);

        var property = new Property
        {
            OrganizationId = org.Id, CaseId = c.Id, Type = s.PropertyType, City = s.City, District = s.District,
            LandAreaM2 = 450, BuiltAreaM2 = 520, YearBuilt = 2016, Occupancy = OccupancyKind.OwnerFamily,
            ShortLabel = s.ShortLabel ?? $"{s.PropertyType.Split('·')[0].Trim()}، {s.District}، {s.City}",
        };
        factory.SetDeed(property, "31" + s.NationalId[2..9]);
        db.Properties.Add(property);

        db.Mortgages.Add(new Mortgage
        {
            OrganizationId = org.Id, CaseId = c.Id, Mortgagee = org.NameAr, Rank = 1, RegisteredOn = D("2019-05-12"),
            DeedMatched = s.Status >= CaseStatus.Valuation, DeedMatchedOn = s.Status >= CaseStatus.Valuation ? D("2026-08-29") : null,
            VerificationSource = "مطابقة يدوية من القانونية",
            LegalReviewStatus = s.Status >= CaseStatus.ProposedSolution ? LegalReviewStatus.Complete : LegalReviewStatus.Pending,
            LegalReviewedByUserId = s.Status >= CaseStatus.ProposedSolution ? UserId("majed") : null,
            LegalReviewedAt = s.Status >= CaseStatus.ProposedSolution ? At("2026-08-29T12:00:00") : null,
            LegalNote = s.Status >= CaseStatus.ProposedSolution ? "الرهن من الدرجة الأولى ومسجل، ولا قيود أخرى على الصك." : null,
        });

        db.FinancingContracts.Add(new FinancingContract
        {
            OrganizationId = org.Id, CaseId = c.Id, ContractNumber = s.ContractNo, ContractDate = D("2019-05-01"),
            OriginalAmount = Math.Round(s.Principal * 1.35m / 1000m) * 1000m, OriginalTermMonths = 240, RemainingTermMonths = 152,
            OriginalInstallment = s.OriginalInstallment, FirstOverdueDate = D(s.ArrearsSince),
        });

        db.DebtSnapshots.Add(new DebtSnapshot
        {
            OrganizationId = org.Id, CaseId = c.Id, Source = "نظام التمويل الأساسي", AsOf = At("2026-09-22T18:40:00"),
            SyncStatus = SyncStatus.Manual, Principal = s.Principal, Profit = s.Profit, LateFees = s.LateFees, OtherFees = 0,
            Total = s.Principal + s.Profit + s.LateFees, RecordedByUserId = manager.UserId,
        });
        if (s.Principal + s.Profit + s.LateFees != s.Outstanding)
            throw new InvalidOperationException($"Seed debt for {s.Reference} does not add up.");

        // 12-month installment strip: arrears months unpaid.
        var since = D(s.ArrearsSince);
        for (var i = 11; i >= 0; i--)
        {
            var month = new DateOnly(2026, 9, 1).AddMonths(-i);
            var unpaid = month >= new DateOnly(since.Year, since.Month, 1) && month <= new DateOnly(2026, 8, 1) && s.ArrearsInstallments > 0;
            db.InstallmentHistory.Add(new InstallmentHistoryEntry
            {
                OrganizationId = org.Id, CaseId = c.Id, Month = month.ToString("yyyy-MM"),
                Status = month == new DateOnly(2026, 9, 1) ? InstallmentHistoryStatus.Due : unpaid ? InstallmentHistoryStatus.Unpaid : InstallmentHistoryStatus.Paid,
                AmountDue = s.OriginalInstallment, AmountPaid = unpaid || month == new DateOnly(2026, 9, 1) ? 0 : s.OriginalInstallment,
            });
        }

        db.OwnerAccesses.Add(new OwnerAccess
        {
            OrganizationId = org.Id, CaseId = c.Id, PartyId = party.Id,
            InvitationStatus = s.Status >= CaseStatus.Verification ? OwnerInvitationStatus.Accepted : OwnerInvitationStatus.NotSent,
            InvitedAt = s.Status >= CaseStatus.Verification ? At("2026-08-18T10:00:00") : null,
            AcceptedAt = s.Status >= CaseStatus.Verification ? At("2026-08-20T19:30:00") : null,
            IdentityVerifiedAt = s.Status >= CaseStatus.Verification ? At("2026-08-20T19:31:00") : null,
            IdentityMethod = s.Status >= CaseStatus.Verification ? "رابط الدعوة + آخر 4 أرقام من الهوية + رمز جوال" : null,
            ContactHours = "بعد 4 مساءً",
        });
        if (s.Status >= CaseStatus.Verification) { party.IdentityVerifiedAt = At("2026-08-20T19:31:00"); party.IdentityVerifiedVia = "الدعوة ورمز الجوال"; }

        Audit(org.Id, c, "case.transition", "مسودة ← بانتظار البيانات", At(s.OpenedOn + "T09:30:00"), s.Manager,
            from: "draft", to: "awaiting_data", detail: "إنشاء الحالة", reason: "اكتملت البيانات الإلزامية");
        return c;
    }

    private static string RegionOf(string city) => city switch
    {
        "الرياض" or "الخرج" => "الرياض",
        "جدة" or "مكة" or "الطائف" => "مكة المكرمة",
        "الدمام" or "الخبر" or "الأحساء" => "الشرقية",
        "المدينة" => "المدينة المنورة",
        "أبها" or "خميس مشيط" => "عسير",
        _ => "أخرى",
    };

    private async Task<CaseDocument> AddDocumentAsync(Case c, string typeKey, string name, DocumentSource source,
        IReadOnlyList<(string File, string By, string At, ReviewStatus Review, string? Note, string? OwnerReason)> versions,
        DateOnly? validUntil = null, bool visibleToOwner = false, List<string>? visibleTo = null)
    {
        var doc = new CaseDocument
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = typeKey, Name = name, Source = source,
            ValidUntil = validUntil, VisibleToOwner = visibleToOwner, VisibleTo = visibleTo ?? ["case_team"],
        };
        db.Documents.Add(doc);
        var n = 0;
        foreach (var v in versions)
        {
            n++;
            var stored = await storage.SaveAsync(new MemoryStream(DemoPdf($"{c.Reference} - {typeKey} v{n}")), v.File, c.OrganizationId);
            var ver = new DocumentVersion
            {
                OrganizationId = c.OrganizationId, DocumentId = doc.Id, CaseId = c.Id, VersionNo = n, FileName = v.File,
                ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
                UploadedByUserId = _users.TryGetValue(v.By, out var u) ? u.Id : UserId("sara"), UploadedByLabel = LabelOf(v.By),
                UploadedAt = At(v.At), ScanStatus = ScanStatus.Clean, ReviewStatus = v.Review, ReviewNote = v.Note, OwnerFacingReason = v.OwnerReason,
                ReviewedAt = v.Review == ReviewStatus.Pending ? null : At(v.At).AddHours(20),
                ReviewedByUserId = v.Review == ReviewStatus.Pending ? null : UserId("sara"),
            };
            db.DocumentVersions.Add(ver);
            doc.CurrentVersionId = ver.Id;
            doc.VersionCount = n;
            doc.Status = v.Review switch { ReviewStatus.Verified => DocumentStatus.Verified, ReviewStatus.Rejected => DocumentStatus.Rejected, _ => DocumentStatus.InReview };
        }
        return doc;
    }

    private string LabelOf(string by) => by switch
    {
        "owner" => "المالك",
        "core" => "نظام التمويل",
        "valuer" => "مكتب تقييم معتمد «ب»",
        _ => _users.TryGetValue(by, out var u) ? u.FullName : by,
    };

    /// <summary>Minimal valid one-page PDF for fictional demo documents.</summary>
    internal static byte[] DemoPdf(string label)
    {
        var text = $"Rahoon demo document - fictional data - {label}".Replace("(", "[").Replace(")", "]");
        var content = $"BT /F1 12 Tf 50 780 Td ({text}) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    /// <summary>Buffers historical audit events; they are chained in chronological order at the end.</summary>
    private void Audit(Guid orgId, Case? c, string type, string title, DateTimeOffset at, string? actor,
        string? from = null, string? to = null, string? reason = null, string? detail = null, bool blocked = false, List<string>? evidence = null, string? role = null)
    {
        _audit.Add(new AuditEvent
        {
            OrganizationId = orgId, CaseId = c?.Id, CaseReference = c?.Reference, Type = type, Title = title, FromState = from, ToState = to,
            ActorType = actor is null ? "system" : actor == "owner" ? "owner" : actor == "valuer" ? "provider" : "user",
            ActorUserId = actor is not null && _users.TryGetValue(actor, out var u) ? u.Id : null,
            ActorLabel = actor is null ? "النظام" : LabelOf(actor), ActorRole = role ?? RoleLabel(actor),
            Reason = reason, Detail = detail, Blocked = blocked, Evidence = evidence ?? [],
            OccurredAt = new DateTimeOffset(at.UtcTicks / 10 * 10, TimeSpan.Zero), PrevHash = "", Hash = "",
        });
    }

    private string? RoleLabel(string? actor) => actor switch
    {
        "sara" => "مديرة حالات", "fahad" => "محلل ائتمان", "noura" => "معتمدة", "majed" => "القانونية", "reem" => "المالية",
        "khaled" => "موظف حالة", "layla" => "مسؤولة المنشأة", "owner" => "المالك", "valuer" => "مقدم خدمة", _ => null,
    };

    private async Task FlushAuditAsync()
    {
        foreach (var chain in _audit.GroupBy(a => a.OrganizationId))
        {
            var prev = AuditLog.Genesis;
            foreach (var e in chain.OrderBy(a => a.OccurredAt))
            {
                e.PrevHash = prev;
                e.Hash = AuditLog.ComputeHash(e);
                prev = e.Hash;
                db.AuditEvents.Add(e);
            }
        }
        await db.SaveChangesAsync();
    }
}
