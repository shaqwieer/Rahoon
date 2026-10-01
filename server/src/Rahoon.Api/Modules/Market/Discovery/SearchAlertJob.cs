using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Market.Discovery;

/// <summary>
/// Saved-search alerts (Phase 2). One run: (1) evaluate every active, alerting, consented search through the discovery spine and
/// queue only what it has not seen — a new opportunity, or a new terms version whose cash due now dropped; (2) deliver queued rows
/// claimed with <c>FOR UPDATE SKIP LOCKED</c>, re-checking at send time that the opportunity is still published on that version and
/// the person still wants the alerts. The unique (search, opportunity, terms version) key makes every run, retry or parallel worker
/// safe against duplicates. In-app delivery is written with its status in one transaction; an SMS is attempted after the row is
/// marked «sending», so a crash can miss an SMS but never send it twice. The SMS gateway is the sandbox (decision D1): it sends
/// nothing and the result is recorded as simulated.
/// </summary>
public sealed class SearchAlertJob(RahoonDbContext db, MarketService market, IClock clock, ILogger<SearchAlertJob> log)
{
    public const int DeliveryBatch = 200;
    /// <summary>A row left «sending» longer than this (crash during an SMS attempt) is closed as failed, never resent.</summary>
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(15);

    public sealed record RunReport(int Searches, int Queued, int Delivered, int Skipped, int Failed);

    public async Task<RunReport> RunOnceAsync(CancellationToken ct = default)
    {
        using var _ = db.Request.BeginSystemScope();
        var (searches, queued) = await EvaluateAsync(ct);
        var (delivered, skipped, failed) = await DeliverAsync(ct);
        failed += await CloseStuckAsync(ct);
        if (queued + delivered + skipped + failed > 0)
            log.LogInformation("Saved-search alerts: {Searches} searches, {Queued} queued, {Delivered} delivered, {Skipped} skipped, {Failed} failed",
                searches, queued, delivered, skipped, failed);
        return new RunReport(searches, queued, delivered, skipped, failed);
    }

    /// <summary>Active = not deleted, not paused, alerts on, consent recorded.</summary>
    public static IQueryable<SavedSearch> Active(RahoonDbContext db) =>
        db.SavedSearches.Where(s => s.DeletedAt == null && s.PausedAt == null && s.AlertsEnabled && s.AlertsConsentAt != null);

    /// <summary>The criteria a saved search runs: its stored query, with «match=me» resolved from its owner's own buyer request.</summary>
    public static async Task<(SearchCriteria Criteria, RankingPreferences? Prefs)> CriteriaAsync(RahoonDbContext db, SavedSearch s, CancellationToken ct = default)
    {
        var c = SearchCriteria.Parse(s.Query) with { Page = 1 };
        if (!c.MatchMe) return (c, null);
        var buyer = await db.BuyerRequests.Where(b => b.ApplicantUserId == s.ApplicantUserId && b.Status != BuyerRequestStatus.Withdrawn && b.Status != BuyerRequestStatus.Rejected)
            .OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync(ct);
        return buyer is null ? (c, null) : (Matching.WithProfile(c, buyer), Matching.Preferences(buyer));
    }

    /// <summary>Current matches of a search (never the person's own listings). Caller supplies the system scope.</summary>
    public static async Task<List<(Guid OpportunityId, Guid TermsId, decimal? DueNow)>> MatchesAsync(RahoonDbContext db, SavedSearch s, CancellationToken ct = default)
    {
        var (c, _) = await CriteriaAsync(db, s, ct);
        var (passing, _) = DiscoveryQuery.Budget(DiscoveryQuery.Attributes(DiscoveryQuery.Published(db), c), c.Capacity);
        var rows = await passing.Where(x => x.O.ApplicantUserId != s.ApplicantUserId)
            .Select(x => new { OpportunityId = x.O.Id, TermsId = x.T.Id, x.T.DueNow }).ToListAsync(ct);
        return rows.Select(r => (r.OpportunityId, r.TermsId, r.DueNow)).ToList();
    }

