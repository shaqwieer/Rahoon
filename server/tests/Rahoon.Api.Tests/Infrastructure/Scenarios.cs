using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Documents;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>Builds independent cases so tests never depend on each other's order.</summary>
public static class Scenarios
{
    private static int _seq;

    public static string NextContract() => $"MF-SC-{Interlocked.Increment(ref _seq):D4}{Random.Shared.Next(100, 999)}";

    /// <summary>
    /// Wizard-created case moved through verification and valuation to «حل مقترح», with an analyst-prepared
    /// v1 handed over for review. Provider-delivered artifacts (valuation, legal note, verified documents)
    /// are placed directly in the database.
    /// </summary>
    public static async Task<string> ReadyForApprovalAsync(ApiFixture api, decimal waiver = 0, int term = 84)
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var r = await WorkflowTests.CreateCaseAsync(sara, NextContract());
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_verification", expectedStatus = "awaiting_data" })).Status);

        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            foreach (var (key, name) in new[] { ("title_deed", "صك الملكية"), ("national_id", "صورة الهوية الوطنية"), ("financing_contract", "عقد التمويل") })
                db.Documents.Add(new CaseDocument { OrganizationId = c.OrganizationId, CaseId = c.Id, DocumentTypeKey = key, Name = name, Status = DocumentStatus.Verified, Source = DocumentSource.Lender });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_valuation", expectedStatus = "verification" })).Status);

        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.ValuationReports.Add(new ValuationReport
            {
                OrganizationId = c.OrganizationId, CaseId = c.Id, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = 1_000_000m,
                ReportDate = today.AddDays(-3), ValidUntil = today.AddDays(87), Status = ValuationStatus.Accepted,
            });
            var m = await db.Mortgages.FirstAsync(x => x.CaseId == c.Id);
            m.LegalReviewStatus = LegalReviewStatus.Complete;
            db.Analyses.Add(new AffordabilityAnalysis { OrganizationId = c.OrganizationId, CaseId = c.Id, NetMonthlyIncome = 25_000m, IncomeSourceLabel = "كشف الراتب", Completed = true });
            return await db.SaveChangesAsync();
        });

        var fahad = await api.LoginAsync("f.alotaibi@alufuq.example");
        Assert.Equal(HttpStatusCode.OK, (await fahad.PostAsync($"/api/cases/{r}/transitions", new { action = "propose_solution", expectedStatus = "valuation" })).Status);
        var (sCreate, created) = await fahad.PostAsync($"/api/cases/{r}/solutions");
        Assert.Equal(HttpStatusCode.OK, sCreate);
        var n = created!["version"]!.GetValue<int>();
        var first = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(2);
        var (sUpd, upd) = await fahad.PutAsync($"/api/cases/{r}/solutions/{n}", new
        {
            kind = "Reschedule", termMonths = term, firstDueDate = first.ToString("yyyy-MM-dd"), waiverAmount = waiver, downPayment = 0, graceMonths = 0,
            justification = "تمديد المدة يخفض القسط ضمن حد الاستقطاع وفق الدخل المتحقق.",
        });
        Assert.True(sUpd == HttpStatusCode.OK, upd?.ToJsonString());
        Assert.Equal(HttpStatusCode.OK, (await fahad.PostAsync($"/api/cases/{r}/solutions/{n}/handover")).Status);
        return r;
    }

    /// <summary>Submits the reviewed v1 as the case manager; returns the approval request id.</summary>
    public static async Task SubmitAsync(ApiFixture api, string r)
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", "مصرف الأفق");
        var (s, b) = await sara.PostAsync($"/api/cases/{r}/solutions/1/submit", new { note = "راجعت الحل والأدلة، والقسط ضمن حد الاستقطاع.", attested = true, expectedStatus = "proposed_solution" });
        Assert.True(s == HttpStatusCode.OK, b?.ToJsonString());
    }

    /// <summary>Owner sign-in for a wizard-created case: issue an invitation via the DB (lender invite endpoint is separate).</summary>
    public static async Task<TestClient> OwnerForNewCaseAsync(ApiFixture api, string r)
    {
        var token = $"test-{r}";
        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == r);
            var party = await db.Parties.FirstAsync(p => p.CaseId == c.Id && p.IsPrimary);
            var existing = await db.OwnerAccesses.FirstOrDefaultAsync(o => o.CaseId == c.Id);
            if (existing is null)
                db.OwnerAccesses.Add(new OwnerAccess
                {
                    OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, InvitationTokenHash = Rahoon.Api.Infrastructure.Security.Tokens.Sha256(token),
                    InvitationStatus = OwnerInvitationStatus.Sent, InvitationExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                });
            return await db.SaveChangesAsync();
        });
        var owner = api.Client();
        var (_, sent) = await owner.PostAsync("/api/auth/owner/verify-id", new { token, idLast4 = "5678" }); // wizard ID 1012345678
        var (s, _) = await owner.PostAsync("/api/auth/owner/verify-otp", new { token, code = TestClient.Str(sent, "sandboxCode") });
        Assert.Equal(HttpStatusCode.OK, s);
        return owner;
    }
}
