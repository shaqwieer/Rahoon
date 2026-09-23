using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Imports;
using Rahoon.Api.Tests.Infrastructure;

namespace Rahoon.Api.Tests;

/// <summary>L04 bulk import: row validation, duplicates and decisions, PII at rest, commit idempotency, isolation.</summary>
[Collection(ApiCollection.Name)]
public sealed class ImportTests(ApiFixture api)
{
    private const string Header = "contract_number,owner_name,national_id,mobile,region,city,district,property_type,principal,profit,late_fees,other_fees,outstanding_amount,arrears_amount,arrears_installments,first_overdue_date";

    private static string U() => Random.Shared.Next(100000, 999999).ToString();

    private static string Row(string contract, string id = "1023456789", string mobile = "0551100231", string region = "الرياض", string outstanding = "577000.00",
        string overdue = "2026-06-01", string name = "بندر سالم الرويلي") =>
        $"{contract},{name},{id},{mobile},{region},الرياض,حي الملقا,شقة سكنية,540000.00,31000.00,6000.00,0.00,{outstanding},24000.00,3,{overdue}";

    [Fact]
    public async Task Rows_are_validated_duplicates_decided_and_commit_is_idempotent()
    {
        var u = U();
        string C(int n) => $"MF-T{u}-{n:D2}";
        var future = B3Scenarios.TodayRiyadh.AddDays(30).ToString("yyyy-MM-dd");
        var csv = string.Join('\n',
            Header,
            Row(C(1)),                                                    // 1 ready
            Row(C(2), id: "1034567890", name: "غادة فهد المطيري"),          // 2 ready
            Row(C(3), id: "104567890"),                                   // 3 ID with 9 digits
            Row(C(4), outstanding: "1.2 مليون"),                          // 4 text amount
            Row("MF-88-3317406"),                                         // 5 open case RH-2026-004172
            Row(C(6), overdue: future),                                   // 6 future first overdue
            Row(C(7), region: "Riyad"),                                   // 7 unknown region
            Row(C(8), mobile: "66 214 81"),                               // 8 bad mobile
            Row(C(1)),                                                    // 9 duplicate within the file
            Row(C(10), outstanding: "600000.00"));                        // 10 total ≠ Σ items

        var sara = await B3Scenarios.SaraAsync(api);
        var (s, up) = await B3Scenarios.UploadCsvAsync(sara, csv, "محفظة_اختبار.csv");
        Assert.True(s == HttpStatusCode.OK, up?.ToJsonString());
        var counts = up!["batch"]!["counts"]!;
        Assert.Equal(10, counts["total"]!.GetValue<int>());
        Assert.Equal(2, counts["ready"]!.GetValue<int>());
        Assert.Equal(1, counts["duplicates"]!.GetValue<int>());
        Assert.Equal(7, counts["errors"]!.GetValue<int>());
        var batchId = TestClient.Str(up["batch"], "id");

        var (_, detail) = await sara.GetAsync($"/api/cases/imports/{batchId}?status=all");
        var rows = detail!["rows"]!.AsArray();
        (string Field, string Code, string Message) Issue(int n) => rows.First(r => r!["rowNumber"]!.GetValue<int>() == n)!["issues"]![0] is { } i
            ? (TestClient.Str(i, "field"), TestClient.Str(i, "code"), TestClient.Str(i, "message")) : default;
        Assert.Equal(("national_id", "national_id_length", "9 أرقام؛ يجب 10."), Issue(3));
        Assert.Equal(("outstanding_amount", "amount_format"), (Issue(4).Field, Issue(4).Code));
        Assert.Equal("open_case", Issue(5).Code);
        Assert.Equal("مرتبط بحالة مفتوحة RH-2026-004172", Issue(5).Message);
        Assert.Equal("date_future", Issue(6).Code);
        Assert.Equal("قيمة غير معروفة «Riyad»؛ اختر من القائمة", Issue(7).Message);
        Assert.Equal(("mobile", "mobile_format", "صيغة غير صحيحة؛ يبدأ بـ 05"), Issue(8));
        Assert.Equal("duplicate_in_file", Issue(9).Code);
        Assert.Equal("total_mismatch", Issue(10).Code);
        Assert.All(rows.SelectMany(r => r!["issues"]!.AsArray()), i => Assert.False(string.IsNullOrEmpty(TestClient.Str(i, "fix"))));

        // No plaintext identity or phone at rest.
        var raw = await api.WithDbAsync(db => db.ImportRows.Where(r => r.BatchId == Guid.Parse(batchId)).Select(r => r.RawJson).ToListAsync());
        Assert.All(raw, j => { Assert.DoesNotContain("1023456789", j); Assert.DoesNotContain("0551100231", j); });

        // Decisions only on duplicate rows; link only to an open case; create needs a reason.
        Assert.Equal(HttpStatusCode.Conflict, (await sara.PostAsync($"/api/cases/imports/{batchId}/rows/1/decision", new { decision = "skip" })).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await sara.PostAsync($"/api/cases/imports/{batchId}/rows/5/decision", new { decision = "create_with_reason" })).Status);
        var (sLink, linked) = await sara.PostAsync($"/api/cases/imports/{batchId}/rows/5/decision", new { decision = "link" });
        Assert.True(sLink == HttpStatusCode.OK, linked?.ToJsonString());
        Assert.Equal(2, linked!["batch"]!["counts"]!["importable"]!.GetValue<int>());