    /// <summary>Records every current match as seen («baseline», never sent): on save, on turning alerts on and on resume.</summary>
    public static async Task<int> BaselineAsync(RahoonDbContext db, SavedSearch s, IClock clock, CancellationToken ct = default)
    {
        var inserted = 0;
        foreach (var (opp, terms, due) in await MatchesAsync(db, s, ct))
            inserted += await InsertAsync(db, s, opp, terms, due, "baseline", SearchAlertStatus.Baseline, clock, ct);
        return inserted;
    }

    private static Task<int> InsertAsync(RahoonDbContext db, SavedSearch s, Guid opp, Guid terms, decimal? due, string kind, SearchAlertStatus status, IClock clock, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var statusText = status.ToString();
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO market.search_alerts (id, organization_id, created_at, updated_at, saved_search_id, applicant_user_id, opportunity_id, terms_id, due_now, kind, status, attempts)
            VALUES ({Guid.CreateVersion7()}, {s.OrganizationId}, {now}, {now}, {s.Id}, {s.ApplicantUserId}, {opp}, {terms}, {due}, {kind}, {statusText}, 0)
            ON CONFLICT (saved_search_id, opportunity_id, terms_id) DO NOTHING
            """, ct);
    }

    private async Task<(int Searches, int Queued)> EvaluateAsync(CancellationToken ct)
    {
        var searches = await Active(db).AsNoTracking().ToListAsync(ct);
        var queued = 0;
        foreach (var s in searches)
        {
            var seen = (await db.SearchAlerts.AsNoTracking().Where(a => a.SavedSearchId == s.Id)
                    .Select(a => new { a.OpportunityId, a.TermsId, a.DueNow, a.CreatedAt }).ToListAsync(ct))
                .GroupBy(a => a.OpportunityId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.CreatedAt).ToList());
            foreach (var (opp, terms, due) in await MatchesAsync(db, s, ct))
            {
                if (!seen.TryGetValue(opp, out var history))
                {
                    queued += await InsertAsync(db, s, opp, terms, due, "new", SearchAlertStatus.Pending, clock, ct);
                    continue;
                }
                if (history.Any(h => h.TermsId == terms)) continue;
                // A new terms version is worth an alert only when the cash due now dropped (or became known); otherwise it is just recorded.
                var last = history[^1].DueNow;
                var meaningful = due is { } d && (last is null || d < last);
                var inserted = await InsertAsync(db, s, opp, terms, due, "revision", meaningful ? SearchAlertStatus.Pending : SearchAlertStatus.Baseline, clock, ct);
                if (meaningful) queued += inserted;
            }
            await db.SavedSearches.Where(x => x.Id == s.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.LastCheckedAt, clock.UtcNow), ct);
        }
        return (searches.Count, queued);
    }

    private async Task<(int Delivered, int Skipped, int Failed)> DeliverAsync(CancellationToken ct)
    {
        int delivered = 0, skipped = 0, failed = 0;
        var smsBatches = new List<(SavedSearch Search, Guid EventId, List<Guid> Rows, string Body)>();
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var ids = await db.Database.SqlQuery<Guid>($"""
                SELECT id AS "Value" FROM market.search_alerts WHERE status = 'Pending' ORDER BY created_at LIMIT {DeliveryBatch} FOR UPDATE SKIP LOCKED
                """).ToListAsync(ct);
            if (ids.Count == 0) return (0, 0, 0);
            var rows = await db.SearchAlerts.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
            var searchIds = rows.Select(r => r.SavedSearchId).Distinct().ToList();
            var searches = await db.SavedSearches.Where(s => searchIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
            var oppIds = rows.Select(r => r.OpportunityId).Distinct().ToList();
            var opps = await db.Opportunities.Where(o => oppIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
            var now = clock.UtcNow;

            foreach (var group in rows.GroupBy(r => r.SavedSearchId))
            {
                var s = searches[group.Key];
                var optedOut = s.DeletedAt is not null ? "حُذف البحث" : s.PausedAt is not null ? "أوقف التنبيهات" : !s.AlertsEnabled || s.AlertsConsentAt is null ? "التنبيهات غير مفعلة" : null;
                var send = new List<(SearchAlert Row, Opportunity Opp)>();
                foreach (var row in group)
                {
                    row.Attempts++;
                    var o = opps.GetValueOrDefault(row.OpportunityId);
                    var reason = optedOut ?? (o is null || o.Status != OpportunityStatus.Published ? "الفرصة لم تعد منشورة"
                        : o.PublishedTermsId != row.TermsId ? "تغيّرت أرقام الفرصة بعد المطابقة" : null);
                    if (reason is not null) { row.Status = SearchAlertStatus.Skipped; row.Reason = reason; skipped++; continue; }
                    send.Add((row, o!));
                }
                if (send.Count == 0) continue;
                var title = send.Count == 1 ? $"فرصة جديدة تطابق بحثك «{s.Name}»" : $"{send.Count} فرص جديدة تطابق بحثك «{s.Name}»";
                var body = string.Join("\n", send.Select(x => $"{x.Opp.Reference} · {x.Opp.Title}{(x.Row.Kind == "revision" ? " (انخفض المطلوب الآن)" : "")}"));
                var e = market.Event(s.OrganizationId, "saved_search", s.Id, s.ApplicantUserId, "search_alert", title, visible: true, body: body,
                    data: new { search = s.Id, opportunities = send.Select(x => x.Opp.Reference) });
                foreach (var (row, _) in send)
                {
                    row.EventId = e.Id;
                    row.SentAt = now;
                    row.Channel = s.AlertChannel;
                    row.Status = s.AlertChannel == "sms" ? SearchAlertStatus.Sending : SearchAlertStatus.Sent;
                }
                delivered += send.Count;
                if (s.AlertChannel == "sms")
                    smsBatches.Add((s, e.Id, send.Select(x => x.Row.Id).ToList(),
                        send.Count == 1 ? $"رهون: فرصة جديدة {send[0].Opp.Reference} تطابق بحثك «{s.Name}». تفاصيلها في حسابك." : $"رهون: {send.Count} فرص جديدة تطابق بحثك «{s.Name}». تفاصيلها في حسابك."));
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // SMS after the in-app delivery is committed; each row is already «sending», so no later run can pick it again.
        foreach (var (s, eventId, rowIds, body) in smsBatches)
        {
            var result = await market.NotifyBySmsAsync(s.ApplicantUserId, eventId, body);
            var status = result switch { "sent" => SearchAlertStatus.Sent, "simulated" => SearchAlertStatus.Simulated, _ => SearchAlertStatus.Failed };
            if (status == SearchAlertStatus.Failed) failed += rowIds.Count;
            var reason = status == SearchAlertStatus.Failed ? $"تعذّر إرسال الرسالة النصية ({result ?? "unknown"})؛ وصل التنبيه داخل التطبيق" : null;
            await db.SearchAlerts.Where(a => rowIds.Contains(a.Id) && a.Status == SearchAlertStatus.Sending)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, status).SetProperty(a => a.Reason, reason).SetProperty(a => a.UpdatedAt, clock.UtcNow), ct);
        }
        return (delivered, skipped, failed);
    }

    private async Task<int> CloseStuckAsync(CancellationToken ct)
    {
        var before = clock.UtcNow - StuckAfter;
        return await db.SearchAlerts.Where(a => a.Status == SearchAlertStatus.Sending && a.UpdatedAt < before)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.Status, SearchAlertStatus.Failed)
                .SetProperty(a => a.Reason, "انقطع إرسال الرسالة النصية؛ لم يُعَد إرسالها لتجنب التكرار، ووصل التنبيه داخل التطبيق")
                .SetProperty(a => a.UpdatedAt, clock.UtcNow), ct);
    }
}

/// <summary>Runs <see cref="SearchAlertJob"/> on an interval (<c>Alerts:Enabled</c>, <c>Alerts:IntervalSeconds</c>). Off unless enabled.</summary>
public sealed class SearchAlertWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<SearchAlertWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("Alerts:Enabled", false))
        {
            log.LogInformation("Saved-search alerts worker is off (Alerts:Enabled=false): no alerts are evaluated or delivered.");
            return;
        }
        var interval = TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Alerts:IntervalSeconds", 300), 15, 86_400));
        log.LogInformation("Saved-search alerts worker on, every {Seconds}s; SMS goes through the configured gateway (sandbox today).", interval.TotalSeconds);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Min(20, interval.TotalSeconds)), ct);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<SearchAlertJob>().RunOnceAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Saved-search alert run failed; the next run retries (deduplicated by the alert key).");
                }
                await Task.Delay(interval, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
