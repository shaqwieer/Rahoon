using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>L24 case audit log: filters, blocked attempts, Hijri, chain verification and the signed CSV export.</summary>
[Collection(ApiCollection.Name)]
public sealed class CaseAuditTests(ApiFixture api)
{
    private const string Anchor = "RH-2026-004172";

    [Fact]
    public async Task Audit_log_filters_by_type_actor_and_blocked_with_hijri_dates()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (s, all) = await sara.GetAsync($"/api/cases/{Anchor}/audit?pageSize=2");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(2, all!["items"]!.AsArray().Count);
        Assert.True(all["total"]!.GetValue<int>() > 10);
        var first = all["items"]![0]!;
        Assert.EndsWith("هـ", TestClient.Str(first, "hijri"));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$", TestClient.Str(first, "time"));

        var (_, blocked) = await sara.GetAsync($"/api/cases/{Anchor}/audit?blocked=true");
        var blockedItems = blocked!["items"]!.AsArray();
        Assert.NotEmpty(blockedItems);
        Assert.All(blockedItems, i => Assert.True(i!["blocked"]!.GetValue<bool>()));
        Assert.Contains(blockedItems, i => TestClient.Str(i, "tone") == "err");

        var (_, transitions) = await sara.GetAsync($"/api/cases/{Anchor}/audit?type=transitions,reveal&pageSize=200");
        var types = transitions!["items"]!.AsArray().Select(i => TestClient.Str(i, "type")).ToList();
        Assert.Contains("pii.reveal", types);
        Assert.Contains("case.transition_blocked", types);
        Assert.All(types, t => Assert.True(t.StartsWith("case.transition") || t.StartsWith("case.draft_created") || t.StartsWith("pii."), t));

        var (_, owner) = await sara.GetAsync($"/api/cases/{Anchor}/audit?actor=owner");
        Assert.NotEmpty(owner!["items"]!.AsArray());
        Assert.All(owner["items"]!.AsArray(), i => Assert.Equal("owner", TestClient.Str(i!["actor"], "type")));

        var (_, period) = await sara.GetAsync($"/api/cases/{Anchor}/audit?from=2026-09-02&to=2026-09-02");
        Assert.Contains(period!["items"]!.AsArray(), i => TestClient.Str(i, "type") == "pii.reveal");
        Assert.All(period["items"]!.AsArray(), i => Assert.StartsWith("2026-09-02", TestClient.Str(i, "time")));
    }

    [Fact]
    public async Task Chain_verification_reports_the_result_for_the_case_organization()
    {
        // Sunbula's chain is never touched by other tests.
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        var (_, list) = await maha.GetAsync("/api/cases?view=all&pageSize=1");
        var sunbulaRef = TestClient.Str(list!["items"]![0], "reference");
        var (s, res) = await maha.GetAsync($"/api/cases/{sunbulaRef}/audit/verify");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(res!["ok"]!.GetValue<bool>(), res.ToJsonString());
        Assert.Null(res["firstBrokenSeq"]);

        // For Alufuq the endpoint must agree with a direct recomputation (independent of test order).
        var sara = await B3Scenarios.SaraAsync(api);
        var (_, anchor) = await sara.GetAsync($"/api/cases/{Anchor}/audit/verify");
        var orgId = await api.WithDbAsync(db => db.Cases.Where(c => c.Reference == Anchor).Select(c => c.OrganizationId).FirstAsync());
        var expected = await api.WithDbAsync(db => AuditLog.VerifyChainAsync(db, orgId));
        Assert.Equal(expected is null, anchor!["ok"]!.GetValue<bool>());
        if (expected is null) Assert.StartsWith("السجل للإضافة فقط", TestClient.Str(anchor, "text"));
        else Assert.Equal(expected, anchor["firstBrokenSeq"]!.GetValue<long>());

        Assert.Equal(HttpStatusCode.Forbidden, (await maha.GetAsync($"/api/cases/{Anchor}/audit/verify")).Status);
    }

    [Fact]
    public async Task Export_is_a_csv_with_a_trailing_sha256_over_its_content_and_is_audited()
    {
        var hind = await api.LoginAsync("h.almutairi@alufuq.example"); // compliance: audit.view, not on the case team
        var (s, bytes, type) = await hind.SendRawAsync(HttpMethod.Get, $"/api/cases/{Anchor}/audit/export");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal("text/csv", type);

        var marker = Encoding.UTF8.GetBytes("\n" + CaseAuditEndpoints.ExportHashPrefix);
        var at = bytes.AsSpan().LastIndexOf(marker);
        Assert.True(at > 0);
        var content = bytes[..(at + 1)];
        var line = Encoding.UTF8.GetString(bytes[(at + 1)..]).Trim();
        var hash = line[CaseAuditEndpoints.ExportHashPrefix.Length..];
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(content)), hash);

        var text = Encoding.UTF8.GetString(content);
        Assert.StartsWith("﻿seq,occurred_at_utc", text);
        Assert.Contains("pii.reveal", text);
        Assert.Contains("case.transition_blocked", text);

        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == Anchor && e.Type == "audit.exported" && e.Detail!.Contains(hash))));

        // Filters apply to the export too.
        var (_, filtered, _) = await hind.SendRawAsync(HttpMethod.Get, $"/api/cases/{Anchor}/audit/export?type=reveal");
        var rows = Encoding.UTF8.GetString(filtered).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(rows.Skip(1).Take(rows.Length - 2), r => Assert.Contains("pii.", r));

        var omar = await api.LoginAsync("o.alanazi@valuer-b.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await omar.SendRawAsync(HttpMethod.Get, $"/api/cases/{Anchor}/audit/export")).Status);
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await maha.SendRawAsync(HttpMethod.Get, $"/api/cases/{Anchor}/audit/export")).Status);
    }
}
