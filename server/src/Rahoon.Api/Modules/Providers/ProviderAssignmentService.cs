using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Providers;

public sealed record CreateAssignmentInput(
    Guid ProviderOrganizationId, AssignmentType Type, DateOnly DueOn, IReadOnlyList<string> Scope, IReadOnlyList<Guid> SharedDocumentIds,
    DateTimeOffset? InspectionAt, string? Title = null, string? FeesLabel = null, decimal? FeeAmount = null, Guid? AssigneeUserId = null);

public sealed record ReviewSubmissionInput(string Decision, IReadOnlyList<string>? Notes, DateOnly? ResubmitDueOn, string? Note);

/// <summary>
/// Provider assignments (valuers, brokers): creation by the lender, the provider's access window
/// and the lender's review of submissions. Access rule (A-11, PA12 «التكليف + 7 أيام»): write access
/// until delivery, read-only for 7 days after delivery, then nothing. A return reopens write access
/// and the clock restarts at the next delivery (B7 C1).
///
/// Providers' data organizations include the lender org while any of their assignments there is live,
/// so every provider query must go through <see cref="ProviderVisible"/> — never rely on the EF filter alone.
/// </summary>
public sealed class ProviderAssignmentService(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
{
    public const int ReadOnlyDays = 7;

    /// <summary>Documents carrying debt figures or agreements are never shared with providers, whatever the lender selects.</summary>
    public static readonly IReadOnlySet<string> NonShareableTypes = new HashSet<string>
    {
        "financing_contract", "signed_agreement", "payment_proof", "final_clearance", "lien_release_letter", "sale_consent",
    };

    /// <summary>Shared deeds are shown with the number masked and are not downloadable by providers (no redaction service yet).</summary>
    public const string MaskedDeedType = "title_deed";

    public static string TypeLabel(AssignmentType t) => t switch
    {
        AssignmentType.Valuation => "تقييم",
        AssignmentType.Inspection => "معاينة",
        AssignmentType.Brokerage => "وساطة",
        AssignmentType.Legal => "خدمة قانونية",
        AssignmentType.JudicialSale => "بيع قضائي",
        _ => t.ToString(),
    };

    public static string StatusLabel(AssignmentStatus s) => s switch
    {
        AssignmentStatus.New => "جديد",
        AssignmentStatus.InProgress => "قيد التنفيذ",
        AssignmentStatus.Returned => "أُعيد للتعديل",
        AssignmentStatus.Submitted => "مُسلَّم · قيد المراجعة",
        AssignmentStatus.Accepted => "مقبول",
        AssignmentStatus.Closed => "مغلق",
        AssignmentStatus.Cancelled => "ملغى",
        _ => s.ToString(),
    };

    public static bool IsWritable(AssignmentStatus s) => s is AssignmentStatus.New or AssignmentStatus.InProgress or AssignmentStatus.Returned;

    /// <summary>ASG-YYYY-NNNN from the shared reference counter (atomic upsert).</summary>
    public async Task<string> NextReferenceAsync(int year)
    {
        var key = $"asg:{year}";
        var value = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 1)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        return $"ASG-{year}-{value[0]:D4}";
    }

    // ───────── Lender side ─────────

    public async Task<ProviderAssignment> CreateAsync(Case c, CreateAssignmentInput input)
    {
        var today = clock.TodayRiyadh;
        var provider = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == input.ProviderOrganizationId);
        var docIds = input.SharedDocumentIds.Distinct().ToList();
        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id && docIds.Contains(d.Id)).ToListAsync();
        var types = await db.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Key);
        var scope = input.Scope.Select(s => s?.Trim() ?? "").Where(s => s.Length > 0).ToList();

        var v = new Validator()
            .Require(provider is { Kind: OrganizationKind.ServiceProvider, Status: OrganizationStatus.Active }, "providerOrganizationId", "اختر مقدم خدمة معتمداً ونشطاً.")
            .Require(input.DueOn > today, "dueOn", "موعد التسليم يجب أن يكون بعد اليوم.")
            .Require(input.DueOn <= today.AddDays(90), "dueOn", "موعد التسليم لا يتجاوز 90 يوماً.")
            .Require(scope.Count is > 0 and <= 20, "scope", "حدد نطاق العمل (بند واحد على الأقل، حتى 20 بنداً).")
            .Require(scope.All(s => s.Length <= 200), "scope", "كل بند في نطاق العمل حتى 200 حرف.")
            .Require(docs.Count == docIds.Count, "sharedDocumentIds", "مستند واحد على الأقل لا يتبع هذه الحالة.")
            .Require(docs.All(d => !d.Internal && !NonShareableTypes.Contains(d.DocumentTypeKey) && !(types.GetValueOrDefault(d.DocumentTypeKey)?.Sensitive ?? false)),
                "sharedDocumentIds", "لا تُشارك مع مقدم الخدمة مستندات الهوية أو الدخل أو المديونية أو الاتفاقات.")
            .Require(input.InspectionAt is null || input.InspectionAt > clock.UtcNow, "inspectionAt", "موعد المعاينة يجب أن يكون في المستقبل.")
            .Require(input.FeeAmount is null or >= 0, "feeAmount", "الأتعاب لا تكون سالبة.");
        if (input.AssigneeUserId is { } assignee)
        {
            using var _ = rc.BeginSystemScope();
            var ok = await db.Memberships.AnyAsync(m => m.OrganizationId == input.ProviderOrganizationId && m.UserId == assignee && m.Status == MembershipStatus.Active);
            v.Require(ok, "assigneeUserId", "المُكلَّف ليس عضواً نشطاً لدى مقدم الخدمة.");
        }
        v.ThrowIfInvalid();

        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id);
        var owner = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        var location = string.Join("، ", new[] { property?.District, property?.City ?? c.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var propertyLabel = location.Length > 0 ? location : property?.ShortLabel ?? "—";
        var asg = new ProviderAssignment
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ProviderOrganizationId = provider!.Id, AssigneeUserId = input.AssigneeUserId,
            Reference = await NextReferenceAsync(today.Year), Type = input.Type,
            Title = string.IsNullOrWhiteSpace(input.Title) ? $"{TypeLabel(input.Type)} {property?.Type.Split('·')[0].Trim() ?? "عقار"}" : input.Title.Trim(),
            PropertyLabel = propertyLabel, DueOn = input.DueOn, InspectionAt = input.InspectionAt,
            // Owner reached through the platform only; masked display name (A-10).
            InspectionContact = owner is null ? null : $"{owner.DisplayName} (المالك) عبر المنصة",
            Scope = scope, SharedDocumentIds = docIds, FeesLabel = input.FeesLabel?.Trim(), FeeAmount = input.FeeAmount, CreatedByUserId = rc.UserId,
        };
        db.Assignments.Add(asg);
        await audit.RecordAsync(new AuditEntry("assignment.created", $"تكليف {provider.NameAr} · {asg.Reference}", c.Id, c.Reference,
            Detail: $"{TypeLabel(asg.Type)} · التسليم {asg.DueOn:yyyy-MM-dd} · مستندات مشاركة {docIds.Count}", OrganizationId: c.OrganizationId));
        await NotifyProviderAsync(asg, $"تكليف جديد {asg.Reference}", $"{asg.Title} — {asg.PropertyLabel} · التسليم {asg.DueOn:yyyy-MM-dd}");
        return asg;
    }

    public async Task<AssignmentSubmission> ReviewAsync(Case c, ProviderAssignment asg, int versionNo, ReviewSubmissionInput input)
    {
        var decision = input.Decision?.Trim().ToLowerInvariant();
        var notes = (input.Notes ?? []).Select(n => n?.Trim() ?? "").Where(n => n.Length > 0).ToList();
        var today = clock.TodayRiyadh;
        new Validator()
            .Require(decision is "accept" or "return", "decision", "اختر: قبول التقرير أو إعادته للتعديل.")
            .Require(decision != "return" || notes.Count > 0, "notes", "اكتب ملاحظات الإعادة بنقاط محددة.")
            .Require(decision != "return" || input.ResubmitDueOn > today, "resubmitDueOn", "حدد مهلة إعادة التسليم بعد اليوم.")
            .ThrowIfInvalid();

        var sub = await db.AssignmentSubmissions.FirstOrDefaultAsync(s => s.AssignmentId == asg.Id && s.VersionNo == versionNo) ?? throw new NotFoundException();
        if (sub.Status != SubmissionStatus.Submitted || asg.Status != AssignmentStatus.Submitted)
            throw new ConflictException("already_reviewed", "رُوجع هذا الإصدار مسبقاً أو لم يعد الإصدار الحالي.");
        var latest = await db.AssignmentSubmissions.Where(s => s.AssignmentId == asg.Id).MaxAsync(s => s.VersionNo);
        if (latest != versionNo) throw new ConflictException("stale_version", "يوجد إصدار أحدث من هذا التقرير.");

        var reportVersion = sub.ReportDocumentVersionId is { } rv ? await db.DocumentVersions.FirstOrDefaultAsync(x => x.Id == rv) : null;
        var reportDoc = reportVersion is null ? null : await db.Documents.FirstOrDefaultAsync(d => d.Id == reportVersion.DocumentId);
        var provider = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == asg.ProviderOrganizationId);
        sub.ReviewedByUserId = rc.UserId;
        sub.ReviewedAt = clock.UtcNow;

        if (decision == "accept")
        {
            sub.Status = SubmissionStatus.Accepted;
            asg.Status = AssignmentStatus.Accepted;
            asg.DeliveredAt = sub.SubmittedAt;
            asg.AccessExpiresAt = sub.SubmittedAt.AddDays(ReadOnlyDays);

            var reportDate = DateOnly.FromDateTime(sub.SubmittedAt.ToOffset(TimeSpan.FromHours(3)).DateTime);
            var ruleDays = await db.DocumentRules.Where(r => r.OrganizationId == c.OrganizationId && r.DocumentTypeKey == "valuation_report")
                .Select(r => r.ValidityDays).FirstOrDefaultAsync();
            var validity = Math.Min(ruleDays ?? 90, 90);
            if (asg.Type is AssignmentType.Valuation or AssignmentType.Inspection && sub.MarketValue is > 0)
            {
                foreach (var old in await db.ValuationReports.Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted && r.AssignmentId != asg.Id).ToListAsync())
                    old.Status = ValuationStatus.Superseded;
                var report = await db.ValuationReports.FirstOrDefaultAsync(r => r.AssignmentId == asg.Id);
                if (report is null)
                {
                    report = new ValuationReport { OrganizationId = c.OrganizationId, CaseId = c.Id, AssignmentId = asg.Id, ValuerName = provider.NameAr };
                    db.ValuationReports.Add(report);
                }
                report.VersionNo = sub.VersionNo;
                report.MarketValue = sub.MarketValue!.Value;
                report.RangeLow = sub.RangeLow;
                report.RangeHigh = sub.RangeHigh;
                report.Methodology = sub.Methodology;
                report.ComparablesCount = sub.ComparablesCount;
                report.InspectionDate = sub.InspectionDate;
                report.ReportDate = reportDate;
                report.ValidUntil = reportDate.AddDays(validity);
                report.DocumentVersionId = sub.ReportDocumentVersionId;
                report.Status = ValuationStatus.Accepted;
                report.ReviewedByUserId = rc.UserId;
                report.ReviewedAt = clock.UtcNow;
                report.ReviewNote = input.Note?.Trim();
            }
            if (reportVersion is not null && reportDoc is not null)
            {
                reportVersion.ReviewStatus = ReviewStatus.Verified;
                reportVersion.ReviewedByUserId = rc.UserId;
                reportVersion.ReviewedAt = clock.UtcNow;
                reportDoc.Status = DocumentStatus.Verified;
                reportDoc.ValidUntil = reportDate.AddDays(validity);
            }
            await audit.RecordAsync(new AuditEntry("valuation.accepted", $"قبول تقرير {asg.Reference} v{sub.VersionNo}", c.Id, c.Reference,
                Reason: input.Note, Detail: sub.MarketValue is { } mv ? $"القيمة السوقية {mv:N2} ر.س · وصول المقدم للقراءة حتى {asg.AccessExpiresAt:yyyy-MM-dd}" : null,
                OrganizationId: c.OrganizationId));
            await NotifyProviderAsync(asg, $"قُبل التقرير v{sub.VersionNo} · {asg.Reference}",
                $"شكراً لكم. يبقى التكليف متاحاً للقراءة فقط حتى {asg.AccessExpiresAt!.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} ثم يُسحب الوصول آلياً.", "ok");
        }
        else
        {
            sub.Status = SubmissionStatus.Returned;
            sub.ReturnNotes = notes;
            sub.ResubmitDueOn = input.ResubmitDueOn;
            asg.Status = AssignmentStatus.Returned;
            // A return reopens write access; the 7-day read-only clock restarts at the next delivery (C1).
            asg.DeliveredAt = null;
            asg.AccessExpiresAt = null;
            if (reportVersion is not null && reportDoc is not null)
            {
                reportVersion.ReviewStatus = ReviewStatus.Rejected;
                reportVersion.ReviewedByUserId = rc.UserId;
                reportVersion.ReviewedAt = clock.UtcNow;
                reportVersion.ReviewNote = string.Join(" · ", notes);
                reportDoc.Status = DocumentStatus.Rejected;
            }
            await audit.RecordAsync(new AuditEntry("valuation.returned", $"إعادة تقرير {asg.Reference} v{sub.VersionNo} للتعديل", c.Id, c.Reference,
                Reason: string.Join(" · ", notes), Detail: $"مهلة إعادة التسليم {input.ResubmitDueOn:yyyy-MM-dd}", OrganizationId: c.OrganizationId));
            await NotifyProviderAsync(asg, $"أُعيد التقرير v{sub.VersionNo} للتعديل · {asg.Reference}",
                $"{string.Join(" · ", notes)} — مهلة إعادة التسليم {input.ResubmitDueOn:yyyy-MM-dd}", "warn");
        }
        return sub;
    }

    // ───────── Provider side ─────────

    /// <summary>Assignments the calling provider org may still read: its own, not cancelled, access not expired.</summary>
    public IQueryable<ProviderAssignment> ProviderVisible()
    {
        if (!rc.IsProvider || !rc.Has(P.AssignmentWork)) throw new ForbiddenException();
        var now = clock.UtcNow;
        var org = rc.OrganizationId;
        return db.Assignments.Where(a => a.ProviderOrganizationId == org && a.Status != AssignmentStatus.Cancelled
                                         && (a.AccessExpiresAt == null || a.AccessExpiresAt > now));
    }

    /// <summary>Same refusal whether the assignment is unknown, another provider's, or expired.</summary>
    public async Task<ProviderAssignment> GetForProviderAsync(Guid id, bool track = true)
    {
        var q = ProviderVisible();
        if (!track) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NotFoundException();
    }

    public void EnsureWritable(ProviderAssignment asg)
    {
        if (IsWritable(asg.Status)) return;
        var until = asg.AccessExpiresAt is { } e ? $" حتى {e.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}" : "";
        throw new ConflictException("assignment_read_only", $"سُلِّم هذا التكليف؛ وصولك للقراءة فقط{until} ولا يمكن الإرسال أو التعديل.");
    }

    /// <summary>First provider action moves a new assignment into progress.</summary>
    public void MarkStarted(ProviderAssignment asg)
    {
        if (asg.Status == AssignmentStatus.New) asg.Status = AssignmentStatus.InProgress;
    }

    // ───────── Notifications ─────────

    public async Task NotifyProviderAsync(ProviderAssignment asg, string title, string? body, string tone = "info")
    {
        List<Guid> users;
        using (rc.BeginSystemScope())
        {
            users = asg.AssigneeUserId is { } a
                ? [a]
                : await db.Memberships.Where(m => m.OrganizationId == asg.ProviderOrganizationId && m.Status == MembershipStatus.Active
                                                  && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.AssignmentWork)))
                    .Select(m => m.UserId).ToListAsync();
        }
        foreach (var u in users.Distinct())
            notifier.Notify(u, asg.ProviderOrganizationId, "assignment", title, body, $"/provider/assignments/{asg.Id}", null, tone);
    }

    public void NotifyLender(ProviderAssignment asg, string caseReference, string title, string? body, string tone = "info") =>
        notifier.Notify(asg.CreatedByUserId, asg.OrganizationId, "assignment", title, body, $"/cases/{caseReference}/valuation", asg.CaseId, tone);
}
