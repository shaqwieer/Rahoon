using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Complaints;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Administration;

public sealed record PlatformRuleUpdate(string Key, decimal Value);
public sealed record PlatformRulesRequest(List<PlatformRuleUpdate>? Rules, string? Reason);
public sealed record DocumentTypeRequest(string Key, string NameAr, string? NameEn, string? Icon, int? ValidityDays, bool Sensitive, List<string>? AllowedFormats);
public sealed record AuditExportRequest(string? Reason, Guid? OrganizationId, string? Type, DateOnly? From, DateOnly? To);
public sealed record RetentionUpdateRequest(string Period, string? AfterAction, string State, string? LegalReference);

/// <summary>
/// Platform administration PA01–PA05, PA07–PA13. Platform staff see aggregate or anonymous figures only;
/// tenant case data is reachable solely through an approved temporary grant (PA06, <see cref="TempAccessEndpoints"/>).
/// Cross-tenant reads run in a system scope and project counts or metadata only.
/// </summary>
public static partial class PlatformEndpoints
{
    [GeneratedRegex(@"RH-\d{4}-\d{6}")]
    private static partial Regex CaseRefRegex();

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/platform").RequireOrg(OrganizationKind.Platform);
        g.MapGet("/ops", Ops).RequirePermission(P.PlatformOps);
        g.MapGet("/services", Services).RequirePermission(P.PlatformOps);
        g.MapGet("/institutions", Institutions).RequireAnyPermission(P.PlatformInstitutions, P.PlatformOps);
        g.MapGet("/users", Users).RequirePermission(P.PlatformUsers);
        g.MapGet("/permissions", Permissions).RequireAnyPermission(P.PlatformUsers, P.PlatformOps);
        g.MapGet("/defaults/approval-rules", Rules).RequireAnyPermission(P.PlatformDefaults, P.PlatformOps);
        g.MapPut("/defaults/approval-rules", PutRules).RequirePermission(P.PlatformDefaults).Idempotent();
        g.MapGet("/defaults/document-types", DocumentTypes).RequireAnyPermission(P.PlatformDefaults, P.PlatformOps);
        g.MapPost("/defaults/document-types", CreateDocumentType).RequirePermission(P.PlatformDefaults).Idempotent();
        g.MapPut("/defaults/document-types/{key}", UpdateDocumentType).RequirePermission(P.PlatformDefaults).Idempotent();
        g.MapGet("/complaints", Complaints).RequirePermission(P.PlatformComplaints);
        g.MapGet("/audit", AuditEvents).RequirePermission(P.PlatformAudit);
        g.MapPost("/audit/exports", ExportAudit).RequirePermission(P.PlatformAudit);
        g.MapGet("/retention-policies", Retention).RequirePermission(P.PlatformPrivacy);
        g.MapPut("/retention-policies/{id:guid}", PutRetention).RequirePermission(P.PlatformPrivacy).Idempotent();
    }

    // ───────── PA01 / PA13 ─────────

    private static async Task<IResult> Ops(RahoonDbContext db, RequestContext rc, IClock clock, TempAccessService temp)
    {
        await temp.ExpireDueAsync();
        var now = clock.UtcNow;
        var dayStart = new DateTimeOffset(clock.TodayRiyadh.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).ToUniversalTime();
        using var _ = rc.BeginSystemScope();
        var activeTenants = await db.Organizations.CountAsync(o => o.Kind == OrganizationKind.Lender && o.Status == OrganizationStatus.Active);
        var quarterStart = new DateTimeOffset(new DateTime(now.Year, (now.Month - 1) / 3 * 3 + 1, 1), TimeSpan.Zero);
        var newTenants = await db.Organizations.CountAsync(o => o.Kind == OrganizationKind.Lender && o.CreatedAt >= quarterStart);
        var byState = await db.Cases.GroupBy(c => c.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var activeCases = byState.Where(s => !CaseStatusInfo.IsTerminal(s.Key) && s.Key != CaseStatus.Draft).Sum(s => s.N);
        var activeUsers = await db.Sessions.Where(s => s.LastSeenAt >= dayStart && s.Stage == SessionStage.Active).Select(s => s.UserId).Distinct().CountAsync();
        var openTemp = await db.TempAccessRequests.Where(t => t.Status == TempAccessStatus.Active && t.ExpiresAt > now).Select(t => t.ExpiresAt).ToListAsync();
        var pendingTemp = await db.TempAccessRequests.CountAsync(t => t.Status == TempAccessStatus.Pending);
        var pendingApps = await db.InstitutionApplications.CountAsync(a => a.Status == ApplicationStatus.New || a.Status == ApplicationStatus.InReview || a.Status == ApplicationStatus.MoreInfoRequested);
        var escalated = await db.Complaints.CountAsync(c => c.Status == ComplaintStatus.Escalated || c.OwnerReaction == "escalated");
        var queued = await db.OutboundMessages.CountAsync(m => m.Status == OutboundStatus.Queued);
        var failedIntegrations = await db.IntegrationSettings.Where(i => i.State == IntegrationState.Failed).Select(i => i.NameAr).ToListAsync();

        var attention = new List<object>();
        if (queued > 0) attention.Add(new { tone = "warning", icon = "warning", text = $"تأخر الرسائل النصية — {queued} رسالة في الطابور" });
        if (escalated > 0) attention.Add(new { tone = "info", icon = "support_agent", text = $"{escalated} شكاوى مصعّدة للمنصة" });
        if (pendingApps > 0) attention.Add(new { tone = "neutral", icon = "domain", text = $"{pendingApps} طلبات انضمام بانتظار التحقق من الترخيص" });
        if (pendingTemp > 0) attention.Add(new { tone = "warning", icon = "visibility_lock", text = $"{pendingTemp} طلبات وصول مؤقت بانتظار الموافقات" });
        foreach (var f in failedIntegrations) attention.Add(new { tone = "error", icon = "cloud_off", text = $"تعطل تكامل: {f}" });

        return Results.Ok(new
        {
            subtitle = "أرقام مجمعة ومجهولة · لا بيانات حالات",
            kpis = new object[]
            {
                new { key = "active_institutions", label = "منشآت نشطة", value = activeTenants, sub = newTenants > 0 ? $"+{newTenants} هذا الربع" : "—" },
                new { key = "active_cases", label = "حالات نشطة (كل المنشآت)", value = activeCases, sub = "مجمعة" },
                new { key = "active_users_today", label = "مستخدمون نشطون اليوم", value = activeUsers, sub = "—" },
                new { key = "open_temp_access", label = "وصول مؤقت مفتوح", value = openTemp.Count,
                    sub = openTemp.Count == 0 ? "—" : $"ينتهي {openTemp.Min()!.Value.ToOffset(TimeSpan.FromHours(3)):HH:mm}" },
            },
            casesByState = byState.OrderBy(s => s.Key).Select(s => new { status = CaseStatusInfo.Key(s.Key), label = CaseStatusInfo.Of(s.Key).LabelAr, count = s.N }),
            pendingApplications = pendingApps,
            integrations = await db.IntegrationSettings.OrderBy(i => i.NameAr).Select(i => new { i.Key, name = i.NameAr, i.State }).ToListAsync(),
            attention,
        });
    }

    private static async Task<IResult> Services(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var sw = Stopwatch.StartNew();
        var dbUp = false;
        try { dbUp = await db.Database.CanConnectAsync(); } catch { /* reported as down */ }
        var latency = sw.ElapsedMilliseconds;
        using var _ = rc.BeginSystemScope();
        var since = clock.UtcNow.AddHours(-24);
        var outbound = await db.OutboundMessages.Where(m => m.CreatedAt >= since).GroupBy(m => new { m.Channel, m.Status })
            .Select(g => new { g.Key.Channel, g.Key.Status, N = g.Count() }).ToListAsync();
        var beats = await db.Set<JobHeartbeat>().AsNoTracking().ToDictionaryAsync(h => h.Key);
        var integrations = await db.IntegrationSettings.AsNoTracking().OrderBy(i => i.NameAr).ToListAsync();
        string Beat(string key) => beats.TryGetValue(key, out var b) ? $"آخر تشغيل {b.LastRunAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}" : "لم يُسجَّل تشغيل بعد";
        var sms = outbound.Where(o => o.Channel == MessageChannel.Sms).Sum(o => o.N);
        var email = outbound.Where(o => o.Channel == MessageChannel.Email).Sum(o => o.N);
        var rows = new List<object>
        {
            new { key = "database", service = "قاعدة البيانات", state = dbUp ? IntegrationState.Enabled : IntegrationState.Failed, label = dbUp ? "يعمل" : "متوقف", metric = $"زمن الاستجابة {latency} م.ث" },
            new { key = "portals", service = "البوابات والواجهات", state = IntegrationState.Enabled, label = "يعمل", metric = "—" },
            new { key = "documents", service = "تخزين المستندات وفحصها", state = IntegrationState.Enabled, label = "يعمل", metric = "فحص توقيعي أساسي (بيئة تطوير)" },
            new { key = "breach_monitor", service = "مراقبة الإخلال بالأقساط", state = beats.ContainsKey("breach_monitor") ? IntegrationState.Enabled : IntegrationState.Pending,
                label = beats.ContainsKey("breach_monitor") ? "يعمل" : "لم يعمل بعد", metric = Beat("breach_monitor") },
            new { key = "temp_access_expiry", service = "انتهاء الوصول المؤقت", state = beats.ContainsKey("temp_access_expiry") ? IntegrationState.Enabled : IntegrationState.Pending,
                label = beats.ContainsKey("temp_access_expiry") ? "يعمل" : "لم يعمل بعد", metric = Beat("temp_access_expiry") },
        };
        rows.AddRange(integrations.Select(i => (object)new
        {
            key = i.Key, service = i.NameAr, state = i.State,
            label = i.State switch { IntegrationState.Enabled => "يعمل", IntegrationState.Simulated => "بيئة تجريبية", IntegrationState.Pending => "غير مفعّل", IntegrationState.Failed => "متعطل", _ => "غير متاح" },
            metric = i.Key == IntegrationKeys.Sms ? $"{sms} رسالة خلال 24 ساعة · لا تُرسل فعلياً" : i.Key == IntegrationKeys.Email ? $"{email} رسالة خلال 24 ساعة · لا تُرسل فعلياً" : i.Note,
        }));
        return Results.Ok(new
        {
            services = rows,
            outbound = outbound.Select(o => new { channel = o.Channel, status = o.Status, count = o.N }),
            checkedAt = clock.UtcNow,
        });
    }

    // ───────── PA03 / PA04 / PA05 ─────────

    private static async Task<IResult> Institutions(string? kind, RahoonDbContext db, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var k = kind is null ? OrganizationKind.Lender : Enum.TryParse<OrganizationKind>(kind, true, out var parsed) ? parsed : OrganizationKind.Lender;
        var orgs = await db.Organizations.AsNoTracking().Where(o => o.Kind == k).OrderBy(o => o.NameAr).ToListAsync();
        var ids = orgs.Select(o => o.Id).ToList();
        var users = await db.Memberships.Where(m => ids.Contains(m.OrganizationId) && m.Status == MembershipStatus.Active).GroupBy(m => m.OrganizationId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var cases = await db.Cases.Where(c => ids.Contains(c.OrganizationId) && c.Status != CaseStatus.Closed && c.Status != CaseStatus.Cancelled && c.Status != CaseStatus.Draft)
            .GroupBy(c => c.OrganizationId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        return Results.Ok(orgs.Select(o => new
        {
            o.Id, name = o.NameAr, o.NameEn, kind = o.Kind, status = o.Status, o.City, o.ShortCode, o.CreatedAt, domains = o.AllowedEmailDomains,
            activeUsers = users.GetValueOrDefault(o.Id), activeCases = cases.GetValueOrDefault(o.Id), // aggregate counts only
        }));
    }

    private static async Task<IResult> Users(RahoonDbContext db, RequestContext rc)
    {
        var members = await db.Memberships.AsNoTracking().Where(m => m.OrganizationId == rc.OrganizationId)
            .Include(m => m.User).Include(m => m.Roles).ThenInclude(r => r.Role).OrderBy(m => m.CreatedAt).ToListAsync();
        return Results.Ok(new
        {
            roles = SystemRoles.Templates.Where(t => t.Kind == OrganizationKind.Platform).Select(t => new { t.Key, name = t.NameAr, t.Permissions }),
            users = members.Select(m => new
            {
                membershipId = m.Id, name = m.User!.FullName, email = m.User.Email, role = m.Roles.Select(r => r.Role!.NameAr).FirstOrDefault(),
                roleKey = m.Roles.Select(r => r.Role!.Key).FirstOrDefault(), mfa = m.User.MfaEnrolled ? "مفعّل" : "لم يُفعّل",
                status = m.Status, lastLoginAt = m.User.LastLoginAt,
            }),
        });
    }

    private static IResult Permissions() => Results.Ok(new
    {
        count = P.Catalog.Count,
        items = P.Catalog.Select(p => new
        {
            p.Key, name = p.NameAr, p.NameEn, p.Group, sensitivity = p.Sensitivity.ToString().ToLowerInvariant(),
            sensitivityLabel = p.Sensitivity switch { Sensitivity.High => "عالية", Sensitivity.Medium => "متوسطة", _ => "عادية" },
            scope = p.Scope.ToString().ToLowerInvariant(), scopeLabel = p.Scope switch { PermissionScope.Case => "الحالة", PermissionScope.Platform => "المنصة", _ => "المنشأة" },
            condition = p.ConditionAr ?? "—",
        }),
    });

    // ───────── PA07 / PA08 ─────────

    private static async Task<IResult> Rules(RahoonDbContext db) =>
        Results.Ok(new
        {
            subtitle = "قيم أساسية تُنسخ للمنشأة الجديدة، ويمكن للمنشأة تشديدها لا تخفيفها",
            rules = await db.Set<PlatformDefaultRule>().AsNoTracking().OrderBy(r => r.SortOrder)
                .Select(r => new { r.Key, name = r.NameAr, minimum = r.MinimumLabel, r.Note, r.Value, r.Editable, r.UpdatedAt }).ToListAsync(),
            counts = new { documentTypes = await db.DocumentTypes.CountAsync(), baseTemplates = await db.Templates.Where(t => t.OrganizationId == null).Select(t => t.Code).Distinct().CountAsync() },
        });

    private static async Task<IResult> PutRules(PlatformRulesRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var updates = req.Rules ?? [];
        var rules = await db.Set<PlatformDefaultRule>().ToListAsync();
        var v = new Validator()
            .Require(updates.Count > 0, "rules", "لا توجد تعديلات.")
            .Require(!string.IsNullOrWhiteSpace(req.Reason), "reason", "اكتب سبب تعديل الحد الأدنى للمنصة.")
            .Require(updates.All(u => rules.Any(r => r.Key == u.Key && r.Editable)), "rules", "هذه القاعدة ثابتة ولا تُعدّل (فصل المهام والموافقات المزدوجة).");
        foreach (var u in updates)
        {
            if (u.Key == PlatformRuleKeys.MaxWaiverWithoutCommittee) v.Require(u.Value is > 0 and <= 0.5m, "rules", "حد التنازل بين 0 و50%.");
            if (u.Key == PlatformRuleKeys.ValuationMaxValidityDays) v.Require(u.Value is >= 30 and <= 180 && u.Value == Math.Floor(u.Value), "rules", "صلاحية التقييم بين 30 و180 يوماً.");
            if (u.Key == PlatformRuleKeys.OwnerResponseMinDays) v.Require(u.Value is >= 1 and <= 30 && u.Value == Math.Floor(u.Value), "rules", "مهلة رد المالك بين يوم و30 يوماً.");
        }
        v.ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var changes = new List<string>();
        foreach (var u in updates)
        {
            var r = rules.First(x => x.Key == u.Key);
            changes.Add($"{r.NameAr}: {r.Value:0.####}→{u.Value:0.####}");
            r.Value = u.Value;
            r.MinimumLabel = u.Key == PlatformRuleKeys.MaxWaiverWithoutCommittee ? $"{u.Value * 100:0.##}%" : u.Key == PlatformRuleKeys.ValuationMaxValidityDays ? $"≤ {u.Value:0} يوماً" : $"{u.Value:0} أيام";
            r.UpdatedAt = clock.UtcNow;
            r.UpdatedByUserId = rc.UserId;
        }
        await audit.RecordAsync(new AuditEntry("platform.defaults_updated", "تعديل الحدود الدنيا للمنصة", Reason: req.Reason!.Trim(), Detail: string.Join(" · ", changes)));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { saved = true, changes });
    }

    private static async Task<IResult> DocumentTypes(RahoonDbContext db) =>
        Results.Ok(await db.DocumentTypes.AsNoTracking().OrderBy(t => t.NameAr)
            .Select(t => new { t.Key, name = t.NameAr, t.NameEn, t.Icon, t.ValidityDays, t.Sensitive, t.AllowedFormats }).ToListAsync());

    private static void ValidateType(DocumentTypeRequest req)
    {
        new Validator()
            .Require(Regex.IsMatch(req.Key ?? "", "^[a-z][a-z0-9_]{2,48}$"), "key", "المفتاح بحروف لاتينية صغيرة وأرقام وشرطة سفلية.")
            .Require(!string.IsNullOrWhiteSpace(req.NameAr) && req.NameAr.Trim().Length <= 120, "nameAr", "أدخل اسم النوع.")
            .Require(req.ValidityDays is null or > 0 and <= 3650, "validityDays", "الصلاحية بالأيام غير صحيحة.")
            .Require((req.AllowedFormats ?? ["pdf"]).All(f => f is "pdf" or "jpg" or "png"), "allowedFormats", "الصيغ المسموحة: pdf أو jpg أو png.")
            .ThrowIfInvalid();
    }

    private static async Task<IResult> CreateDocumentType(DocumentTypeRequest req, RahoonDbContext db, AuditLog audit)
    {
        ValidateType(req);
        await using var tx = await db.Database.BeginTransactionAsync();
        if (await db.DocumentTypes.AnyAsync(t => t.Key == req.Key)) throw new ConflictException("type_exists", "يوجد نوع بهذا المفتاح.");
        db.DocumentTypes.Add(new DocumentType
        {
            Key = req.Key, NameAr = req.NameAr.Trim(), NameEn = req.NameEn?.Trim(), Icon = req.Icon ?? "description", ValidityDays = req.ValidityDays,
            Sensitive = req.Sensitive, AllowedFormats = req.AllowedFormats ?? ["pdf", "jpg", "png"],
        });
        await audit.RecordAsync(new AuditEntry("platform.document_type_created", $"إضافة نوع مستند: {req.NameAr}", Detail: req.Key));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { req.Key });
    }

    private static async Task<IResult> UpdateDocumentType(string key, DocumentTypeRequest req, RahoonDbContext db, AuditLog audit)
    {
        ValidateType(req with { Key = key });
        await using var tx = await db.Database.BeginTransactionAsync();
        var t = await db.DocumentTypes.FirstOrDefaultAsync(x => x.Key == key) ?? throw new NotFoundException();
        t.NameAr = req.NameAr.Trim();
        t.NameEn = req.NameEn?.Trim();
        t.Icon = req.Icon ?? t.Icon;
        t.ValidityDays = req.ValidityDays;
        t.Sensitive = req.Sensitive;
        t.AllowedFormats = req.AllowedFormats ?? t.AllowedFormats;
        await audit.RecordAsync(new AuditEntry("platform.document_type_updated", $"تعديل نوع مستند: {t.NameAr}", Detail: key));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { key });
    }

    // ───────── PA10 ─────────

    /// <summary>Complaints across tenants: counts and metadata. The subject is shown only under an active temp grant for that case.</summary>
    private static async Task<IResult> Complaints(string? level, RahoonDbContext db, RequestContext rc, IClock clock, TempAccessService temp)
    {
        await temp.ExpireDueAsync();
        var today = clock.TodayRiyadh;
        var now = clock.UtcNow;
        var granted = await db.TempAccessRequests.AsNoTracking()
            .Where(t => t.RequesterUserId == rc.UserId && t.Status == TempAccessStatus.Active && t.ExpiresAt > now).Select(t => t.CaseId).ToListAsync();
        using var _ = rc.BeginSystemScope();
        var all = await db.Complaints.AsNoTracking()
            .Select(c => new { c.Id, c.Reference, c.OrganizationId, c.CaseId, c.Type, c.Status, c.DueOn, c.OwnerReaction, c.SubmittedAt, c.RespondedAt, c.Subject }).ToListAsync();
        var orgs = await db.Organizations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.NameAr);
        bool IsOpen(ComplaintStatus s) => s is not (ComplaintStatus.Resolved or ComplaintStatus.Closed);
        bool IsPlatform(ComplaintStatus s, string? reaction) => s == ComplaintStatus.Escalated || reaction == "escalated";
        var open = all.Where(c => IsOpen(c.Status)).ToList();
        var resolved = all.Where(c => c.RespondedAt != null).Select(c => (c.RespondedAt!.Value - c.SubmittedAt).TotalDays).ToList();
        var shown = level == "all" ? all : all.Where(c => IsPlatform(c.Status, c.OwnerReaction)).ToList();
        return Results.Ok(new
        {
            kpis = new
            {
                open = open.Count, escalated = open.Count(c => IsPlatform(c.Status, c.OwnerReaction)), overdue = open.Count(c => c.DueOn < today),
                averageResolutionDays = resolved.Count == 0 ? (double?)null : Math.Round(resolved.Average(), 1),
            },
            items = shown.OrderBy(c => IsOpen(c.Status) ? 0 : 1).ThenBy(c => c.DueOn).Take(200).Select(c =>
            {
                var left = c.DueOn.DayNumber - today.DayNumber;
                var (text, tone) = !IsOpen(c.Status) ? ("مغلقة", "neutral") : left < 0 ? (CaseDisplay.LateDays(-left), "error") : left <= 2 ? (left == 0 ? "اليوم" : CaseDisplay.Days(left), "warning") : (CaseDisplay.Days(left), "success");
                var visible = granted.Contains(c.CaseId);
                return new
                {
                    c.Reference, organization = orgs.GetValueOrDefault(c.OrganizationId), type = c.Type == ComplaintType.Objection ? "اعتراض" : "شكوى",
                    subject = visible ? c.Subject : null, subjectHidden = !visible, due = text, dueTone = tone,
                    level = IsPlatform(c.Status, c.OwnerReaction) ? "المنصة" : "المنشأة", status = c.Status,
                };
            }),
            note = "بيانات وصفية فقط. الموضوع ونص الشكوى يظهران أثناء وصول مؤقت معتمد للحالة.",
        });
    }

    // ───────── PA11 ─────────

    private static string MaskRefs(string? text) => text is null ? "" : CaseRefRegex().Replace(text, "RH-… (مخفي)");

    private static IQueryable<AuditEvent> Filter(RahoonDbContext db, Guid? organizationId, string? type, DateOnly? from, DateOnly? to, string? q)
    {
        var e = db.AuditEvents.AsNoTracking();
        if (organizationId is { } o) e = e.Where(x => x.OrganizationId == o);
        if (!string.IsNullOrWhiteSpace(type)) e = e.Where(x => x.Type.StartsWith(type));
        if (from is { } f) { var start = new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).ToUniversalTime(); e = e.Where(x => x.OccurredAt >= start); }
        if (to is { } t) { var end = new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(3)).ToUniversalTime(); e = e.Where(x => x.OccurredAt < end); }
        if (!string.IsNullOrWhiteSpace(q)) e = e.Where(x => x.Title.Contains(q) || (x.ActorLabel != null && x.ActorLabel.Contains(q)));
        return e;
    }

    private static async Task<IResult> AuditEvents(Guid? organizationId, string? type, DateOnly? from, DateOnly? to, string? q, long? before, IConfiguration config,
        RahoonDbContext db, RequestContext rc)
    {
        using var _ = rc.BeginSystemScope();
        var query = Filter(db, organizationId, type, from, to, q);
        if (before is { } b) query = query.Where(x => x.Seq < b);
        var events = await query.OrderByDescending(x => x.Seq).Take(100).ToListAsync();
        var orgs = await db.Organizations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.NameAr);
        var salt = PlatformMasking.Salt(config);
        return Results.Ok(new
        {
            items = events.Select(e => new
            {
                e.Seq, at = e.OccurredAt, e.Type, @event = MaskRefs(e.Title), actor = e.ActorLabel, e.ActorRole,
                organization = e.OrganizationId is { } oid ? orgs.GetValueOrDefault(oid) : "المنصة",
                caseId = e.CaseId is { } cid ? Infrastructure.Security.Mask.OpaqueCaseId(cid, salt) : null,
                details = MaskRefs(e.Detail), reason = MaskRefs(e.Reason), e.Blocked, e.Hash, e.PrevHash,
            }),
            next = events.Count == 100 ? events[^1].Seq : (long?)null,
        });
    }

    /// <summary>Signed export: CSV with masked references, its SHA-256 in a header and in the (hash-chained) audit record, with the stated reason.</summary>
    private static async Task<IResult> ExportAudit(AuditExportRequest req, IConfiguration config, RahoonDbContext db, RequestContext rc, AuditLog audit, IClock clock, HttpResponse response)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reason) && req.Reason.Trim().Length >= 5, "reason", "اكتب سبب التصدير (مثل: طلب قانوني).").ThrowIfInvalid();
        List<AuditEvent> events;
        Dictionary<Guid, string> orgs;
        using (rc.BeginSystemScope())
        {
            events = await Filter(db, req.OrganizationId, req.Type, req.From, req.To, null).OrderBy(x => x.Seq).Take(20000).ToListAsync();
            orgs = await db.Organizations.AsNoTracking().ToDictionaryAsync(o => o.Id, o => o.NameAr);
        }
        var salt = PlatformMasking.Salt(config);
        var sb = new StringBuilder("﻿\"seq\",\"at\",\"type\",\"event\",\"actor\",\"organization\",\"case\",\"details\",\"reason\",\"blocked\",\"hash\",\"prev_hash\"\n");
        static string Q(string? s) { s ??= ""; if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s; return "\"" + s.Replace("\"", "\"\"") + "\""; }
        foreach (var e in events)
            sb.AppendLine(string.Join(",", Q(e.Seq.ToString()), Q(e.OccurredAt.ToString("O")), Q(e.Type), Q(MaskRefs(e.Title)), Q(e.ActorLabel),
                Q(e.OrganizationId is { } o ? orgs.GetValueOrDefault(o) : "المنصة"), Q(e.CaseId is { } c ? Infrastructure.Security.Mask.OpaqueCaseId(c, salt) : ""),
                Q(MaskRefs(e.Detail)), Q(MaskRefs(e.Reason)), Q(e.Blocked ? "1" : "0"), Q(e.Hash), Q(e.PrevHash)));
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await audit.RecordAsync(new AuditEntry("audit.exported", "تصدير سجل موقّع", Reason: req.Reason!.Trim(),
                Detail: $"{events.Count} حدث · sha256:{sha}", Data: new { req.OrganizationId, req.Type, req.From, req.To }));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        response.Headers["X-Content-SHA256"] = sha;
        return Results.File(bytes, "text/csv; charset=utf-8", $"audit-{clock.TodayRiyadh:yyyyMMdd}.csv");
    }

    // ───────── PA12 ─────────

    private static async Task<IResult> Retention(RahoonDbContext db) =>
        Results.Ok(new
        {
            subtitle = "كل المدد أدناه افتراضات — تتطلب تأكيداً قانونياً قبل التفعيل",
            items = await db.RetentionPolicies.AsNoTracking().OrderBy(r => r.DataCategory)
                .Select(r => new
                {
                    r.Id, category = r.DataCategory, r.Period, afterAction = r.AfterAction, r.Basis, r.State,
                    stateLabel = r.State == "effective" ? "نافذ" : "مسودة", stateTone = r.State == "effective" ? "success" : "warning", r.LegalReference,
                }).ToListAsync(),
            dataSubjectRequests = new { access = 0, correction = 0, note = "لا يوجد سجل طلبات أصحاب البيانات في المرحلة 1 (يُدار خارج المنصة)." },
            maskingRules = "الهوية: أول رقم وآخر رقمين · الاسم: الأول + حرف · الجوال: آخر رقمين",
        });

    private static async Task<IResult> PutRetention(Guid id, RetentionUpdateRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.Period) && req.Period.Length <= 120, "period", "أدخل مدة الاحتفاظ.")
            .Require(req.State is "draft" or "pending_legal" or "effective", "state", "الحالة: مسودة أو بانتظار التأكيد القانوني أو نافذ.")
            .Require(req.State != "effective" || !string.IsNullOrWhiteSpace(req.LegalReference), "legalReference", "لا تصبح المدة نافذة دون مرجع تأكيد قانوني.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var p = await db.RetentionPolicies.FirstOrDefaultAsync(r => r.Id == id) ?? throw new NotFoundException();
        var before = $"{p.Period} · {p.State}";
        p.Period = req.Period.Trim();
        p.AfterAction = req.AfterAction?.Trim() ?? p.AfterAction;
        p.State = req.State;
        p.LegalReference = req.LegalReference?.Trim();
        p.UpdatedAt = clock.UtcNow;
        p.UpdatedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("platform.retention_updated", $"تعديل سياسة الاحتفاظ: {p.DataCategory}", FromState: before, ToState: $"{p.Period} · {p.State}",
            Reason: p.LegalReference));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { p.Id, p.State });
    }
}
