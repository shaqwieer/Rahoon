using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Cases;

public sealed record ContractStep(string? ContractNumber, string? ProductType, DateOnly? ContractDate, decimal? OriginalAmount,
    int? OriginalTermMonths, decimal? OriginalInstallment, string? City);
public sealed record PartyInput(Guid? Id, string? Role, string? Kind, string? FullName, string? NationalId, string? Phone, string? Email,
    string? Language, string? SpecialNeeds, string? Relation);
public sealed record PartiesStep(PartyInput Primary, List<PartyInput>? Additional);
public sealed record PropertyStep(string? Type, string? City, string? District, decimal? LandAreaM2, decimal? BuiltAreaM2, int? YearBuilt,
    string? DeedNumber, string? Occupancy, string? Mortgagee, int? Rank, DateOnly? RegisteredOn);
public sealed record DebtStep(decimal? Principal, decimal? Profit, decimal? LateFees, decimal? OtherFees, DateTimeOffset? AsOf, string? Source,
    int? ArrearsInstallments, decimal? ArrearsAmount, DateOnly? ArrearsSince);
public sealed record SubmitDraftRequest(string? DuplicateOverrideReason);

/// <summary>Create-case wizard (L03): six steps, autosaved drafts, strict validation at final creation.</summary>
public static class CaseDraftEndpoints
{
    public static readonly string[] ProductTypes = ["تمويل سكني · مرابحة", "تمويل سكني · إجارة", "تمويل عقاري · تورق", "تمويل عقاري تجاري"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/drafts").RequirePermission(P.CaseCreate);
        g.MapPost("", Create).Idempotent();
        g.MapGet("/{reference}", Get);
        g.MapPut("/{reference}/contract", SaveContract);
        g.MapPut("/{reference}/parties", SaveParties);
        g.MapPut("/{reference}/property", SaveProperty);
        g.MapPut("/{reference}/debt", SaveDebt);
        g.MapPut("/{reference}/step/{step:int}", SetStep);
        g.MapPost("/{reference}/submit", Submit).Idempotent();
        app.MapGet("/api/cases/duplicate-check", DuplicateCheck).RequirePermission(P.CaseCreate);
    }

    private static async Task<Case> LoadDraftAsync(RahoonDbContext db, RequestContext rc, string reference)
    {
        var c = await db.Cases.FirstOrDefaultAsync(x => x.Reference == reference && x.OrganizationId == rc.OrganizationId) ?? throw new CaseForbiddenException();
        if (c.Status != CaseStatus.Draft) throw new ConflictException("not_draft", "هذه الحالة أُنشئت بالفعل ولم تعد مسودة.");
        if (c.CreatedByUserId != rc.UserId && !rc.Has(P.CaseViewAll)) throw new CaseForbiddenException();
        return c;
    }