        var (s1, commit1) = await sara.PostAsync($"/api/cases/imports/{batchId}/commit");
        Assert.True(s1 == HttpStatusCode.OK, commit1?.ToJsonString());
        Assert.False(commit1!["alreadyCommitted"]!.GetValue<bool>());
        Assert.Equal(2, commit1["created"]!.AsArray().Count);
        Assert.Equal(1, commit1["linked"]!.GetValue<int>());

        // A second commit with a different Idempotency-Key creates nothing.
        var (s2, commit2) = await sara.PostAsync($"/api/cases/imports/{batchId}/commit", key: Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.True(commit2!["alreadyCommitted"]!.GetValue<bool>());
        var created = await api.WithDbAsync(db => db.Cases.Where(c => c.ImportBatchId == Guid.Parse(batchId)).ToListAsync());
        Assert.Equal(2, created.Count);
        Assert.All(created, c => { Assert.Equal(CaseStatus.Draft, c.Status); Assert.Equal(CaseSource.Import, c.Source); });

        // PII encrypted on the new party; no owner access and no outbound message for imported drafts.
        var ids = created.Select(c => c.Id).ToList();
        var parties = await api.WithDbAsync(db => db.Parties.Where(p => ids.Contains(p.CaseId)).ToListAsync());
        Assert.All(parties, p => { Assert.NotNull(p.NationalIdEnc); Assert.NotNull(p.PhoneEnc); Assert.DoesNotContain("1023456789", p.NationalIdEnc!); Assert.Contains("•", p.NationalIdMasked!); });
        Assert.False(await api.WithDbAsync(db => db.OwnerAccesses.AnyAsync(o => ids.Contains(o.CaseId))));
        Assert.False(await api.WithDbAsync(db => db.OutboundMessages.AnyAsync(o => o.CaseId != null && ids.Contains(o.CaseId.Value))));
        var snapshot = await api.WithDbAsync(db => db.DebtSnapshots.FirstAsync(d => d.CaseId == ids[0]));
        Assert.Equal(577_000.00m, snapshot.Total);
        Assert.True(await api.WithDbAsync(db => db.AuditEvents.AnyAsync(e => e.CaseReference == "RH-2026-004172" && e.Type == "import.row_linked")));

        // The imported draft can be finalized through the regular wizard (intake guard satisfied).
        var (sSubmit, submitted) = await sara.PostAsync($"/api/cases/drafts/{created[0].Reference}/submit", new { });
        Assert.True(sSubmit == HttpStatusCode.OK, submitted?.ToJsonString());

        Assert.Equal(HttpStatusCode.Conflict, (await sara.PostAsync($"/api/cases/imports/{batchId}/rows/5/decision", new { decision = "skip" })).Status);

        // Re-uploading the same file: the committed contracts (drafts included) are now possible duplicates.
        var (_, again) = await B3Scenarios.UploadCsvAsync(sara, csv);
        Assert.Equal(0, again!["batch"]!["counts"]!["ready"]!.GetValue<int>());
        Assert.Equal(3, again["batch"]!["counts"]!["duplicates"]!.GetValue<int>());
    }

