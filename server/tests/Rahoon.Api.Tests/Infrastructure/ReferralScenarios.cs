using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Referral;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>Independent referral/closure cases built on top of <see cref="Scenarios"/> (B10).</summary>
public static class ReferralScenarios
{
    public const string Legal = "m.alharbi@alufuq.example";      // ماجد الحربي — القانونية
    public const string Approver = "n.alshehri@alufuq.example";   // نورة الشهري — معتمدة
    public const string Finance = "r.aldosari@alufuq.example";    // ريم الدوسري — المالية (مُعِدّة)
    public const string Checker = "a.alshammari@alufuq.example";  // عبدالعزيز الشمري — المالية (مدقق)
    public const string Agent = "y.alhamdan@agent-j.example";     // ياسر الحمدان — مكتب وكيل البيع «ج»

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

    /// <summary>
    /// «حل مقترح» case whose eight readiness items are all met: one declined offer, written voluntary-sale refusal
    /// recorded by legal, prior notice sent through the API and the objection period moved into the past.
    /// </summary>
    public static async Task<string> ReadyAsync(ApiFixture api, decimal? debt = null)
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        await PlantDeclinedOfferAsync(api, r);
        if (debt is { } d) await SetDebtAsync(api, r, d);
        var majed = await api.LoginAsync(Legal);
        var (s1, b1) = await majed.PostAsync($"/api/cases/{r}/referral/readiness/voluntary_sale_offered/evidence", new { note = "عُرض البيع الطوعي على المالك ورفضه كتابياً." });
        Assert.True(s1 == HttpStatusCode.OK, b1?.ToJsonString());
        var (s2, b2) = await majed.PostAsync($"/api/cases/{r}/referral/notice");
        Assert.True(s2 == HttpStatusCode.OK, b2?.ToJsonString());
        await ElapseObjectionPeriodAsync(api, r);
        return r;
    }

    /// <summary>Ready case with an approved referral decision (legal requests, a different approver decides with MFA).</summary>
    public static async Task<string> ApprovedAsync(ApiFixture api, decimal? debt = null)
    {
        var r = await ReadyAsync(api, debt);
        var majed = await api.LoginAsync(Legal);
        var (s1, b1) = await majed.PostAsync($"/api/cases/{r}/referral/request", new { reason = "استنفاد الحلول الودية ورفض البيع الطوعي كتابياً." });
        Assert.True(s1 == HttpStatusCode.OK, b1?.ToJsonString());
        var noura = await api.LoginAsync(Approver);
        await noura.StepUpAsync();
        var (s2, b2) = await noura.PostAsync($"/api/cases/{r}/referral/decision", new { decision = "approve", reason = "اكتملت الجاهزية وانقضت مهلة الاعتراض دون اعتراض." });
        Assert.True(s2 == HttpStatusCode.OK, b2?.ToJsonString());
        Assert.Equal("judicial_referral", TestClient.Str(b2, "caseStatus"));
        return r;
    }

    public static Task PlantDeclinedOfferAsync(ApiFixture api, string r) => api.WithDbAsync(async db =>
    {
        var c = await db.Cases.FirstAsync(x => x.Reference == r);
        var v = await db.Solutions.OrderBy(s => s.VersionNo).FirstAsync(s => s.CaseId == c.Id);
        db.Offers.Add(new Offer
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, SolutionVersionId = v.Id, VersionNo = v.VersionNo, SentAt = DateTimeOffset.UtcNow.AddDays(-30),
            ValidUntil = Today.AddDays(-20), Status = OfferStatus.Declined, SentByUserId = v.PreparedByUserId, RespondedAt = DateTimeOffset.UtcNow.AddDays(-25),
            DeclineReason = "القسط أعلى من قدرتي.",
        });
        return await db.SaveChangesAsync();
    });

    public static Task SetDebtAsync(ApiFixture api, string r, decimal total) => api.WithDbAsync(async db =>
    {
        var c = await db.Cases.FirstAsync(x => x.Reference == r);
        var snap = await db.DebtSnapshots.FirstAsync(d => d.CaseId == c.Id && d.IsCurrent);
        snap.Principal = total; snap.Profit = 0; snap.LateFees = 0; snap.OtherFees = 0; snap.Total = total;
        c.OutstandingAmount = total;
        return await db.SaveChangesAsync();
    });

    public static Task ElapseObjectionPeriodAsync(ApiFixture api, string r) => api.WithDbAsync(async db =>
    {
        var c = await db.Cases.FirstAsync(x => x.Reference == r);
        var referral = await db.Referrals.FirstAsync(x => x.CaseId == c.Id && x.Status != ReferralStatus.Rejected);
        referral.NoticeSentAt = DateTimeOffset.UtcNow.AddDays(-16);
        referral.ObjectionEndsOn = Today.AddDays(-1);
        return await db.SaveChangesAsync();
    });

    /// <summary>Temporarily adds a role to a seeded user (to prove a separation-of-duties rule rather than a missing permission).</summary>
    public static Task GrantRoleAsync(ApiFixture api, string email, string roleKey) => api.WithDbAsync(async db =>
    {
        var m = await db.Memberships.Include(x => x.Organization).FirstAsync(x => x.User!.Email == email && x.Organization!.ShortCode == "alufuq");
        var role = await db.Roles.FirstAsync(x => x.OrganizationId == m.OrganizationId && x.Key == roleKey);
        if (!await db.MembershipRoles.AnyAsync(x => x.MembershipId == m.Id && x.RoleId == role.Id))
            db.MembershipRoles.Add(new MembershipRole { MembershipId = m.Id, RoleId = role.Id });
        return await db.SaveChangesAsync();
    });

    public static Task RevokeRoleAsync(ApiFixture api, string email, string roleKey) => api.WithDbAsync(async db =>
    {
        var m = await db.Memberships.Include(x => x.Organization).FirstAsync(x => x.User!.Email == email && x.Organization!.ShortCode == "alufuq");
        var role = await db.Roles.FirstAsync(x => x.OrganizationId == m.OrganizationId && x.Key == roleKey);
        db.MembershipRoles.RemoveRange(await db.MembershipRoles.Where(x => x.MembershipId == m.Id && x.RoleId == role.Id).ToListAsync());
        return await db.SaveChangesAsync();
    });
}
