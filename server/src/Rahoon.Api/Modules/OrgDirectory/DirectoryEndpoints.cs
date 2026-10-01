using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.OrgDirectory;

public sealed record DirectoryInput(
    string? NameAr, string? NameEn, List<string>? Types, string? Website, string? LicenseNumber, string? RegistrationNumber,
    string? SourceName, string? SourceUrl, DateOnly? VerifiedOn, uint? Version);
public sealed record DirectoryActiveInput(string? Reason, uint? Version);

/// <summary>
/// The organization directory. Public lookup for the owner and buyer forms (active records only), and its management by
/// authorised Rahoon administrators (directory.manage): list, search, filter, add, edit, activate and deactivate — never
/// delete. Every administrator change is audited and protects the record from later imports.
/// </summary>
public static class DirectoryEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/directory/organizations", Lookup);

        var g = app.MapGroup("/api/team/directory").RequireOrg(OrganizationKind.Operator).RequirePermission(P.DirectoryManage);
        g.MapGet("", List);
        g.MapGet("/{id:guid}", Get);
        g.MapPost("", Create).Idempotent();
        g.MapPut("/{id:guid}", Update).Idempotent();
        g.MapPost("/{id:guid}/activate", Activate).Idempotent();
        g.MapPost("/{id:guid}/deactivate", Deactivate).Idempotent();
    }

    // ── Lookup for forms ──

    /// <param name="kind">developer → developers; financier → banks and finance companies; or one directory type.</param>
    private static async Task<IResult> Lookup(string? kind, RahoonDbContext db)
    {
        IReadOnlyList<string> types = kind switch
        {
            "developer" or "financier" => OrgTypes.ForObligationKind(kind),
            { } t when OrgTypes.All.Contains(t) => [t],
            _ => OrgTypes.All,
        };
        var wanted = types.ToList();
        var rows = await db.DirectoryOrganizations.AsNoTracking()
            .Where(d => d.Active && d.Types.Any(t => wanted.Contains(t)))
            .OrderBy(d => d.NameAr)
            .Select(d => new { d.Id, d.NameAr, d.NameEn, d.Types, d.Website })
            .ToListAsync();
        return Results.Ok(new { items = rows, total = rows.Count });
    }

    // ── Administration ──

    private static object Dto(DirectoryOrganization d, int? usage = null) => new
    {
        d.Id, d.NameAr, d.NameEn, d.Types, typeLabels = d.Types.Select(t => OrgTypes.Labels.GetValueOrDefault(t, t)),
        d.Website, d.LicenseNumber, d.RegistrationNumber, d.SourceName, d.SourceUrl, d.VerifiedOn, d.Active, d.Origin,
        originLabel = d.Origin == DirectoryOrigins.Import ? "استيراد من مصدر" : "إضافة يدوية",
        d.ImportKeys, d.LastImportedAt, d.AdminEditedAt, adminProtected = d.AdminEditedAt is not null, d.CreatedAt, d.UpdatedAt, d.Version,
        usage,
    };

    private static async Task<IResult> List(string? q, string? type, string? status, string? origin, int? page, RahoonDbContext db)
    {
        var query = db.DirectoryOrganizations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var ar = DirectoryNames.Normalize(q);
            var en = DirectoryNames.NormalizeEnglish(q);
            var raw = q.Trim();
            var lower = raw.ToLowerInvariant();
            query = query.Where(d => (ar.Length > 0 && d.NormalizedNameAr.Contains(ar))
                                     || (en.Length > 0 && d.NormalizedNameEn != null && d.NormalizedNameEn.Contains(en))
                                     || (d.Website != null && d.Website.Contains(lower))
                                     || d.LicenseNumber == raw || d.RegistrationNumber == raw);
        }
        if (type is { Length: > 0 } && OrgTypes.All.Contains(type)) query = query.Where(d => d.Types.Contains(type));
        if (status == "active") query = query.Where(d => d.Active);
        else if (status == "inactive") query = query.Where(d => !d.Active);
        if (origin is DirectoryOrigins.Import or DirectoryOrigins.Manual) query = query.Where(d => d.Origin == origin);

        const int size = 50;
        var total = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var p = Math.Clamp(page ?? 1, 1, pages);
        var items = await query.OrderBy(d => d.NameAr).Skip((p - 1) * size).Take(size).ToListAsync();
        var all = db.DirectoryOrganizations.AsNoTracking();
        var counts = new
        {
            all = await all.CountAsync(),
            active = await all.CountAsync(d => d.Active),
            developer = await all.CountAsync(d => d.Types.Contains(OrgTypes.Developer)),
            bank = await all.CountAsync(d => d.Types.Contains(OrgTypes.Bank)),
            financeCompany = await all.CountAsync(d => d.Types.Contains(OrgTypes.FinanceCompany)),
        };
        return Results.Ok(new { items = items.Select(d => Dto(d)), total, page = p, pages, pageSize = size, counts });
    }

    private static async Task<IResult> Get(Guid id, RahoonDbContext db, RequestContext rc)
    {
        var d = await db.DirectoryOrganizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        int usage;
        using (rc.BeginSystemScope())
            usage = await db.SaleObligations.CountAsync(o => o.PartyId == id) + await db.BuyerRequests.CountAsync(b => b.PreferredFinancierId == id);
        return Results.Ok(Dto(d, usage));
    }

    private sealed record Clean(string NameAr, string? NameEn, List<string> Types, string? Website, string? License, string? Registration,
        string? SourceName, string? SourceUrl, DateOnly? VerifiedOn);

    private static Clean ValidateInput(DirectoryInput req, IClock clock)
    {
        var nameAr = DirectoryNames.Clean(req.NameAr);
        var nameEn = DirectoryNames.Clean(req.NameEn);
        var types = (req.Types ?? []).Distinct().ToList();
        var website = DirectoryNames.Clean(req.Website) is { } w ? DirectoryNames.CleanUrl(w) : null;
        var sourceUrl = DirectoryNames.Clean(req.SourceUrl) is { } su ? DirectoryNames.CleanUrl(su) : null;
        var license = DirectoryNames.Clean(req.LicenseNumber, 60);
        var registration = DirectoryNames.Clean(req.RegistrationNumber, 60);
        new Validator()
            .Require(nameAr is { Length: >= 2 } && DirectoryNames.Normalize(nameAr).Length > 0, "nameAr", "اكتب الاسم العربي للجهة.")
            .Require(types.Count > 0 && types.All(OrgTypes.All.Contains), "types", "اختر نوعًا واحدًا على الأقل.")
            .Require(DirectoryNames.Clean(req.Website) is null || website is not null, "website", "اكتب رابط موقع صحيحًا يبدأ بـ https://")
            .Require(DirectoryNames.Clean(req.SourceUrl) is null || sourceUrl is not null, "sourceUrl", "اكتب رابط المصدر صحيحًا.")
            // A license or registration number is recorded only with the authoritative source that publishes it.
            .Require((license is null && registration is null) || sourceUrl is not null, "sourceUrl", "رقم الترخيص أو السجل يُسجّل فقط مع رابط المصدر الرسمي الذي ينشره.")
            .Require(req.VerifiedOn is null || req.VerifiedOn <= DateOnly.FromDateTime(clock.UtcNow.UtcDateTime.AddDays(1)), "verifiedOn", "تاريخ التحقق لا يكون في المستقبل.")
            .ThrowIfInvalid();
        return new Clean(nameAr!, nameEn, types, website, license, registration, DirectoryNames.Clean(req.SourceName), sourceUrl, req.VerifiedOn);
    }

    private static async Task EnsureUniqueAsync(RahoonDbContext db, string normalized, Guid? except)
    {
        var dup = await db.DirectoryOrganizations.AsNoTracking().FirstOrDefaultAsync(d => d.NormalizedNameAr == normalized && d.Id != except);
        if (dup is not null)
            throw new ConflictException("duplicate", $"الجهة موجودة في الدليل باسم «{dup.NameAr}»{(dup.Active ? "" : " (موقوفة)")}. عدّلها بدل إضافة نسخة ثانية.");
    }

    private static void Apply(DirectoryOrganization d, Clean c)
    {
        d.NameAr = c.NameAr;
        d.NormalizedNameAr = DirectoryNames.Normalize(c.NameAr);
        d.NameEn = c.NameEn;
        d.NormalizedNameEn = c.NameEn is null ? null : DirectoryNames.NormalizeEnglish(c.NameEn);
        d.Types = OrgTypes.All.Where(c.Types.Contains).ToList();
        d.Website = c.Website;
        d.LicenseNumber = c.License;
        d.RegistrationNumber = c.Registration;
        d.SourceName = c.SourceName;
        d.SourceUrl = c.SourceUrl;
        d.VerifiedOn = c.VerifiedOn;
    }

    private static object Snapshot(DirectoryOrganization d) =>
        new { d.NameAr, d.NameEn, d.Types, d.Website, d.LicenseNumber, d.RegistrationNumber, d.SourceName, d.SourceUrl, d.VerifiedOn };

    private static async Task<IResult> Create(DirectoryInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var c = ValidateInput(req, clock);
        await EnsureUniqueAsync(db, DirectoryNames.Normalize(c.NameAr), null);
        var d = new DirectoryOrganization { NameAr = c.NameAr, NormalizedNameAr = "", Origin = DirectoryOrigins.Manual, Active = true };
        Apply(d, c);
        d.AdminEditedAt = clock.UtcNow;
        d.AdminEditedByUserId = rc.UserId;
        db.DirectoryOrganizations.Add(d);
        await audit.RecordAsync(new AuditEntry("directory.created", $"إضافة جهة إلى الدليل: {d.NameAr}", SubjectType: "directory_org",
            SubjectReference: d.Id.ToString(), Data: Snapshot(d)));
        await SaveAsync(db);
        return Results.Ok(Dto(d, 0));
    }

    private static async Task<DirectoryOrganization> LoadForEditAsync(RahoonDbContext db, Guid id, uint? version)
    {
        var d = await db.DirectoryOrganizations.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NotFoundException();
        if (version is { } v && v != d.Version)
            throw new ConflictException("stale", "عُدّلت هذه الجهة بعد فتحها. حدّث الصفحة وأعد التعديل.");
        return d;
    }

    private static async Task<IResult> Update(Guid id, DirectoryInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var c = ValidateInput(req, clock);
        var d = await LoadForEditAsync(db, id, req.Version);
        await EnsureUniqueAsync(db, DirectoryNames.Normalize(c.NameAr), id);
        var before = Snapshot(d);
        Apply(d, c);
        d.AdminEditedAt = clock.UtcNow;
        d.AdminEditedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("directory.updated", $"تعديل جهة في الدليل: {d.NameAr}", SubjectType: "directory_org",
            SubjectReference: d.Id.ToString(), Data: new { before, after = Snapshot(d) }));
        await SaveAsync(db);
        return Results.Ok(Dto(d));
    }

    private static Task<IResult> Activate(Guid id, DirectoryActiveInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock) =>
        SetActiveAsync(id, req, true, db, rc, audit, clock);

    private static Task<IResult> Deactivate(Guid id, DirectoryActiveInput req, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock) =>
        SetActiveAsync(id, req, false, db, rc, audit, clock);

    /// <summary>Deactivated records disappear from the forms; requests that already name them keep the recorded name and id.</summary>
    private static async Task<IResult> SetActiveAsync(Guid id, DirectoryActiveInput req, bool active, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock)
    {
        var reason = req.Reason?.Trim();
        if (!active && reason is not { Length: >= 3 and <= 500 }) Validate.Throw("reason", "اكتب سبب الإيقاف (3 أحرف على الأقل).");
        var d = await LoadForEditAsync(db, id, req.Version);
        if (d.Active == active) return Results.Ok(Dto(d));
        d.Active = active;
        d.AdminEditedAt = clock.UtcNow;
        d.AdminEditedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry(active ? "directory.activated" : "directory.deactivated",
            $"{(active ? "تفعيل" : "إيقاف")} جهة في الدليل: {d.NameAr}", Reason: reason is { Length: > 0 } ? reason : null,
            SubjectType: "directory_org", SubjectReference: d.Id.ToString()));
        await SaveAsync(db);
        return Results.Ok(Dto(d));
    }

    private static async Task SaveAsync(RahoonDbContext db)
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("stale", "عُدّلت هذه الجهة في الوقت نفسه. حدّث الصفحة وأعد التعديل.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new ConflictException("duplicate", "جهة بالاسم نفسه أُضيفت للدليل للتو. حدّث الصفحة.");
        }
    }
}
