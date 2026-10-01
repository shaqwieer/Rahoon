using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.Market;
using Rahoon.Api.Modules.Market.Discovery;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;
using Shape = Rahoon.Api.Tests.Infrastructure.DiscoveryRows.Shape;

namespace Rahoon.Api.Tests;

/// <summary>
/// Phase 2 acceptance 7: the real alert job (the worker is off in Testing; tests call the job). Retry-safe, deduplicated,
/// observes opt-out at send time, never sends for a withdrawn opportunity; SMS through the sandbox only (nothing is sent).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SearchAlertTests(ApiFixture api)
{
    private static readonly Shape Fits = new() { DueNow = 300_000, Total = 900_000, FutureBalance = 600_000, Installment = 5_000, Frequency = "monthly" };

    private async Task<SearchAlertJob.RunReport> RunAsync()
    {
        using var scope = api.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<SearchAlertJob>().RunOnceAsync();
    }

    private Task<List<SearchAlert>> AlertsAsync(Guid searchId) =>
        api.WithDbAsync(db => db.SearchAlerts.Where(a => a.SavedSearchId == searchId).OrderBy(a => a.CreatedAt).ToListAsync());

    private Task<int> InAppAlertsAsync(Guid userId) =>
        api.WithDbAsync(db => db.MarketEvents.CountAsync(e => e.ApplicantUserId == userId && e.Kind == "search_alert" && e.VisibleToApplicant));

    private async Task<(TestClient Client, Guid UserId, Guid SearchId)> SubscribeAsync(string city, string channel = "in_app")
    {
        var c = await SellerAsync(api, name: "مشترك تنبيهات");
        var (_, body) = await Ok(c.PostAsync("/api/market/my/searches", new { name = $"بحث {city}", query = $"city={city}&maxNow=400000", alertsEnabled = true, consent = true, channel }));
        var searchId = Guid.Parse(body!["search"]!["id"]!.GetValue<string>());
        var userId = await api.WithDbAsync(db => db.SavedSearches.Where(s => s.Id == searchId).Select(s => s.ApplicantUserId).FirstAsync());
        return (c, userId, searchId);
    }

    [Fact]
    public async Task Existing_matches_are_a_baseline_and_a_new_opportunity_alerts_once_however_often_the_job_runs()
    {
        const string city = "buraydah";
        var before = await DiscoveryRows.InsertAsync(api, city, Fits);
        var (_, user, search) = await SubscribeAsync(city);
        var baseline = await AlertsAsync(search);
        Assert.Equal(SearchAlertStatus.Baseline, baseline.Single(a => a.OpportunityId == before.Id).Status);

        await RunAsync();
        Assert.Equal(0, await InAppAlertsAsync(user)); // nothing new yet

        var fresh = await DiscoveryRows.InsertAsync(api, city, Fits);
        var dear = await DiscoveryRows.InsertAsync(api, city, Fits with { DueNow = 900_000 });
        await RunAsync();
        await RunAsync();
        await RunAsync();
        var rows = await AlertsAsync(search);
        var alert = rows.Single(a => a.OpportunityId == fresh.Id);
        Assert.Equal(SearchAlertStatus.Sent, alert.Status);
        Assert.Equal("new", alert.Kind);
        Assert.DoesNotContain(rows, a => a.OpportunityId == dear.Id); // outside the budget
        Assert.Equal(1, await InAppAlertsAsync(user));
        var e = await api.WithDbAsync(db => db.MarketEvents.SingleAsync(x => x.ApplicantUserId == user && x.Kind == "search_alert"));
        Assert.Contains(fresh.Reference, e.Body);
        Assert.Equal("saved_search", e.SubjectType);
    }

    [Fact]
    public async Task Parallel_runs_never_alert_twice()
    {
        const string city = "unaizah";
        var (_, user, search) = await SubscribeAsync(city);
        await DiscoveryRows.InsertAsync(api, city, Fits);
        await DiscoveryRows.InsertAsync(api, city, Fits);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RunAsync()));
        var rows = await AlertsAsync(search);
        Assert.Equal(2, rows.Count(a => a.Status == SearchAlertStatus.Sent));
        Assert.Equal(2, rows.Count);
        // One notification per search per run that delivered; never one per worker.
        var events = await api.WithDbAsync(db => db.MarketEvents.Where(x => x.ApplicantUserId == user && x.Kind == "search_alert").ToListAsync());
        Assert.Equal(2, events.Sum(x => x.Body!.Split('\n').Length));
    }

    [Fact]
    public async Task Send_time_checks_skip_withdrawn_opportunities_and_paused_or_deleted_searches()
    {
        const string city = "yanbu";
        var (client, user, search) = await SubscribeAsync(city);
        var gone = await DiscoveryRows.InsertAsync(api, city, Fits);
        // Queue it as the evaluation would, then withdraw before delivery.
        await QueueAsync(search, user, gone);
        await DiscoveryRows.SetStatusAsync(api, gone.Id, OpportunityStatus.Withdrawn);
        await RunAsync();
        var skipped = (await AlertsAsync(search)).Single(a => a.OpportunityId == gone.Id);
        Assert.Equal(SearchAlertStatus.Skipped, skipped.Status);
        Assert.Contains("لم تعد منشورة", skipped.Reason);
        Assert.Equal(0, await InAppAlertsAsync(user));

        // Opt-out: paused → nothing evaluated, and anything queued is skipped.
        await Ok(client.PostAsync($"/api/market/my/searches/{search}/pause"));
        var whilePaused = await DiscoveryRows.InsertAsync(api, city, Fits);
        await RunAsync();
        Assert.DoesNotContain(await AlertsAsync(search), a => a.OpportunityId == whilePaused.Id);
        var queued = await DiscoveryRows.InsertAsync(api, city, Fits);
        await QueueAsync(search, user, queued);
        await RunAsync();
        Assert.Equal(SearchAlertStatus.Skipped, (await AlertsAsync(search)).Single(a => a.OpportunityId == queued.Id).Status);
        // Resume: what appeared while paused is taken as seen, not sent.
        await Ok(client.PostAsync($"/api/market/my/searches/{search}/resume"));
        await RunAsync();
        Assert.Equal(SearchAlertStatus.Baseline, (await AlertsAsync(search)).Single(a => a.OpportunityId == whilePaused.Id).Status);
        Assert.Equal(0, await InAppAlertsAsync(user));

        // Deleted: nothing more.
        await Ok(client.DeleteAsync($"/api/market/my/searches/{search}"));
        await DiscoveryRows.InsertAsync(api, city, Fits);
        await RunAsync();
        Assert.Equal(0, await InAppAlertsAsync(user));
    }

    private Task QueueAsync(Guid search, Guid user, DiscoveryRows.Inserted o) => api.WithDbAsync(async db =>
    {
        var org = await db.SavedSearches.Where(s => s.Id == search).Select(s => s.OrganizationId).FirstAsync();
        db.SearchAlerts.Add(new SearchAlert
        {
            OrganizationId = org, SavedSearchId = search, ApplicantUserId = user, OpportunityId = o.Id, TermsId = o.TermsId, Kind = "new", Status = SearchAlertStatus.Pending,
        });
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task A_new_version_alerts_again_only_when_cash_now_drops()
    {
        const string city = "taif";
        var (_, user, search) = await SubscribeAsync(city);
        var o = await DiscoveryRows.InsertAsync(api, city, Fits);
        await RunAsync();
        Assert.Equal(1, await InAppAlertsAsync(user));

        await DiscoveryRows.NewVersionAsync(api, o.Id, 310_000); // dearer: recorded, not sent
        await RunAsync();
        var rows = await AlertsAsync(search);
        Assert.Equal(2, rows.Count);
        Assert.Equal(SearchAlertStatus.Baseline, rows[^1].Status);
        Assert.Equal(1, await InAppAlertsAsync(user));

        await DiscoveryRows.NewVersionAsync(api, o.Id, 250_000); // cheaper than what was alerted: a revision alert
        await RunAsync();
        rows = await AlertsAsync(search);
        Assert.Equal("revision", rows[^1].Kind);
        Assert.Equal(SearchAlertStatus.Sent, rows[^1].Status);
        Assert.Equal(2, await InAppAlertsAsync(user));
    }

    [Fact]
    public async Task Sms_channel_goes_through_the_sandbox_once_and_a_stuck_attempt_is_never_resent()
    {
        const string city = "jubail";
        var (_, user, search) = await SubscribeAsync(city, channel: "sms");
        var o = await DiscoveryRows.InsertAsync(api, city, Fits);
        var since = DateTimeOffset.UtcNow.AddSeconds(-1);
        await RunAsync();
        await RunAsync();
        var row = (await AlertsAsync(search)).Single(a => a.OpportunityId == o.Id);
        Assert.Equal(SearchAlertStatus.Simulated, row.Status); // sandbox: nothing was sent
        Assert.Equal(1, await InAppAlertsAsync(user));          // the in-app notification is real
        var sms = await api.WithDbAsync(db => db.MarketNotifications.Where(n => n.UserId == user && n.At >= since).ToListAsync());
        Assert.Equal("simulated", Assert.Single(sms).Result);

        // A row left «sending» (crash between claim and gateway) is closed as failed after the timeout and never retried.
        var stuck = await DiscoveryRows.InsertAsync(api, city, Fits);
        await api.WithDbAsync(async db =>
        {
            db.SearchAlerts.Add(new SearchAlert
            {
                OrganizationId = await db.SavedSearches.Where(s => s.Id == search).Select(s => s.OrganizationId).FirstAsync(), SavedSearchId = search, ApplicantUserId = user,
                OpportunityId = stuck.Id, TermsId = stuck.TermsId, Kind = "new", Status = SearchAlertStatus.Sending, Channel = "sms",
            });
            await db.SaveChangesAsync();
            return await db.Database.ExecuteSqlRawAsync("UPDATE market.search_alerts SET updated_at = now() - interval '1 hour' WHERE opportunity_id = {0}", stuck.Id);
        });
        await RunAsync();
        await RunAsync();
        var closed = (await AlertsAsync(search)).Single(a => a.OpportunityId == stuck.Id);
        Assert.Equal(SearchAlertStatus.Failed, closed.Status);
        Assert.Single(await api.WithDbAsync(db => db.MarketNotifications.Where(n => n.UserId == user && n.At >= since).ToListAsync()));
    }
}
