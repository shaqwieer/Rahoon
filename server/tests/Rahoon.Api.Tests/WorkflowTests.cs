using System.Net;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Solutions;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class WorkflowTests(ApiFixture api)
{
    private const string OrgAlufuq = "مصرف الأفق";

    /// <summary>Creates a complete case through the wizard API; returns its reference.</summary>
    internal static async Task<string> CreateCaseAsync(TestClient c, string contract)
    {
        var (s, draft) = await c.PostAsync("/api/cases/drafts");
        Assert.Equal(HttpStatusCode.OK, s);
        var r = TestClient.Str(draft, "reference");
        var steps = new (string path, object body)[]
        {
            ("contract", new { contractNumber = contract, productType = "تمويل سكني · مرابحة", contractDate = "2020-01-10", originalAmount = 900000, originalTermMonths = 240, originalInstallment = 6500, city = "الرياض" }),
            ("parties", new { primary = new { kind = "Individual", fullName = "خالد سعد الغامدي", nationalId = "1012345678", phone = "0551112233", language = "ar" }, additional = Array.Empty<object>() }),
            ("property", new { type = "شقة سكنية", city = "الرياض", district = "حي الياسمين", deedNumber = "310998877", occupancy = "OwnerFamily", rank = 1 }),
            ("debt", new { principal = 700000, profit = 45000, lateFees = 12000, otherFees = 0, arrearsInstallments = 4, arrearsAmount = 26000, arrearsSince = "2026-05-01" }),
        };
        foreach (var (path, body) in steps)
        {
            var (st, res) = await c.PutAsync($"/api/cases/drafts/{r}/{path}", body);
            Assert.Equal(HttpStatusCode.OK, st);
            Assert.Empty(res!["errors"]!.AsObject());
        }
        var (s2, created) = await c.PostAsync($"/api/cases/drafts/{r}/submit", new { });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("awaiting_data", TestClient.Str(created, "status"));
        return r;
    }

    [Fact]
    public async Task Wizard_validates_fields_without_losing_input_and_creates_case()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        var (_, draft) = await sara.PostAsync("/api/cases/drafts");
        var r = TestClient.Str(draft, "reference");
        var (s, res) = await sara.PutAsync($"/api/cases/drafts/{r}/parties",
            new { primary = new { kind = "Individual", fullName = "عبدالله محمد", nationalId = "1098123", phone = "66 214 81" } });
        Assert.Equal(HttpStatusCode.OK, s);
        var errors = res!["errors"]!.AsObject();
        Assert.Equal("10 أرقام مطلوبة، أُدخل 7", errors["primary.nationalId"]![0]!.GetValue<string>());
        Assert.Equal("يجب أن يبدأ بـ 05", errors["primary.phone"]![0]!.GetValue<string>());

        // Submitting an incomplete draft is refused with reasons, and the refusal is audited.
        var (s2, refused) = await sara.PostAsync($"/api/cases/drafts/{r}/submit", new { });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s2);
        Assert.NotEmpty(refused!["reasons"]!.AsArray());

        var created = await CreateCaseAsync(sara, "MF-TEST-000101");
        var (s3, ws) = await sara.GetAsync($"/api/cases/{created}");
        Assert.Equal(HttpStatusCode.OK, s3);
        Assert.Equal("awaiting_data", TestClient.Str(ws!["header"], "status"));
    }

    [Fact]
    public async Task Duplicate_open_contract_requires_documented_reason()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        await CreateCaseAsync(sara, "MF-DUP-000777");
        var (_, draft) = await sara.PostAsync("/api/cases/drafts");
        var r = TestClient.Str(draft, "reference");
        var (_, saved) = await sara.PutAsync($"/api/cases/drafts/{r}/contract", new { contractNumber = "MF-DUP-000777", productType = "تمويل سكني · مرابحة" });
        Assert.Equal("open_match", TestClient.Str(saved!["duplicate"], "status"));
    }

    [Fact]
    public async Task Forbidden_transition_is_refused_and_audited_as_blocked()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        var r = await CreateCaseAsync(sara, "MF-TEST-000202");
        // start_valuation is only allowed from Verification.
        var (status, body) = await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_valuation", reason = "x", expectedStatus = "awaiting_data" });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("transition_not_allowed", TestClient.Str(body, "code"));
        var blocked = await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == r && e.Blocked && e.Type == "case.transition_blocked"));
        Assert.True(blocked);
    }

    [Fact]
    public async Task Guarded_transition_lists_unmet_prerequisites()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        var r = await CreateCaseAsync(sara, "MF-TEST-000303");
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_verification", expectedStatus = "awaiting_data" })).Status);
        var (status, body) = await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_valuation", expectedStatus = "verification" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Contains(body!["reasons"]!.AsArray(), x => x!.GetValue<string>().Contains("صك الملكية"));
    }

    [Fact]
    public async Task Stale_expected_status_is_a_conflict()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        var r = await CreateCaseAsync(sara, "MF-TEST-000404");
        var (status, body) = await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "start_verification", expectedStatus = "verification" });
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal("stale_state", TestClient.Str(body, "code"));
    }

    [Fact]
    public async Task Pause_requires_reason_and_resume_restores_previous_state()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        var r = await CreateCaseAsync(sara, "MF-TEST-000505");
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "pause" })).Status);
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "pause", reason = "بانتظار مستند من جهة خارجية" })).Status);
        var (_, resumed) = await sara.PostAsync($"/api/cases/{r}/transitions", new { action = "resume", reason = "وصل المستند" });
        Assert.Equal("awaiting_data", TestClient.Str(resumed, "status"));
    }

    [Fact]
    public async Task Audit_chain_verifies_after_activity()
    {
        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", OrgAlufuq);
        await CreateCaseAsync(sara, "MF-TEST-000606");
        var orgId = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "alufuq").Select(o => (Guid?)o.Id).FirstAsync());
        var broken = await api.WithDbAsync(db => AuditLog.VerifyChainAsync(db, orgId));
        Assert.Null(broken);
    }
}