    private static async Task<IResult> Create(RahoonDbContext db, RequestContext rc, CaseFactory factory, IClock clock, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = new Case
        {
            OrganizationId = rc.OrganizationId!.Value, Reference = await factory.NextReferenceAsync(clock.TodayRiyadh.Year), Status = CaseStatus.Draft,
            StatusChangedAt = clock.UtcNow, CreatedByUserId = rc.UserId, AssignedManagerId = rc.MembershipId, OpenedOn = clock.TodayRiyadh,
        };
        db.Cases.Add(c);
        await db.SaveChangesAsync();
        await audit.RecordAsync(new AuditEntry("case.draft_created", "بدء حالة جديدة (مسودة)", c.Id, c.Reference, ToState: "draft", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { reference = c.Reference });
    }

    private static async Task<IResult> Get(string reference, RahoonDbContext db, RequestContext rc, CaseWorkflow workflow)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        var org = await db.Organizations.FirstAsync(o => o.Id == c.OrganizationId);
        var f = await db.FinancingContracts.FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var parties = await db.Parties.Where(p => p.CaseId == c.Id).OrderByDescending(p => p.IsPrimary).ToListAsync();
        var p = await db.Properties.FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var m = await db.Mortgages.FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var d = await db.DebtSnapshots.FirstOrDefaultAsync(x => x.CaseId == c.Id && x.IsCurrent);
        var docs = await db.Documents.Where(x => x.CaseId == c.Id).Select(x => new { x.Id, x.DocumentTypeKey, x.Name, x.VersionCount, status = x.Status.ToString() }).ToListAsync();
        return Results.Ok(new
        {
            reference = c.Reference, step = c.DraftStep, organization = org.NameAr, savedAt = c.UpdatedAt, duplicateOverrideReason = c.DuplicateOverrideReason,
            productTypes = ProductTypes,
            contract = f is null ? null : new { f.ContractNumber, productType = c.ProductType, f.ContractDate, f.OriginalAmount, f.OriginalTermMonths, f.OriginalInstallment, c.City },
            parties = parties.Select(x => new
            {
                x.Id, role = x.Role.ToString(), kind = x.Kind.ToString(), x.IsPrimary, x.FullName, nationalIdMasked = x.NationalIdMasked, hasNationalId = x.NationalIdEnc != null,
                phoneMasked = x.PhoneMasked, hasPhone = x.PhoneEnc != null, x.Email, language = x.PreferredLanguage, x.SpecialNeeds, x.Relation,
            }),
            property = p is null ? null : new
            {
                p.Type, p.City, p.District, p.LandAreaM2, p.BuiltAreaM2, p.YearBuilt, deedMasked = p.DeedNumberMasked, occupancy = p.Occupancy.ToString(),
                mortgagee = m?.Mortgagee ?? org.NameAr, rank = m?.Rank ?? 1, registeredOn = m?.RegisteredOn,
            },
            debt = d is null ? null : new { d.Principal, d.Profit, d.LateFees, d.OtherFees, d.Total, d.AsOf, d.Source, c.ArrearsInstallments, c.ArrearsAmount, c.ArrearsSince },
            documents = docs,
            missing = await workflow.MissingIntakeAsync(c),
            duplicate = f is null ? null : await DuplicateStatusAsync(db, f.ContractNumber, c.Id),
        });
    }

