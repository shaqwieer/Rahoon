using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rahoon.Api.Modules.Ecosystem;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EcosystemTests(ApiFixture api)
{
    private const string Alufuq = "مصرف الأفق";

    [Fact]
    public async Task Provider_directory_shows_licence_states_and_per_institution_performance()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (s, dir) = await layla.GetAsync("/api/institution/providers");
        Assert.Equal(HttpStatusCode.OK, s);
        var items = dir!["items"]!.AsArray();
        var b = items.First(i => TestClient.Str(i, "name") == "مكتب تقييم معتمد «ب»")!;
        Assert.Equal(0.96m, b["onTimeRate"]!.GetValue<decimal>());
        Assert.Equal(0.04m, b["reworkRate"]!.GetValue<decimal>());
        Assert.Equal("approved", TestClient.Str(b, "status"));
        var h = items.First(i => TestClient.Str(i, "name") == "مكتب التقييم «ح»")!;
        Assert.Equal("suspended", TestClient.Str(h, "status"));
        Assert.False(h["assignable"]!.GetValue<bool>());
        Assert.Equal("expired", TestClient.Str(h["license"], "state"));
        var d = items.First(i => TestClient.Str(i, "name") == "مكتب وساطة عقارية «د»")!;
        Assert.Equal("warning", TestClient.Str(d, "status"));
        Assert.True(d["assignable"]!.GetValue<bool>());
        Assert.Null(d["reworkRate"]); // rework does not apply to brokers
        var expiring = dir["chips"]!.AsArray().First(c => TestClient.Str(c, "key") == "expiring")!;
        Assert.Equal(items.Count(i => TestClient.Str(i!["license"], "state") == "expiring_soon"), expiring["count"]!.GetValue<int>());

        // Another institution sees only its own directory and its own performance for the same provider.
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        var (_, other) = await maha.GetAsync("/api/institution/providers");
        var list = other!["items"]!.AsArray();
        Assert.Single(list);
        Assert.Equal(0.9565m, list[0]!["onTimeRate"]!.GetValue<decimal>());
        // Institutions never see registration applications.
        Assert.Equal(HttpStatusCode.Forbidden, (await layla.GetAsync("/api/platform/providers/applications")).Status);
    }

    [Fact]
    public async Task Onboarding_submission_and_compliance_review_with_licence_expiry()
    {
        var tariq = await RawSession.LoginAsync(api, "t.alsaadi@inspect-t.example");
        var (_, draft) = await tariq.Json.GetAsync("/api/provider/onboarding");
        Assert.Equal(3, draft!["currentStep"]!.GetValue<int>());
        Assert.Equal("مطلوب", TestClient.Str(draft["documents"]!.AsArray().First(x => TestClient.Str(x, "kind") == "commercial_register"), "status"));
        var (s0, incomplete) = await tariq.Json.PostAsync("/api/provider/onboarding/submit");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s0);
        Assert.NotEmpty(incomplete!["reasons"]!.AsArray());

        var (su, card) = await tariq.UploadAsync("/api/provider/onboarding/documents", "commercial_register", "1010998877", null);
        Assert.True(su == HttpStatusCode.OK, card);
        foreach (var (step, data) in new (int, object)[]
                 {
                     (3, new { }), (4, new { teamCount = 3, teamIndividuallyLicensed = true }), (5, new { iban = "SA0380000000608010167519" }),
                     (6, new { independenceDeclared = true, dataProtectionSigned = false }),
                 })
            Assert.Equal(HttpStatusCode.OK, (await tariq.Json.PutAsync($"/api/provider/onboarding/steps/{step}", new { data, advance = true })).Status);
        var stored = await api.WithDbAsync(db => db.Set<ProviderProfile>().FirstAsync(p => p.ApplicationRef == "PRV-APP-0047"));
        Assert.DoesNotContain("608010167519", stored.StepDataJson); // only the masked IBAN is kept
        var (s1, submitted) = await tariq.Json.PostAsync("/api/provider/onboarding/submit");
        Assert.True(s1 == HttpStatusCode.OK, SaleScenarios.Raw(submitted));

        // Compliance review: insurance expires within 30 days and the data-protection agreement is unsigned → accept is blocked.
        var hessa = await api.LoginAsync("h.alotaibi@rahoon.example");
        var (_, app) = await hessa.GetAsync($"/api/platform/providers/applications/{stored.Id}");
        Assert.False(app!["canAccept"]!.GetValue<bool>());
        Assert.Equal("warn", TestClient.Str(app["checklist"]!.AsArray().First(c => TestClient.Str(c, "key") == "insurance"), "status"));
        Assert.Equal(HttpStatusCode.BadRequest, (await hessa.PostAsync($"/api/platform/providers/applications/{stored.Id}/decision", new { decision = "request_info", message = "" })).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await hessa.PostAsync($"/api/platform/providers/applications/{stored.Id}/decision", new { decision = "accept", message = "قبول رغم البنود المفتوحة." })).Status);
        var (s2, info) = await hessa.PostAsync($"/api/platform/providers/applications/{stored.Id}/decision", new { decision = "request_info", message = "وثيقة التأمين المهني تنتهي خلال 14 يوماً؛ يرجى رفع التجديد." });
        Assert.True(s2 == HttpStatusCode.OK, SaleScenarios.Raw(info));
        Assert.Equal("NeedsInfo", TestClient.Str(info, "status"));

        // Provider completes and resubmits; compliance accepts; the institution can then add it from the platform directory.
        Assert.Equal(HttpStatusCode.OK, (await tariq.UploadAsync("/api/provider/onboarding/documents", "professional_insurance", "INS-7781", "2027-10-07")).Status);
        Assert.Equal(HttpStatusCode.OK, (await tariq.Json.PutAsync("/api/provider/onboarding/steps/6", new { data = new { independenceDeclared = true, dataProtectionSigned = true }, advance = true })).Status);
        Assert.Equal(HttpStatusCode.OK, (await tariq.Json.PostAsync("/api/provider/onboarding/submit")).Status);
        var (s3, accepted) = await hessa.PostAsync($"/api/platform/providers/applications/{stored.Id}/decision", new { decision = "accept", message = "اكتملت المراجعة وجميع البنود سارية." });
        Assert.True(s3 == HttpStatusCode.OK, SaleScenarios.Raw(accepted));
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (_, platformDir) = await layla.GetAsync("/api/institution/providers/platform-directory");
        Assert.Contains(platformDir!["items"]!.AsArray(), i => TestClient.Str(i, "name") == "شركة الفحص «ط»");
        Assert.Equal(HttpStatusCode.OK, (await layla.PostAsync("/api/institution/providers", new { providerOrganizationId = stored.ProviderOrganizationId, frameworkFee = 2_000 })).Status);
        var (_, dir) = await layla.GetAsync("/api/institution/providers?type=inspection");
        Assert.Contains(dir!["items"]!.AsArray(), i => TestClient.Str(i, "name") == "شركة الفحص «ط»" && TestClient.Str(i, "status") == "approved");
    }

    [Fact]
    public async Task Self_uploaded_licence_renewal_changes_nothing_until_compliance_approves()
    {
        var hatem = await RawSession.LoginAsync(api, "h.alrashid@valuer-b.example");
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        async Task<string> LicenceTextAsync()
        {
            var (_, dir) = await layla.GetAsync("/api/institution/providers?type=valuer");
            return TestClient.Str(dir!["items"]!.AsArray().First(i => TestClient.Str(i, "name") == "مكتب تقييم معتمد «ب»")!["license"], "text");
        }
        var before = await LicenceTextAsync();
        var renewal = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(700);
        var (su, _) = await hatem.UploadAsync("/api/provider/onboarding/documents", "practice_license", "1100884471", renewal.ToString("yyyy-MM-dd"));
        Assert.Equal(HttpStatusCode.OK, su);
        Assert.Equal(before, await LicenceTextAsync()); // the pending upload does not change directory status
        var (_, profile) = await hatem.Json.GetAsync("/api/provider/profile");
        Assert.NotNull(profile!["license"]!["pendingRenewal"]);

        var (providerOrg, licenseId) = await api.WithDbAsync(async db =>
        {
            var l = await db.Set<ProviderLicense>().FirstAsync(x => x.IsCurrent && x.Kind == "practice_license" && db.Organizations.Any(o => o.Id == x.ProviderOrganizationId && o.ShortCode == "valuer-b"));
            return (l.ProviderOrganizationId, l.Id);
        });
        var hessa = await api.LoginAsync("h.alotaibi@rahoon.example");
        Assert.Equal(HttpStatusCode.OK, (await hessa.PostAsync($"/api/platform/providers/{providerOrg}/licenses/{licenseId}/review", new { decision = "approve", note = "مطابق للشهادة" })).Status);
        Assert.Equal($"ساري حتى {renewal:yyyy-MM}", await LicenceTextAsync());
    }

    [Fact]
    public async Task Invoice_from_delivered_assignment_requires_a_different_approver()
    {
        var hatem = await api.LoginAsync("h.alrashid@valuer-b.example");
        var (_, invoiceable) = await hatem.GetAsync("/api/provider/invoices/invoiceable");
        var candidates = invoiceable!["items"]!.AsArray().Where(i => TestClient.Str(i, "lender") == Alufuq).Select(i => TestClient.Str(i, "assignment")).ToList();
        Assert.True(candidates.Count >= 2);
        var (sc, created) = await hatem.PostAsync("/api/provider/invoices", new { assignmentReference = candidates[0] });
        Assert.True(sc == HttpStatusCode.OK, SaleScenarios.Raw(created));
        Assert.Equal(3_500m, created!["amount"]!.GetValue<decimal>());
        var number = TestClient.Str(created, "number");
        Assert.Equal(HttpStatusCode.Conflict, (await hatem.PostAsync("/api/provider/invoices", new { assignmentReference = candidates[0] })).Status);
        Assert.Equal(HttpStatusCode.OK, (await hatem.PostAsync($"/api/provider/invoices/{number}/submit")).Status);
        var id = await api.WithDbAsync(db => db.Set<ProviderInvoice>().Where(i => i.Number == number).Select(i => i.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await hatem.PostAsync($"/api/institution/provider-invoices/{id}/review", new { decision = "approve" })).Status);

        // A user who is both the provider's admin and the lender's finance officer cannot check their own invoice.
        var dual = await DualMemberAsync();
        var asProvider = await api.LoginAsync(dual, "مكتب تقييم معتمد «ب»");
        var (_, own) = await asProvider.PostAsync("/api/provider/invoices", new { assignmentReference = candidates[1] });
        var ownNumber = TestClient.Str(own, "number");
        Assert.Equal(HttpStatusCode.OK, (await asProvider.PostAsync($"/api/provider/invoices/{ownNumber}/submit")).Status);
        var ownId = await api.WithDbAsync(db => db.Set<ProviderInvoice>().Where(i => i.Number == ownNumber).Select(i => i.Id).FirstAsync());
        var asLender = await api.LoginAsync(dual, Alufuq);
        Assert.Equal(HttpStatusCode.Forbidden, (await asLender.PostAsync($"/api/institution/provider-invoices/{ownId}/review", new { decision = "approve" })).Status);

        // An independent invoice.approve holder approves; payment is recorded by reference only.
        var reem = await api.LoginAsync("r.aldosari@alufuq.example");
        Assert.Equal(HttpStatusCode.BadRequest, (await reem.PostAsync($"/api/institution/provider-invoices/{id}/review", new { decision = "reject" })).Status);
        var (sa, approved) = await reem.PostAsync($"/api/institution/provider-invoices/{id}/review", new { decision = "approve" });
        Assert.True(sa == HttpStatusCode.OK, SaleScenarios.Raw(approved));
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        Assert.Equal(HttpStatusCode.OK, (await layla.PostAsync($"/api/institution/provider-invoices/{id}/payment", new { reference = "TRX-12345678" })).Status);
        var inv = await api.WithDbAsync(db => db.Set<ProviderInvoice>().FirstAsync(i => i.Id == id));
        Assert.Equal(ProviderInvoiceStatus.Paid, inv.Status);
        Assert.NotEqual(inv.SubmittedByUserId, inv.DecidedByUserId);

        // Tenant isolation: alufuq never sees or decides the sunbula invoice.
        var (_, lenderList) = await reem.GetAsync("/api/institution/provider-invoices");
        Assert.DoesNotContain(lenderList!["items"]!.AsArray(), i => TestClient.Str(i, "number") == "INV-B-2026-121");
        var sunbulaInvoice = await api.WithDbAsync(db => db.Set<ProviderInvoice>().Where(i => i.Number == "INV-B-2026-121").Select(i => i.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await reem.PostAsync($"/api/institution/provider-invoices/{sunbulaInvoice}/review", new { decision = "approve" })).Status);
        var (_, perf) = await hatem.GetAsync("/api/provider/performance");
        Assert.Contains("من", TestClient.Str(perf!["metrics"]![0], "note"));
    }

    [Fact]
    public async Task Workflow_version_publish_needs_second_user_and_simulation_has_no_side_effects()
    {
        var sunbula = await api.WithDbAsync(db => db.Organizations.Where(o => o.ShortCode == "sunbula").Select(o => o.Id).FirstAsync());
        var ahmad = await api.LoginAsync("a.almutairi@rahoon.example");
        var (sd, draft) = await ahmad.PostAsync($"/api/workflows/{sunbula}/versions");
        Assert.True(sd == HttpStatusCode.OK, SaleScenarios.Raw(draft));
        var v = draft!["version"]!.GetValue<int>();
        var (sr, rule) = await ahmad.PostAsync($"/api/workflows/{sunbula}/versions/{v}/rules",
            new { stageKey = "internal_approval", field = "waiver_percent", @operator = ">", value = 3, action = "add_compliance_review" });
        Assert.True(sr == HttpStatusCode.OK, SaleScenarios.Raw(rule));
        Assert.Equal(HttpStatusCode.Conflict, (await ahmad.SendAsync(HttpMethod.Delete, $"/api/workflows/{sunbula}/versions/{v}/rules/sod")).Status);
        Assert.Equal(HttpStatusCode.OK, (await ahmad.PutAsync($"/api/workflows/{sunbula}/versions/{v}/stages/verification", new { slaDays = 4 })).Status);

        // Simulation: evaluates a hypothetical case against the draft and changes nothing.
        var before = await SnapshotAsync(sunbula);
        var (ss, sim) = await ahmad.PostAsync($"/api/workflows/{sunbula}/versions/{v}/simulate",
            new { @case = new { amount = 1_120_450, waiverPercent = 4, dsrPercent = 45, termMonths = 96, solutionKind = "Reschedule", arrearsInstallments = 6 }, sampleSize = 50 });
        Assert.True(ss == HttpStatusCode.OK, SaleScenarios.Raw(sim));
        var approvals = sim!["case"]!["requiredApprovals"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains("مراجعة الامتثال قبل المعتمد", approvals);
        Assert.DoesNotContain("مراجعة الامتثال قبل المعتمد", sim["case"]!["comparedToActive"]!["requiredApprovals"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(before, await SnapshotAsync(sunbula));

        // Publishing: submitted by one user, approved by a second authorised user with step-up.
        Assert.Equal(HttpStatusCode.OK, (await ahmad.PostAsync($"/api/workflows/{sunbula}/versions/{v}/submit", new { changeSummary = "مراجعة الامتثال للتنازل فوق 3%" })).Status);
        await ahmad.StepUpAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ahmad.PostAsync($"/api/workflows/{sunbula}/versions/{v}/approve", new { note = "اعتماد ذاتي" })).Status);
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        Assert.Equal(HttpStatusCode.NotFound, (await layla.GetAsync($"/api/workflows/{sunbula}")).Status); // another institution
        var hessa = await api.LoginAsync("h.alotaibi@rahoon.example");
        Assert.Equal(HttpStatusCode.Forbidden, (await hessa.PostAsync($"/api/workflows/{sunbula}/versions/{v}/approve", new { note = "بدون رمز" })).Status);
        await hessa.StepUpAsync();
        var (sp, published) = await hessa.PostAsync($"/api/workflows/{sunbula}/versions/{v}/approve", new { note = "قواعد المنصة سليمة." });
        Assert.True(sp == HttpStatusCode.OK, SaleScenarios.Raw(published));
        var versions = await api.WithDbAsync(db => db.Set<WorkflowVersion>().Where(x => x.InstitutionOrganizationId == sunbula).OrderBy(x => x.VersionNo).ToListAsync());
        Assert.Equal(WorkflowVersionStatus.Superseded, versions.First(x => x.VersionNo == v - 1).Status);
        var active = versions.Single(x => x.Status == WorkflowVersionStatus.Active);
        Assert.Equal(v, active.VersionNo);
        Assert.NotEqual(active.SubmittedByUserId, active.ApprovedByUserId);
    }

    [Fact]
    public async Task Report_cells_below_ten_cases_are_suppressed_and_csv_export_is_audited()
    {
        var layla = await api.LoginAsync("l.alghamdi@alufuq.example");
        var (s, report) = await layla.PostAsync("/api/reports/query", new { metric = "avg_resolution_days", breakdown = "solution_kind_quarter", from = "2025-10", to = "2026-09" });
        Assert.True(s == HttpStatusCode.OK, SaleScenarios.Raw(report));
        var cells = report!["cells"]!.AsArray();
        Assert.Equal(16, cells.Count); // 4 kinds × 4 quarters
        foreach (var c in cells)
        {
            if (c!["suppressed"]!.GetValue<bool>()) { Assert.Null(c["value"]); Assert.Null(c["n"]); }
            else Assert.True(c["n"]!.GetValue<int>() >= 10);
        }
        Assert.True(report["suppressedCount"]!.GetValue<int>() > 0);

        var (se, _) = await layla.SendAsync(HttpMethod.Post, "/api/reports/export", new { metric = "case_count", breakdown = "status", from = "2025-10", to = "2026-09" });
        Assert.Equal(HttpStatusCode.OK, se);
        var audited = await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.Type == "report.exported"));
        Assert.True(audited);
        var khaled = await api.LoginAsync("k.alzahrani@alufuq.example"); // case officer: no reports.view
        Assert.Equal(HttpStatusCode.Forbidden, (await khaled.PostAsync("/api/reports/query", new { metric = "case_count", breakdown = "status", from = "2025-10", to = "2026-09" })).Status);
    }

    [Fact]
    public async Task Licensed_signing_and_payment_stay_unavailable_and_never_fabricate()
    {
        var r = await Scenarios.ReadyForApprovalAsync(api);
        var owner = await Scenarios.OwnerForNewCaseAsync(api, r);
        var (_, states) = await owner.GetAsync("/api/integrations/conditional");
        Assert.All(states!["capabilities"]!.AsArray(), c => Assert.Equal("unavailable", TestClient.Str(c, "state"))); // simulated is never shown to the owner
        Assert.Null(states["stateTable"]);
        var consentsBefore = await api.WithDbAsync(db => db.ConsentRecords.CountAsync());
        var paymentsBefore = await api.WithDbAsync(db => db.Payments.CountAsync());

        var (s1, sign) = await owner.PostAsync("/api/owner/agreement/signature-sessions");
        Assert.Equal(HttpStatusCode.Conflict, s1);
        Assert.Equal("integration_unavailable", TestClient.Str(sign, "code"));
        Assert.NotEmpty(TestClient.Str(sign, "manualAlternative"));
        var (s2, pay) = await owner.PostAsync("/api/owner/installments/1/payment-attempts");
        Assert.Equal(HttpStatusCode.Conflict, s2);
        Assert.Equal("integration_unavailable", TestClient.Str(pay, "code"));
        Assert.Equal(consentsBefore, await api.WithDbAsync(db => db.ConsentRecords.CountAsync()));
        Assert.Equal(paymentsBefore, await api.WithDbAsync(db => db.Payments.CountAsync()));

        var sara = await api.LoginAsync("s.alqahtani@alufuq.example", Alufuq);
        var (_, team) = await sara.GetAsync("/api/integrations/conditional");
        var caps = team!["capabilities"]!.AsArray();
        Assert.Equal("unavailable", TestClient.Str(caps.First(c => TestClient.Str(c, "capability") == "licensed_signing"), "state"));
        Assert.Equal("simulated", TestClient.Str(caps.First(c => TestClient.Str(c, "capability") == "licensed_payment"), "state"));
        Assert.All(caps, c => Assert.False(c!["licensedPathAvailable"]!.GetValue<bool>()));
        Assert.Equal(5, team["stateTable"]!.AsArray().Count);
    }

    private Task<string> SnapshotAsync(Guid org) => api.WithDbAsync(async db =>
    {
        var versions = await db.Set<WorkflowVersion>().AsNoTracking().Where(x => x.InstitutionOrganizationId == org).OrderBy(x => x.VersionNo)
            .Select(x => new { x.VersionNo, x.Status, x.StagesJson, x.UpdatedAt }).ToListAsync();
        var audits = await db.AuditEvents.CountAsync(e => e.OrganizationId == org);
        return System.Text.Json.JsonSerializer.Serialize(new { versions, audits });
    });

    /// <summary>A user holding the valuer's provider_admin role and the lender's finance role (maker-checker probe).</summary>
    private Task<string> DualMemberAsync() => api.WithDbAsync(async db =>
    {
        var email = $"dual-{Guid.NewGuid():N}@valuer-b.example";
        var hasher = api.Services.GetRequiredService<IPasswordHasher<User>>();
        var user = new User { Email = email, FullName = "مستخدم مزدوج للاختبار", Phone = "0550009999", MfaEnrolled = true };
        user.PasswordHash = hasher.HashPassword(user, ApiFixture.Password);
        db.Users.Add(user);
        foreach (var (code, role) in new[] { ("valuer-b", SystemRoles.ProviderAdmin), ("alufuq", SystemRoles.Finance) })
        {
            var org = await db.Organizations.FirstAsync(o => o.ShortCode == code);
            var r = await db.Roles.FirstAsync(x => x.OrganizationId == org.Id && x.Key == role);
            var m = new Membership { OrganizationId = org.Id, UserId = user.Id, Title = role };
            m.Roles.Add(new MembershipRole { MembershipId = m.Id, RoleId = r.Id });
            db.Memberships.Add(m);
        }
        await db.SaveChangesAsync();
        return email;
    });
}