    [Fact]
    public async Task Seeded_batch_is_at_validation_step_with_row_errors()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (_, list) = await sara.GetAsync("/api/cases/imports");
        var seeded = list!.AsArray().First(b => TestClient.Str(b, "fileName") == "محفظة_سبتمبر_2026.csv")!;
        var (s, detail) = await sara.GetAsync($"/api/cases/imports/{TestClient.Str(seeded, "id")}");
        Assert.Equal(HttpStatusCode.OK, s);
        var counts = detail!["batch"]!["counts"]!;
        Assert.Equal((9, 3, 1, 5), (counts["total"]!.GetValue<int>(), counts["ready"]!.GetValue<int>(), counts["duplicates"]!.GetValue<int>(), counts["errors"]!.GetValue<int>()));
        Assert.Contains(detail["rows"]!.AsArray(), r => TestClient.Str(r, "duplicateOf") == "RH-2026-004172");
    }

    [Fact]
    public async Task Same_owner_in_a_closed_case_needs_a_documented_reason()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var closedRef = await WorkflowTests.CreateCaseAsync(sara, Scenarios.NextContract());
        await api.WithDbAsync(async db =>
        {
            var c = await db.Cases.FirstAsync(x => x.Reference == closedRef);
            c.Status = CaseStatus.Closed;
            c.ClosedAt = DateTimeOffset.UtcNow.AddMonths(-11);
            return await db.SaveChangesAsync();
        });
        // Wizard cases use owner ID 1012345678.
        var contract = $"MF-C{U()}-01";
        var csv = Header + "\n" + Row(contract, id: "1012345678", name: "خالد سعد الغامدي");
        var (_, up) = await B3Scenarios.UploadCsvAsync(sara, csv);
        var batchId = TestClient.Str(up!["batch"], "id");
        var issue = up["attention"]![0]!["issues"]![0]!;
        Assert.Equal("same_owner_closed", TestClient.Str(issue, "code"));
        Assert.DoesNotContain("link", up["attention"]![0]!["actions"]!.AsArray().Select(a => a!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await sara.PostAsync($"/api/cases/imports/{batchId}/rows/1/decision", new { decision = "link" })).Status);

        var reason = "عقد جديد للمالك نفسه بعد إغلاق الحالة السابقة بالسداد الكامل.";
        Assert.Equal(HttpStatusCode.OK, (await sara.PostAsync($"/api/cases/imports/{batchId}/rows/1/decision", new { decision = "create_with_reason", reason })).Status);
        var (_, commit) = await sara.PostAsync($"/api/cases/imports/{batchId}/commit");
        var r = commit!["created"]![0]!.GetValue<string>();
        var created = await api.WithDbAsync(db => db.Cases.FirstAsync(c => c.Reference == r));
        Assert.Equal(reason, created.DuplicateOverrideReason);
    }

    [Fact]
    public async Task Import_requires_permission_valid_template_and_is_tenant_isolated()
    {
        var sara = await B3Scenarios.SaraAsync(api);
        var (sTpl, tpl, _) = await sara.SendRawAsync(HttpMethod.Get, "/api/cases/imports/template");
        Assert.Equal(HttpStatusCode.OK, sTpl);
        Assert.StartsWith("﻿" + string.Join(',', ImportService.Columns.Select(c => c.Key)), Encoding.UTF8.GetString(tpl));

        var (sCols, cols) = await B3Scenarios.UploadCsvAsync(sara, "contract_number,owner_name\nMF-X-000001,اسم");
        Assert.Equal(HttpStatusCode.BadRequest, sCols);
        Assert.Contains("أعمدة ناقصة", cols!["errors"]!["file"]![0]!.GetValue<string>());
        var (sExt, _, _) = await sara.SendRawAsync(HttpMethod.Post, "/api/cases/imports", B3Scenarios.CsvContent(Header, "file.xlsx"));
        Assert.Equal(HttpStatusCode.BadRequest, sExt);

        var khaled = await api.LoginAsync("k.alzahrani@alufuq.example"); // case officer: no case.import
        var (sKh, _) = await B3Scenarios.UploadCsvAsync(khaled, Header + "\n" + Row($"MF-K{U()}-01"));
        Assert.Equal(HttpStatusCode.Forbidden, sKh);

        var (_, up) = await B3Scenarios.UploadCsvAsync(sara, Header + "\n" + Row($"MF-I{U()}-01"));
        var batchId = TestClient.Str(up!["batch"], "id");
        var maha = await api.LoginAsync("m.alshahrani@sunbula.example");
        Assert.Equal(HttpStatusCode.NotFound, (await maha.GetAsync($"/api/cases/imports/{batchId}")).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await maha.PostAsync($"/api/cases/imports/{batchId}/commit")).Status);
        var (_, mahaList) = await maha.GetAsync("/api/cases/imports");
        Assert.DoesNotContain(mahaList!.AsArray(), b => TestClient.Str(b, "id") == batchId);

        var (sErr, errFile, _) = await sara.SendRawAsync(HttpMethod.Get, $"/api/cases/imports/{batchId}/errors.csv");
        Assert.Equal(HttpStatusCode.OK, sErr);
        Assert.StartsWith("﻿row,contract_number", Encoding.UTF8.GetString(errFile));
    }
}
