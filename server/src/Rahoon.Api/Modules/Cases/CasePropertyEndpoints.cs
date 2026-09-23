using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Cases;

public sealed record UpdatePropertyRequest(string? Type, string? City, string? District, decimal? LandAreaM2, decimal? BuiltAreaM2, int? YearBuilt,
    string? DeedNumber, string? Occupancy, string? OccupancyNote);
public sealed record MortgageLegalReviewRequest(string? Status, string? Note, bool? DeedMatched, string? OtherEncumbrances, string? VerificationSource);

/// <summary>
/// L08 (mortgage &amp; security, legal) + L09 (property, case team). Legal review completion is the
/// «legal_review_complete» guard of the propose_solution transition.
/// </summary>
public static class CasePropertyEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/property", Get);
        g.MapPatch("/property", Update).RequirePermission(P.CaseEdit);
        // Legal only: agreement.activate is held by the legal role (no dedicated permission key exists).
        g.MapPost("/mortgage/legal-review", LegalReview).RequirePermission(P.AgreementActivate).Idempotent();
    }

    public static string OccupancyLabel(OccupancyKind k) => k switch
    {
        OccupancyKind.OwnerFamily => "يسكنها المالك وأسرته",
        OccupancyKind.Tenant => "مؤجّر لمستأجر",
        OccupancyKind.Vacant => "شاغر",
        _ => "غير معروف",
    };

    private static string RankLabel(int rank) => rank switch
    {
        1 => "الأولى", 2 => "الثانية", 3 => "الثالثة", 4 => "الرابعة", 5 => "الخامسة", _ => rank.ToString(),
    };

    private static async Task<IResult> Get(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var today = clock.TodayRiyadh;
        var p = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var m = await db.Mortgages.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var valuation = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted)
            .OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var photo = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && d.DocumentTypeKey == "property_photos" && d.Status == DocumentStatus.Verified)
            .Select(d => new { d.Id, d.CurrentVersionId, d.Name }).FirstOrDefaultAsync();
        var reviewer = m?.LegalReviewedByUserId is { } rid ? await db.Users.Where(u => u.Id == rid).Select(u => u.FullName).FirstOrDefaultAsync() : null;

        // Inspection: an explicit appointment wins; otherwise the valuation assignment's inspection slot.
        var appt = await db.Appointments.AsNoTracking().Where(a => a.CaseId == c.Id && a.Type == "inspection" && a.Status != AppointmentStatus.Cancelled)
            .OrderByDescending(a => a.StartsAt).FirstOrDefaultAsync();
        var asg = await db.Assignments.AsNoTracking().Where(a => a.CaseId == c.Id && a.InspectionAt != null && a.Status != AssignmentStatus.Cancelled)
            .OrderByDescending(a => a.InspectionAt).FirstOrDefaultAsync();
        object? inspection = null;
        if (appt is not null || asg is not null)
        {
            var at = appt?.StartsAt ?? asg!.InspectionAt!.Value;
            var confirmed = appt is null ? asg!.InspectionConfirmed : appt.Status is AppointmentStatus.Confirmed or AppointmentStatus.Done;
            var done = at < clock.UtcNow && confirmed;
            var attended = appt?.Attendees ?? asg?.InspectionContact;
            var local = at.ToOffset(TimeSpan.FromHours(3));
            inspection = new
            {
                at, attendedBy = attended, status = done ? "completed" : confirmed ? "scheduled" : "proposed",
                statusLabel = done ? "مكتملة" : confirmed ? "مؤكدة" : "مقترحة",
                text = done ? $"تمت {local:yyyy-MM-dd HH:mm}" + (attended is null ? "" : $" بحضور {attended}") : $"موعدها {local:yyyy-MM-dd HH:mm} · {Hijri.Format(DateOnly.FromDateTime(local.DateTime))}",
                providerAssignment = asg?.Reference,
            };
        }

        var pending = m is null || m.LegalReviewStatus != LegalReviewStatus.Complete;
        return Results.Ok(new
        {
            property = p is null ? null : new
            {
                p.Type, p.City, p.District, location = string.Join(" · ", new[] { p.City, p.District }.Where(s => !string.IsNullOrWhiteSpace(s))),
                p.LandAreaM2, p.BuiltAreaM2, p.YearBuilt, deedMasked = p.DeedNumberMasked,
                occupancy = p.Occupancy.ToString(), occupancyLabel = OccupancyLabel(p.Occupancy), p.OccupancyNote, p.ShortLabel,
                valuationValue = valuation?.MarketValue, valuationSource = valuation is null ? null : $"{valuation.ValuerName} · {valuation.ReportDate:yyyy-MM-dd}",
                photo = photo is null ? null : new { documentId = photo.Id, versionId = photo.CurrentVersionId, photo.Name, source = valuation is null ? null : $"من تقرير التقييم v{valuation.VersionNo}" },
                protectionNote = p.Occupancy == OccupancyKind.OwnerFamily
                    ? new { title = "عقار مسكون من المالك وأسرته.", body = "أي زيارة تحتاج موعداً متفقاً عليه عبر المنصة، ولا تُنشر صوره خارج ملف الحالة." }
                    : null,
            },
            mortgage = m is null ? null : new
            {
                m.Mortgagee, m.Rank, rankLabel = RankLabel(m.Rank), m.RegisteredOn,
                otherEncumbrances = string.IsNullOrWhiteSpace(m.OtherEncumbrances) ? "لا توجد" : m.OtherEncumbrances,
                m.InsuranceValidUntil,
                insuranceLabel = m.InsuranceValidUntil is { } iv ? (iv >= today ? $"ساري حتى {iv:yyyy-MM-dd}" : $"منتهٍ منذ {iv:yyyy-MM-dd}") : "غير مسجل",
                insuranceTone = m.InsuranceValidUntil is { } iv2 ? (iv2 < today ? "err" : iv2 <= today.AddDays(30) ? "warn" : "ok") : "neutral",
                m.DeedMatched, m.DeedMatchedOn,
                deedMatchLabel = m.DeedMatched ? $"متحقق {m.DeedMatchedOn:yyyy-MM-dd}" : "غير متحقق",
                m.VerificationSource,
                legalReview = new
                {
                    status = m.LegalReviewStatus == LegalReviewStatus.Complete ? "complete" : "pending",
                    label = m.LegalReviewStatus == LegalReviewStatus.Complete ? "مكتملة" : "معلقة",
                    note = m.LegalNote, reviewer, reviewerRole = reviewer is null ? null : "القانونية", at = m.LegalReviewedAt,
                    footer = reviewer is null || m.LegalReviewedAt is null ? null : $"{reviewer} · القانونية · {m.LegalReviewedAt.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}",
                },
            },
            inspection,
            blocksProposal = pending ? "مراجعة القانونية للرهن لم تكتمل؛ تحجب الانتقال إلى «حل مقترح»." : null,
            canEditProperty = rc.Has(P.CaseEdit) && !CaseStatusInfo.IsTerminal(c.Status),
            canLegalReview = rc.Has(P.AgreementActivate) && m is not null && !CaseStatusInfo.IsTerminal(c.Status),
        });
    }

    private static async Task<IResult> Update(string reference, UpdatePropertyRequest req, CaseAccess access, RahoonDbContext db, CaseFactory factory, IClock clock, AuditLog audit)
    {
        var v = new Validator();
        v.Require(req.Type is null || req.Type.Trim().Length is >= 2 and <= 100, "type", "نوع العقار مطلوب.");
        v.Require(req.City is null || req.City.Trim().Length is >= 2 and <= 60, "city", "المدينة مطلوبة.");
        v.Require(req.District is null || req.District.Trim().Length <= 80, "district", "اسم الحي طويل جداً.");
        v.Require(req.LandAreaM2 is null or > 0, "landAreaM2", "المساحة يجب أن تكون أكبر من صفر.");
        v.Require(req.BuiltAreaM2 is null or > 0, "builtAreaM2", "المساحة يجب أن تكون أكبر من صفر.");
        v.Require(req.YearBuilt is null || (req.YearBuilt >= 1950 && req.YearBuilt <= clock.TodayRiyadh.Year), "yearBuilt", "سنة البناء غير صحيحة.");
        var deed = req.DeedNumber is null ? null : new string(req.DeedNumber.Where(char.IsDigit).ToArray());
        v.Require(deed is null || deed.Length is >= 6 and <= 14, "deedNumber", "رقم الصك من 6 إلى 14 رقماً.");
        OccupancyKind occ = default;
        v.Require(req.Occupancy is null || Enum.TryParse(req.Occupancy, true, out occ), "occupancy", "اختر حالة الإشغال من القائمة.");
        v.Require(req.OccupancyNote is null || req.OccupancyNote.Length <= 500, "occupancyNote", "الملاحظة حتى 500 حرف.");
        v.ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var p = await db.Properties.FirstOrDefaultAsync(x => x.CaseId == c.Id) ?? throw new ConflictException("no_property", "لا توجد بيانات عقار لهذه الحالة بعد.");
        var changed = new List<string>();
        void Set<T>(T? value, T current, Action<T> apply, string label)
        {
            if (value is null || EqualityComparer<T>.Default.Equals(value, current)) return;
            apply(value);
            changed.Add(label);
        }
        Set(req.Type?.Trim(), p.Type, x => p.Type = x, "النوع");
        Set(req.City?.Trim(), p.City, x => p.City = x, "المدينة");
        Set(req.District?.Trim(), p.District, x => p.District = x, "الحي");
        Set(req.LandAreaM2, p.LandAreaM2, x => p.LandAreaM2 = x, "مساحة الأرض");
        Set(req.BuiltAreaM2, p.BuiltAreaM2, x => p.BuiltAreaM2 = x, "مساحة البناء");
        Set(req.YearBuilt, p.YearBuilt, x => p.YearBuilt = x, "سنة البناء");
        if (req.Occupancy is not null && occ != p.Occupancy) { p.Occupancy = occ; changed.Add("الإشغال"); }
        Set(req.OccupancyNote?.Trim(), p.OccupancyNote, x => p.OccupancyNote = x, "ملاحظة الإشغال");
        if (deed is not null)
        {
            factory.SetDeed(p, deed);
            changed.Add("رقم الصك");
            // A new deed number invalidates the deed match and the legal review (they were done on the old deed).
            var m = await db.Mortgages.FirstOrDefaultAsync(x => x.CaseId == c.Id);
            if (m is not null)
            {
                m.DeedMatched = false;
                m.DeedMatchedOn = null;
                m.LegalReviewStatus = LegalReviewStatus.Pending;
            }
        }
        if (changed.Count == 0) return Results.Ok(new { changed });
        if (changed.Any(x => x is "النوع" or "المدينة" or "الحي"))
            p.ShortLabel = $"{p.Type.Split('·')[0].Trim()}، {p.District ?? ""}، {p.City}".Replace("، ،", "،");

        await audit.RecordAsync(new AuditEntry("property.updated", "تعديل بيانات العقار", c.Id, c.Reference,
            Detail: "الحقول: " + string.Join("، ", changed) + (deed is not null ? " · أُعيدت المراجعة القانونية إلى «معلقة» لتغيّر الصك" : ""), OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { changed });
    }

    private static async Task<IResult> LegalReview(string reference, MortgageLegalReviewRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit)
    {
        var status = (req.Status ?? "complete").ToLowerInvariant();
        new Validator()
            .Require(status is "complete" or "pending", "status", "اختر: مكتملة أو معلقة.")
            .Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length is >= 10 and <= 2000, "note", "ملاحظة القانونية مطلوبة (10 أحرف على الأقل).")
            .Require(req.OtherEncumbrances is null || req.OtherEncumbrances.Length <= 500, "otherEncumbrances", "النص حتى 500 حرف.")
            .Require(req.VerificationSource is null || req.VerificationSource.Length <= 200, "verificationSource", "النص حتى 200 حرف.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        CaseStaffing.EnsureNotTerminal(c);
        var m = await db.Mortgages.FirstOrDefaultAsync(x => x.CaseId == c.Id) ?? throw new ConflictException("no_mortgage", "لا توجد بيانات رهن لهذه الحالة.");
        m.LegalReviewStatus = status == "complete" ? LegalReviewStatus.Complete : LegalReviewStatus.Pending;
        m.LegalNote = req.Note!.Trim();
        m.LegalReviewedByUserId = rc.UserId;
        m.LegalReviewedAt = clock.UtcNow;
        if (req.DeedMatched is { } matched && matched != m.DeedMatched)
        {
            m.DeedMatched = matched;
            m.DeedMatchedOn = matched ? clock.TodayRiyadh : null;
        }
        if (req.OtherEncumbrances is not null) m.OtherEncumbrances = req.OtherEncumbrances.Trim();
        if (!string.IsNullOrWhiteSpace(req.VerificationSource)) m.VerificationSource = req.VerificationSource.Trim();
        else if (req.DeedMatched == true) m.VerificationSource ??= "مستند مرفوع + مراجعة القانونية";

        await audit.RecordAsync(new AuditEntry("mortgage.legal_review",
            status == "complete" ? "اكتمال المراجعة القانونية للرهن" : "إعادة المراجعة القانونية للرهن إلى «معلقة»",
            c.Id, c.Reference, Reason: m.LegalNote, Detail: m.DeedMatched ? $"مطابقة الصك: متحقق {m.DeedMatchedOn:yyyy-MM-dd}" : "مطابقة الصك: غير متحقق",
            OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status, reviewedAt = m.LegalReviewedAt });
    }
}