/// <summary>Logged-in session that can also send multipart uploads (the shared TestClient sends JSON only).</summary>
public sealed class RawSession
{
    private readonly HttpClient _http;
    private readonly TestClient _json;
    private string _cookie = "";
    private string _csrf = "";

    private RawSession(ApiFixture api)
    {
        _http = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        _json = api.Client();
    }

    public TestClient Json => _json;

    public static async Task<RawSession> LoginAsync(ApiFixture api, string email)
    {
        var s = new RawSession(api);
        // Log in with a private cookie jar mirrored from Set-Cookie headers, then reuse the same session in TestClient.
        var (_, login) = await s.SendJsonAsync("/api/auth/login", new { email, password = ApiFixture.Password });
        await s.SendJsonAsync("/api/auth/mfa/verify", new { code = TestClient.Str(login, "sandboxCode") });
        await s._json.LoginAsync(email, ApiFixture.Password);
        return s;
    }

    private async Task<(HttpStatusCode, System.Text.Json.Nodes.JsonNode?)> SendJsonAsync(string path, object body)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Post, path) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        Prepare(msg);
        using var res = await _http.SendAsync(msg);
        Capture(res);
        var text = await res.Content.ReadAsStringAsync();
        return (res.StatusCode, string.IsNullOrWhiteSpace(text) ? null : System.Text.Json.Nodes.JsonNode.Parse(text));
    }

    public async Task<(HttpStatusCode Status, string Body)> UploadAsync(string path, string kind, string? number, string? expiresOn)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(kind), "kind");
        if (number is not null) form.Add(new StringContent(number), "number");
        if (expiresOn is not null) form.Add(new StringContent(expiresOn), "expiresOn");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "document.pdf");
        using var msg = new HttpRequestMessage(HttpMethod.Post, path) { Content = form };
        Prepare(msg);
        using var res = await _http.SendAsync(msg);
        return (res.StatusCode, await res.Content.ReadAsStringAsync());
    }

    private void Prepare(HttpRequestMessage msg)
    {
        msg.Headers.Add("Origin", TestClient.Origin);
        if (_cookie.Length > 0) msg.Headers.Add("Cookie", _cookie);
        if (_csrf.Length > 0) msg.Headers.Add("X-CSRF-Token", _csrf);
        msg.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
    }

    private void Capture(HttpResponseMessage res)
    {
        if (!res.Headers.TryGetValues("Set-Cookie", out var values)) return;
        var jar = _cookie.Split("; ", StringSplitOptions.RemoveEmptyEntries).Select(c => c.Split('=', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);
        foreach (var v in values)
        {
            var pair = v.Split(';')[0].Split('=', 2);
            if (pair.Length == 2 && pair[1].Length > 0) jar[pair[0]] = pair[1];
        }
        _cookie = string.Join("; ", jar.Select(kv => $"{kv.Key}={kv.Value}"));
        _csrf = jar.GetValueOrDefault("rahoon_csrf") ?? "";
    }
}
