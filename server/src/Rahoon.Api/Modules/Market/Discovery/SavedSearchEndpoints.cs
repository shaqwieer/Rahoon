using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Integrations;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Market.Discovery;

public sealed record SavedSearchCreate(string? Name, string? Query, bool AlertsEnabled, string? Channel, bool Consent);
public sealed record SavedSearchUpdate(string? Name, bool? AlertsEnabled, string? Channel, bool? Consent, uint? Version);

/// <summary>
/// A signed-in person's saved searches (Phase 2). Only their own rows are ever loaded (applicant filter + explicit owner check), so
/// changing an id in the URL answers 404. Criteria are normalized by <see cref="SearchCriteria"/>; saving the same criteria twice
/// returns the first search. Alerts need consent; the alert job and the worker deliver them.
/// </summary>
public static class SavedSearchEndpoints
{
    public const int MaxLive = 20;
    private static readonly string[] Channels = ["in_app", "sms"];

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/market/my/searches").RequireIndividual();
        g.MapGet("", List);
        g.MapPost("", Create).Idempotent();
        g.MapPut("/{id:guid}", Update);
        g.MapPost("/{id:guid}/pause", Pause).Idempotent();
        g.MapPost("/{id:guid}/resume", Resume).Idempotent();
        g.MapDelete("/{id:guid}", Delete).Idempotent();
    }

    /// <summary>What the person is told about delivery: in-app is real; SMS goes through the sandbox until a provider is contracted (D1).</summary>
    public static object Delivery(IConfiguration config, ISmsGateway sms) => new
    {
        workerEnabled = config.GetValue("Alerts:Enabled", false),
        intervalMinutes = Math.Max(1, config.GetValue("Alerts:IntervalSeconds", 300) / 60),
        inApp = true,
        smsLive = sms is not SandboxSmsGateway,
        smsText = sms is SandboxSmsGateway ? "الرسائل النصية تجريبية حاليًا ولا تُرسل فعليًا؛ يصلك التنبيه داخل حسابك." : null,
    };

    private static async Task<object> Dto(RahoonDbContext db, RequestContext rc, SavedSearch s)
    {
        var c = SearchCriteria.Parse(s.Query);
        int matches;
        using (rc.BeginSystemScope()) matches = (await SearchAlertJob.MatchesAsync(db, s)).Count;
        var alerts = await db.SearchAlerts.Where(a => a.SavedSearchId == s.Id && a.Status != SearchAlertStatus.Baseline && a.Status != SearchAlertStatus.Pending)
            .OrderByDescending(a => a.CreatedAt).Take(5).Select(a => new { a.Status, a.Reason, a.SentAt, a.CreatedAt, a.Kind }).ToListAsync();
        return new
        {
            s.Id, s.Name, s.Query, summary = c.Summary(), url = $"/opportunities?{c.ToQuery(sortAndPage: true)}", s.AlertsEnabled, channel = s.AlertChannel,
            paused = s.PausedAt is not null, s.PausedAt, s.CreatedAt, s.LastCheckedAt, s.Version, currentMatches = matches,
            recentAlerts = alerts.Select(a => new { status = a.Status, a.Reason, a.SentAt, a.CreatedAt, a.Kind }),
        };
    }

    private static async Task<SavedSearch> OwnAsync(RahoonDbContext db, RequestContext rc, Guid id) =>
        await db.SavedSearches.FirstOrDefaultAsync(s => s.Id == id && s.ApplicantUserId == rc.UserId && s.DeletedAt == null) ?? throw new NotFoundException();

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, IConfiguration config, ISmsGateway sms)
    {
        var rows = await db.SavedSearches.Where(s => s.ApplicantUserId == rc.UserId && s.DeletedAt == null).OrderByDescending(s => s.CreatedAt).ToListAsync();
        var items = new List<object>();
        foreach (var s in rows) items.Add(await Dto(db, rc, s));
        return Results.Ok(new { items, max = MaxLive, delivery = Delivery(config, sms) });
    }

    private static string CleanName(string? name, SearchCriteria c)
    {
        var n = name?.Trim() ?? "";
        if (n.Length == 0) n = string.Join(" · ", c.Summary().Take(3));
        return n.Length > 80 ? n[..80] : n;
    }

    private static async Task<IResult> Create(SavedSearchCreate req, RahoonDbContext db, RequestContext rc, MarketService market, IClock clock)
    {
        // Same parser as the list and the map; the page is never saved.
        var c = SearchCriteria.Parse(req.Query ?? "") with { Page = 1 };
        var channel = req.Channel ?? "in_app";
        new Validator()
            .Require(Channels.Contains(channel), "channel", "اختر طريقة التنبيه.")
            .Require(!req.AlertsEnabled || req.Consent, "consent", "لتفعيل التنبيهات، وافق على استلامها.")
            .Require(req.Name is null || req.Name.Trim().Length <= 80, "name", "الاسم أطول من 80 حرفًا.")
            .ThrowIfInvalid();
        var hash = c.Hash();
        var existing = await db.SavedSearches.FirstOrDefaultAsync(s => s.ApplicantUserId == rc.UserId && s.DeletedAt == null && s.QueryHash == hash);
        if (existing is not null) return Results.Ok(new { created = false, search = await Dto(db, rc, existing) });
        if (await db.SavedSearches.CountAsync(s => s.ApplicantUserId == rc.UserId && s.DeletedAt == null) >= MaxLive)
            throw new ConflictException("too_many_searches", $"يمكنك حفظ {MaxLive} عملية بحث على الأكثر. احذف بحثًا لا تحتاجه.");
        var now = clock.UtcNow;
        var s = new SavedSearch
        {
            OrganizationId = await market.OperatorOrgIdAsync(), ApplicantUserId = rc.UserId, Name = CleanName(req.Name, c), Query = c.ToQuery(sortAndPage: true),
            QueryHash = hash, AlertsEnabled = req.AlertsEnabled, AlertChannel = channel, AlertsConsentAt = req.AlertsEnabled ? now : null,
        };
        db.SavedSearches.Add(s);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // A concurrent save of the same criteria: the first one wins.
            db.ChangeTracker.Clear();
            var first = await db.SavedSearches.FirstAsync(x => x.ApplicantUserId == rc.UserId && x.DeletedAt == null && x.QueryHash == hash);
            return Results.Ok(new { created = false, search = await Dto(db, rc, first) });
        }
        if (s.AlertsEnabled) await BaselineAsync(db, rc, s, clock);
        return Results.Ok(new { created = true, search = await Dto(db, rc, s) });
    }

    private static async Task BaselineAsync(RahoonDbContext db, RequestContext rc, SavedSearch s, IClock clock)
    {
        using var _ = rc.BeginSystemScope();
        await SearchAlertJob.BaselineAsync(db, s, clock);
    }

    private static async Task<IResult> Update(Guid id, SavedSearchUpdate req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var s = await OwnAsync(db, rc, id);
        if (req.Version is { } v && v != s.Version) throw new ConflictException("stale", "تغيّر هذا البحث من نافذة أخرى. حدّث الصفحة ثم أعد المحاولة.");
        var channel = req.Channel ?? s.AlertChannel;
        var turningOn = req.AlertsEnabled == true && !s.AlertsEnabled;
        new Validator()
            .Require(Channels.Contains(channel), "channel", "اختر طريقة التنبيه.")
            .Require(!turningOn || req.Consent == true, "consent", "لتفعيل التنبيهات، وافق على استلامها.")
            .Require(req.Name is null || req.Name.Trim().Length is >= 1 and <= 80, "name", "اكتب اسمًا حتى 80 حرفًا.")
            .ThrowIfInvalid();
        if (req.Name is not null) s.Name = req.Name.Trim();
        s.AlertChannel = channel;
        if (req.AlertsEnabled is { } on)
        {
            s.AlertsEnabled = on;
            if (turningOn) s.AlertsConsentAt = clock.UtcNow;
            if (!on) s.AlertsConsentAt = null;
        }
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("stale", "تغيّر هذا البحث من نافذة أخرى. حدّث الصفحة ثم أعد المحاولة."); }
        // Turning alerts on starts from now: what already matches is recorded as seen.
        if (turningOn) await BaselineAsync(db, rc, s, clock);
        return Results.Ok(new { search = await Dto(db, rc, s) });
    }

    private static async Task<IResult> Pause(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var s = await OwnAsync(db, rc, id);
        if (s.PausedAt is null) { s.PausedAt = clock.UtcNow; await db.SaveChangesAsync(); }
        return Results.Ok(new { search = await Dto(db, rc, s) });
    }

    private static async Task<IResult> Resume(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var s = await OwnAsync(db, rc, id);
        if (s.PausedAt is not null)
        {
            s.PausedAt = null;
            await db.SaveChangesAsync();
            // What was published while paused is taken as seen: the person had stopped these alerts.
            if (s.AlertsEnabled) await BaselineAsync(db, rc, s, clock);
        }
        return Results.Ok(new { search = await Dto(db, rc, s) });
    }

    private static async Task<IResult> Delete(Guid id, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var s = await OwnAsync(db, rc, id);
        s.DeletedAt = clock.UtcNow;
        s.AlertsEnabled = false;
        await db.SaveChangesAsync();
        return Results.Ok(new { deleted = true });
    }
}