    private static async Task<IResult> SaveContract(string reference, ContractStep s, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        var v = new Validator();
        var number = s.ContractNumber?.Trim().ToUpperInvariant();
        v.Require(!string.IsNullOrWhiteSpace(number), "contractNumber", "رقم عقد التمويل مطلوب.");
        v.Require(number is null || (number.Length is >= 6 and <= 40 && number.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-')), "contractNumber", "رقم العقد يقبل حروفاً لاتينية وأرقاماً وشرطة فقط.");
        v.Require(s.ProductType is null || ProductTypes.Contains(s.ProductType), "productType", "اختر نوع المنتج من القائمة.");
        v.Require(s.OriginalAmount is null or > 0, "originalAmount", "مبلغ التمويل يجب أن يكون أكبر من صفر.");
        v.Require(s.OriginalTermMonths is null or (> 0 and <= 360), "originalTermMonths", "المدة بين 1 و360 شهراً.");
        v.Require(s.ContractDate is null || s.ContractDate <= clock.TodayRiyadh, "contractDate", "تاريخ العقد لا يكون في المستقبل.");

        if (!string.IsNullOrWhiteSpace(number) && number.Length <= 40)
        {
            var f = await db.FinancingContracts.FirstOrDefaultAsync(x => x.CaseId == c.Id);
            if (f is null) db.FinancingContracts.Add(f = new FinancingContract { OrganizationId = c.OrganizationId, CaseId = c.Id, ContractNumber = number });
            f.ContractNumber = number;
            f.ContractDate = s.ContractDate;
            f.OriginalAmount = s.OriginalAmount;
            f.OriginalTermMonths = s.OriginalTermMonths;
            f.OriginalInstallment = s.OriginalInstallment;
        }
        if (s.ProductType is not null && ProductTypes.Contains(s.ProductType)) c.ProductType = s.ProductType;
        if (!string.IsNullOrWhiteSpace(s.City)) c.City = s.City.Trim();
        await db.SaveChangesAsync();
        return Saved(c, v, number is null ? null : await DuplicateStatusAsync(db, number, c.Id));
    }

    private static async Task<IResult> SaveParties(string reference, PartiesStep s, RahoonDbContext db, RequestContext rc, CaseFactory factory)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        var v = new Validator();
        var existing = await db.Parties.Where(p => p.CaseId == c.Id).ToListAsync();
        var primary = existing.FirstOrDefault(p => p.IsPrimary);
        ValidateParty(v, s.Primary, "primary", primary);
        if (!string.IsNullOrWhiteSpace(s.Primary.FullName))
        {
            if (primary is null)
            {
                primary = factory.NewParty(c.OrganizationId, c.Id, PartyRole.OwnerBorrower, s.Primary.FullName, null, null, primary: true);
                db.Parties.Add(primary);
            }
            ApplyParty(factory, primary, s.Primary);
        }

        var keep = new HashSet<Guid>();
        for (var i = 0; i < (s.Additional?.Count ?? 0); i++)
        {
            var input = s.Additional![i];
            var current = input.Id is { } id ? existing.FirstOrDefault(p => p.Id == id && !p.IsPrimary) : null;
            ValidateParty(v, input, $"additional[{i}]", current, requireContact: false);
            if (string.IsNullOrWhiteSpace(input.FullName)) continue;
            if (current is null)
            {
                current = factory.NewParty(c.OrganizationId, c.Id, ParseRole(input.Role), input.FullName, null, null);
                db.Parties.Add(current);
            }
            ApplyParty(factory, current, input);
            keep.Add(current.Id);
        }
        foreach (var removed in existing.Where(p => !p.IsPrimary && !keep.Contains(p.Id))) db.Parties.Remove(removed);
        await db.SaveChangesAsync();
        return Saved(c, v);
    }

    private static PartyRole ParseRole(string? r) => Enum.TryParse<PartyRole>(r, true, out var role) ? role : PartyRole.CoBorrower;

    private static void ValidateParty(Validator v, PartyInput p, string prefix, CaseParty? existing, bool requireContact = true)
    {
        v.Require(!string.IsNullOrWhiteSpace(p.FullName) && p.FullName.Trim().Length >= 3, $"{prefix}.fullName", "الاسم كما في الهوية مطلوب.");
        var kindOrg = string.Equals(p.Kind, "Organization", StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(p.NationalId))
        {
            var digits = new string(p.NationalId.Where(char.IsDigit).ToArray());
            if (kindOrg) v.Require(digits.Length == 10 && digits[0] == '7', $"{prefix}.nationalId", "رقم السجل التجاري/الموحد 10 أرقام ويبدأ بـ 7.");
            else v.Require(CaseFactory.IsValidNationalId(digits), $"{prefix}.nationalId", digits.Length != 10 ? $"10 أرقام مطلوبة، أُدخل {digits.Length}" : "رقم الهوية يبدأ بـ 1 أو 2.");
        }
        else if (requireContact && existing?.NationalIdEnc is null)
            v.Require(false, $"{prefix}.nationalId", kindOrg ? "رقم السجل مطلوب." : "رقم الهوية الوطنية مطلوب.");

        if (!string.IsNullOrWhiteSpace(p.Phone))
            v.Require(CaseFactory.IsValidSaudiMobile(p.Phone), $"{prefix}.phone", "يجب أن يبدأ بـ 05");
        else if (requireContact && existing?.PhoneEnc is null)
            v.Require(false, $"{prefix}.phone", "رقم الجوال مطلوب.");

        v.Require(string.IsNullOrWhiteSpace(p.Email) || (p.Email.Contains('@') && p.Email.Length <= 254), $"{prefix}.email", "البريد الإلكتروني غير صالح.");
        v.Require(p.Language is null or "ar" or "en", $"{prefix}.language", "اختر لغة التواصل.");
    }

    private static void ApplyParty(CaseFactory factory, CaseParty party, PartyInput p)
    {
        party.FullName = p.FullName!.Trim();
        party.Kind = string.Equals(p.Kind, "Organization", StringComparison.OrdinalIgnoreCase) ? PartyKind.Organization : PartyKind.Individual;
        party.DisplayName = party.Kind == PartyKind.Individual ? Mask.PersonName(party.FullName) : party.FullName;
        if (!party.IsPrimary) party.Role = ParseRole(p.Role);
        var digits = new string((p.NationalId ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 10) factory.SetNationalId(party, digits);
        if (CaseFactory.IsValidSaudiMobile(p.Phone)) factory.SetPhone(party, p.Phone);
        party.Email = string.IsNullOrWhiteSpace(p.Email) ? null : p.Email.Trim();
        party.PreferredLanguage = p.Language is "en" ? "en" : "ar";
        party.SpecialNeeds = string.IsNullOrWhiteSpace(p.SpecialNeeds) || p.SpecialNeeds == "لا يوجد" ? null : p.SpecialNeeds.Trim();
        party.Relation = p.Relation;
    }

    private static async Task<IResult> SaveProperty(string reference, PropertyStep s, RahoonDbContext db, RequestContext rc, CaseFactory factory, IClock clock)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        var v = new Validator();
        v.Require(!string.IsNullOrWhiteSpace(s.Type), "type", "نوع العقار مطلوب.");
        v.Require(!string.IsNullOrWhiteSpace(s.City), "city", "المدينة مطلوبة.");
        v.Require(s.LandAreaM2 is null or > 0, "landAreaM2", "المساحة يجب أن تكون أكبر من صفر.");
        v.Require(s.YearBuilt is null || (s.YearBuilt >= 1950 && s.YearBuilt <= clock.TodayRiyadh.Year), "yearBuilt", "سنة البناء غير صحيحة.");
        var deed = new string((s.DeedNumber ?? "").Where(char.IsDigit).ToArray());
        v.Require(s.DeedNumber is null || deed.Length is >= 6 and <= 14, "deedNumber", "رقم الصك من 6 إلى 14 رقماً.");
        v.Require(s.Rank is null or (>= 1 and <= 5), "rank", "درجة الرهن بين 1 و5.");

        if (!string.IsNullOrWhiteSpace(s.Type) && !string.IsNullOrWhiteSpace(s.City))
        {
            var p = await db.Properties.FirstOrDefaultAsync(x => x.CaseId == c.Id);
            if (p is null) db.Properties.Add(p = new Property { OrganizationId = c.OrganizationId, CaseId = c.Id, Type = s.Type, City = s.City });
            p.Type = s.Type.Trim();
            p.City = s.City.Trim();
            p.District = s.District?.Trim();
            p.LandAreaM2 = s.LandAreaM2;
            p.BuiltAreaM2 = s.BuiltAreaM2;
            p.YearBuilt = s.YearBuilt;
            p.Occupancy = Enum.TryParse<OccupancyKind>(s.Occupancy, true, out var occ) ? occ : OccupancyKind.Unknown;
            p.ShortLabel = $"{p.Type.Split('·')[0].Trim()}، {p.District ?? ""}، {p.City}".Replace("، ،", "،");
            if (deed.Length is >= 6 and <= 14) factory.SetDeed(p, deed);
            c.City ??= p.City;

            var m = await db.Mortgages.FirstOrDefaultAsync(x => x.CaseId == c.Id);
            var org = await db.Organizations.FirstAsync(o => o.Id == c.OrganizationId);
            if (m is null) db.Mortgages.Add(m = new Mortgage { OrganizationId = c.OrganizationId, CaseId = c.Id, Mortgagee = org.NameAr });
            m.Mortgagee = string.IsNullOrWhiteSpace(s.Mortgagee) ? org.NameAr : s.Mortgagee.Trim();
            m.Rank = s.Rank ?? 1;
            m.RegisteredOn = s.RegisteredOn;
        }
        await db.SaveChangesAsync();
        return Saved(c, v);
    }

    private static async Task<IResult> SaveDebt(string reference, DebtStep s, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        var v = new Validator();
        v.Require(s.Principal is > 0, "principal", "أصل الدين مطلوب ويجب أن يكون أكبر من صفر.");
        foreach (var (val, key) in new[] { (s.Profit, "profit"), (s.LateFees, "lateFees"), (s.OtherFees, "otherFees"), (s.ArrearsAmount, "arrearsAmount") })
            v.Require(val is null or >= 0, key, "القيمة لا تكون سالبة.");
        v.Require(s.ArrearsInstallments is null or (>= 0 and <= 360), "arrearsInstallments", "عدد الأقساط المتأخرة غير صحيح.");
        v.Require(s.AsOf is null || s.AsOf <= clock.UtcNow.AddMinutes(5), "asOf", "وقت المصدر لا يكون في المستقبل.");

        if (s.Principal is > 0)
        {
            var d = await db.DebtSnapshots.FirstOrDefaultAsync(x => x.CaseId == c.Id && x.IsCurrent);
            if (d is null) db.DebtSnapshots.Add(d = new DebtSnapshot { OrganizationId = c.OrganizationId, CaseId = c.Id, Source = "إدخال يدوي", RecordedByUserId = rc.UserId });
            d.Principal = Math.Round(s.Principal.Value, 2);
            d.Profit = Math.Round(s.Profit ?? 0, 2);
            d.LateFees = Math.Round(s.LateFees ?? 0, 2);
            d.OtherFees = Math.Round(s.OtherFees ?? 0, 2);
            d.Total = d.Principal + d.Profit + d.LateFees + d.OtherFees;
            d.AsOf = s.AsOf ?? clock.UtcNow;
            d.Source = string.IsNullOrWhiteSpace(s.Source) ? "إدخال يدوي" : s.Source.Trim();
            c.OutstandingAmount = d.Total;
            c.OutstandingAsOf = d.AsOf;
            c.OutstandingSource = d.Source;
            c.ArrearsInstallments = s.ArrearsInstallments;
            c.ArrearsAmount = s.ArrearsAmount;
            c.ArrearsSince = s.ArrearsSince;
        }
        await db.SaveChangesAsync();
        return Saved(c, v);
    }

    private static async Task<IResult> SetStep(string reference, int step, RahoonDbContext db, RequestContext rc)
    {
        var c = await LoadDraftAsync(db, rc, reference);
        c.DraftStep = Math.Clamp(step, 1, 6);
        await db.SaveChangesAsync();
        return Results.Ok(new { step = c.DraftStep, savedAt = c.UpdatedAt });
    }

    private static IResult Saved(Case c, Validator v, object? duplicate = null)
    {
        var errors = v.Errors;
        return Results.Ok(new { savedAt = c.UpdatedAt, errors, duplicate });
    }

    private static async Task<IResult> Submit(string reference, SubmitDraftRequest req, RahoonDbContext db, RequestContext rc, CaseWorkflow workflow, IClock clock)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await LoadDraftAsync(db, rc, reference);
        if (!string.IsNullOrWhiteSpace(req.DuplicateOverrideReason)) c.DuplicateOverrideReason = req.DuplicateOverrideReason.Trim();
        c.OpenedOn = clock.TodayRiyadh;
        c.Source = CaseSource.Manual;
        await workflow.TransitionAsync(c, "finalize_intake", "اكتملت البيانات الإلزامية", CaseStatus.Draft);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { reference = c.Reference, status = CaseStatusInfo.Key(c.Status) });
    }

    private static async Task<IResult> DuplicateCheck(string contract, string? exclude, RahoonDbContext db, RequestContext rc)
    {
        var excludeId = exclude is null ? Guid.Empty : await db.Cases.Where(c => c.Reference == exclude).Select(c => c.Id).FirstOrDefaultAsync();
        return Results.Ok(await DuplicateStatusAsync(db, contract.Trim().ToUpperInvariant(), excludeId));
    }

    /// <summary>none | closed_match (warn, link) | open_match (requires documented reason).</summary>
    public static async Task<object> DuplicateStatusAsync(RahoonDbContext db, string contractNumber, Guid excludeCaseId)
    {
        var matches = await db.FinancingContracts.Where(f => f.ContractNumber == contractNumber && f.CaseId != excludeCaseId)
            .Join(db.Cases, f => f.CaseId, c => c.Id, (f, c) => new { c.Reference, c.Status, c.ClosedAt, c.StatusChangedAt })
            .Where(x => x.Status != CaseStatus.Draft).ToListAsync();
        var open = matches.FirstOrDefault(m => m.Status is not (CaseStatus.Closed or CaseStatus.Cancelled));
        if (open is not null) return new { status = "open_match", caseRef = open.Reference, statusLabel = CaseStatusInfo.Of(open.Status).LabelAr, closedOn = (string?)null };
        var closed = matches.OrderByDescending(m => m.ClosedAt ?? m.StatusChangedAt).FirstOrDefault();
        if (closed is not null) return new { status = "closed_match", caseRef = closed.Reference, statusLabel = CaseStatusInfo.Of(closed.Status).LabelAr, closedOn = (closed.ClosedAt ?? closed.StatusChangedAt).ToString("yyyy-MM-dd") };
        return new { status = "none", caseRef = (string?)null, statusLabel = (string?)null, closedOn = (string?)null };
    }
}
