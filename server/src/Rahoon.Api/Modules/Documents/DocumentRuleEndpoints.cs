using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Documents;

public sealed record DocumentRuleRequest(
    string DocumentTypeKey, string? RequiredBeforeStatus, string Uploader, int? ValidityDays, string? ValidityNote, List<string>? VisibleTo, bool OwnerSummaryOnly);

/// <summary>A03 document rules per institution. Validity may tighten the platform catalog, never exceed it (valuation ≤ 90 days, PA07).</summary>
public static class DocumentRuleEndpoints
{
    public static readonly IReadOnlyDictionary<string, string> Uploaders = new Dictionary<string, string>
    {
        ["lender"] = "المصرف", ["owner"] = "المالك", ["provider"] = "مقدم الخدمة", ["owner_or_finance"] = "المالك أو المالية", ["legal"] = "القانونية",
    };

    public static readonly IReadOnlyDictionary<string, string> Audiences = new Dictionary<string, string>
    {
        ["case_team"] = "فريق الحالة", ["legal"] = "القانونية", ["approver"] = "المعتمد", ["finance"] = "المالية", ["owner"] = "المالك", ["provider"] = "مقدم الخدمة",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/settings/document-rules").RequireOrg(OrganizationKind.Lender).RequirePermission(P.OrgSettings);
        g.MapGet("", List);
        g.MapPost("", Create).Idempotent();
        g.MapPut("/{id:guid}", Update).Idempotent();
        g.MapDelete("/{id:guid}", Delete);
    }

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, PlatformMinima minima)
    {
        var types = await db.DocumentTypes.AsNoTracking().ToDictionaryAsync(t => t.Key);
        var rules = await db.DocumentRules.AsNoTracking().Where(r => r.OrganizationId == rc.OrganizationId).ToListAsync();
        return Results.Ok(new
        {
            valuationMaxDays = await minima.ValuationMaxValidityDaysAsync(),
            uploaders = Uploaders.Select(u => new { key = u.Key, label = u.Value }),
            audiences = Audiences.Select(a => new { key = a.Key, label = a.Value }),
            types = types.Values.OrderBy(t => t.NameAr).Select(t => new { t.Key, name = t.NameAr, maxValidityDays = t.ValidityDays }),
            items = rules.OrderBy(r => r.RequiredBeforeStatus is null).ThenBy(r => r.RequiredBeforeStatus).Select(r => new
            {
                r.Id, r.DocumentTypeKey, type = types.GetValueOrDefault(r.DocumentTypeKey)?.NameAr ?? r.DocumentTypeKey,
                r.RequiredBeforeStatus,
                requiredBefore = r.RequiredBeforeStatus is { } s && Enum.TryParse<CaseStatus>(s, out var cs) ? CaseStatusInfo.Of(cs).LabelAr : "حسب الحاجة",
                r.Uploader, uploaderLabel = Uploaders.GetValueOrDefault(r.Uploader, r.Uploader),
                r.ValidityDays, validity = r.ValidityNote ?? (r.ValidityDays is { } d ? $"{d} يوماً" : "—"),
                r.VisibleTo, visibleLabel = string.Join("، ", r.VisibleTo.Select(v => v == "owner" && r.OwnerSummaryOnly ? "المالك (ملخص)" : Audiences.GetValueOrDefault(v, v))),
                r.OwnerSummaryOnly,
            }),
        });
    }

    private static async Task ValidateAsync(DocumentRuleRequest req, RahoonDbContext db, PlatformMinima minima)
    {
        var type = await db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Key == req.DocumentTypeKey);
        var max = req.DocumentTypeKey == "valuation_report" ? await minima.ValuationMaxValidityDaysAsync() : type?.ValidityDays;
        var visible = req.VisibleTo ?? [];
        new Validator()
            .Require(type is not null, "documentTypeKey", "اختر نوع مستند من كتالوج المنصة.")
            .Require(req.RequiredBeforeStatus is null || Enum.TryParse<CaseStatus>(req.RequiredBeforeStatus, out _), "requiredBeforeStatus", "اختر مرحلة صحيحة أو «حسب الحاجة».")
            .Require(Uploaders.ContainsKey(req.Uploader ?? ""), "uploader", "اختر الجهة التي ترفع المستند.")
            .Require(req.ValidityDays is null or > 0, "validityDays", "الصلاحية بالأيام أكبر من صفر.")
            .Require(req.ValidityDays is null || max is null || req.ValidityDays <= max, "validityDays", $"الصلاحية لا تتجاوز حد المنصة ({max} يوماً).")
            .Require(visible.Count > 0 && visible.All(Audiences.ContainsKey), "visibleTo", "حدد من يرى المستند.")
            .Require(!req.OwnerSummaryOnly || visible.Contains("owner"), "ownerSummaryOnly", "ملخص المالك يتطلب أن يرى المالك المستند.")
            .ThrowIfInvalid();
    }

    private static async Task<IResult> Create(DocumentRuleRequest req, RahoonDbContext db, RequestContext rc, PlatformMinima minima, AuditLog audit)
    {
        await ValidateAsync(req, db, minima);
        await using var tx = await db.Database.BeginTransactionAsync();
        if (await db.DocumentRules.AnyAsync(r => r.OrganizationId == rc.OrganizationId && r.DocumentTypeKey == req.DocumentTypeKey))
            throw new ConflictException("rule_exists", "توجد قاعدة لهذا النوع؛ عدّلها بدلاً من إضافة أخرى.");
        var rule = new DocumentRule { OrganizationId = rc.OrganizationId!.Value, DocumentTypeKey = req.DocumentTypeKey, Uploader = req.Uploader };
        Apply(rule, req);
        db.DocumentRules.Add(rule);
        await audit.RecordAsync(new AuditEntry("config.document_rule_created", $"إضافة قاعدة مستند: {req.DocumentTypeKey}", Detail: Describe(rule)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { rule.Id });
    }

    private static async Task<IResult> Update(Guid id, DocumentRuleRequest req, RahoonDbContext db, RequestContext rc, PlatformMinima minima, AuditLog audit)
    {
        await ValidateAsync(req, db, minima);
        await using var tx = await db.Database.BeginTransactionAsync();
        var rule = await db.DocumentRules.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (rule.DocumentTypeKey != req.DocumentTypeKey) Validate.Throw("documentTypeKey", "لا يتغير نوع المستند في قاعدة قائمة.");
        var before = Describe(rule);
        Apply(rule, req);
        await audit.RecordAsync(new AuditEntry("config.document_rule_updated", $"تعديل قاعدة مستند: {rule.DocumentTypeKey}", FromState: before, ToState: Describe(rule)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { rule.Id });
    }

    private static async Task<IResult> Delete(Guid id, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var rule = await db.DocumentRules.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        db.DocumentRules.Remove(rule);
        await audit.RecordAsync(new AuditEntry("config.document_rule_deleted", $"حذف قاعدة مستند: {rule.DocumentTypeKey}", Detail: Describe(rule)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { deleted = true });
    }

    private static void Apply(DocumentRule rule, DocumentRuleRequest req)
    {
        rule.RequiredBeforeStatus = req.RequiredBeforeStatus;
        rule.Uploader = req.Uploader;
        rule.ValidityDays = req.ValidityDays;
        rule.ValidityNote = string.IsNullOrWhiteSpace(req.ValidityNote) ? null : req.ValidityNote.Trim();
        rule.VisibleTo = (req.VisibleTo ?? []).Distinct().ToList();
        rule.OwnerSummaryOnly = req.OwnerSummaryOnly;
    }

    private static string Describe(DocumentRule r) =>
        $"قبل {r.RequiredBeforeStatus ?? "حسب الحاجة"} · يرفعه {r.Uploader} · صلاحية {r.ValidityDays?.ToString() ?? "—"} · يراه {string.Join(",", r.VisibleTo)}";
}
