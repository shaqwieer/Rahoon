using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Cases;

namespace Rahoon.Api.Modules.Imports;

public sealed record ImportIssue(string Field, string FieldLabel, string Code, string Message, string Fix, string Severity);

public sealed record ImportColumn(string Key, string Label, bool Required, string Example);

/// <summary>
/// Bulk import (L04): CSV template v3 parsing and row validation. Rows are stored without plaintext PII —
/// national ID and mobile are kept encrypted (with masked copies and the lookup hash) so the commit can copy
/// them onto the new party without ever decrypting. Every lookup is explicitly scoped to the organization
/// (the seeder runs with tenant filters bypassed).
/// </summary>
public sealed class ImportService(RahoonDbContext db, CaseFactory factory, IClock clock)
{
    public const string TemplateVersion = "v3";
    public const int MaxRows = 5000;
    public const long MaxBytes = 5 * 1024 * 1024;

    public static readonly ImportColumn[] Columns =
    [
        new("contract_number", "رقم العقد", true, "MF-88-3317406"),
        new("owner_name", "اسم المالك", true, "عبدالله محمد السبيعي"),
        new("national_id", "رقم الهوية", true, "1098734542"),
        new("mobile", "الجوال", true, "0551234581"),
        new("region", "المنطقة", true, "الرياض"),
        new("city", "المدينة", true, "الرياض"),
        new("district", "الحي", false, "حي النرجس"),
        new("property_type", "نوع العقار", false, "فيلا سكنية"),
        new("product_type", "نوع المنتج", false, "تمويل سكني · مرابحة"),
        new("contract_date", "تاريخ العقد", false, "2021-05-10"),
        new("original_amount", "مبلغ التمويل الأصلي", false, "1450000.00"),
        new("original_installment", "القسط الأصلي", false, "13774.29"),
        new("principal", "أصل الدين", true, "1121840.00"),
        new("profit", "الأرباح", false, "144420.00"),
        new("late_fees", "غرامات التأخير", false, "18300.00"),
        new("other_fees", "رسوم أخرى", false, "0.00"),
        new("outstanding_amount", "المبلغ القائم", true, "1284560.00"),
        new("arrears_amount", "المتأخرات", false, "96420.00"),
        new("arrears_installments", "عدد الأقساط المتأخرة", false, "7"),
        new("first_overdue_date", "تاريخ أول تأخر", false, "2026-02-01"),
    ];

    /// <summary>Controlled list of the 13 administrative regions (free text such as «Riyad» is refused).</summary>
    public static readonly string[] Regions =
    [
        "الرياض", "مكة المكرمة", "المدينة المنورة", "القصيم", "الشرقية", "عسير", "تبوك", "حائل", "الحدود الشمالية", "جازان", "نجران", "الباحة", "الجوف",
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string TemplateCsv() =>
        "﻿" + string.Join(',', Columns.Select(c => c.Key)) + "\n" + string.Join(',', Columns.Select(c => c.Example.Contains(',') ? $"\"{c.Example}\"" : c.Example)) + "\n";

    public static ImportIssue ParseIssue(string raw)
    {
        try { return JsonSerializer.Deserialize<ImportIssue>(raw, Json) ?? new ImportIssue("", "", "unknown", raw, "", "error"); }
        catch (JsonException) { return new ImportIssue("", "", "unknown", raw, "", "error"); }
    }

    public static Dictionary<string, string?> ParseRaw(string rawJson) =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(rawJson, Json) ?? new();

    // ───────── CSV ─────────

    /// <summary>RFC 4180 parser (quotes, escaped quotes, CRLF/LF, comma or semicolon delimiter).</summary>
    public static List<List<string>> ParseCsv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Contains(';') && !firstLine.Contains(',') ? ';' : ',';
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
                continue;
            }
            if (ch == '"' && field.Length == 0) quoted = true;
            else if (ch == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Any(f => f.Length > 0)) rows.Add(row);
                row = [];
            }
            else field.Append(ch);
        }
        row.Add(field.ToString());
        if (row.Any(f => f.Length > 0)) rows.Add(row);
        return rows;
    }

    // ───────── Validation ─────────

    public async Task<ImportBatch> ValidateAsync(Guid orgId, Guid uploadedBy, string fileName, string csvText)
    {
        var table = ParseCsv(csvText);
        if (table.Count == 0) Validate.Throw("file", "الملف فارغ.");
        var header = table[0].Select(h => h.Trim()).ToList();
        var index = new Dictionary<string, int>();
        for (var i = 0; i < header.Count; i++)
        {
            var col = Columns.FirstOrDefault(c => string.Equals(c.Key, header[i], StringComparison.OrdinalIgnoreCase) || c.Label == header[i]);
            if (col is not null) index.TryAdd(col.Key, i);
        }
        var missingCols = Columns.Where(c => c.Required && !index.ContainsKey(c.Key)).Select(c => c.Label).ToList();
        if (missingCols.Count > 0) Validate.Throw("file", $"أعمدة ناقصة في القالب {TemplateVersion}: {string.Join("، ", missingCols)}. نزّل القالب واستخدمه.");
        var dataRows = table.Skip(1).ToList();
        if (dataRows.Count == 0) Validate.Throw("file", "لا توجد صفوف بيانات في الملف.");
        if (dataRows.Count > MaxRows) Validate.Throw("file", $"الحد الأقصى {MaxRows} صف في الملف الواحد.");

        string? Cell(List<string> r, string key) => index.TryGetValue(key, out var i) && i < r.Count && !string.IsNullOrWhiteSpace(r[i]) ? r[i].Trim() : null;

        // Existing contracts and closed-case owners in this organization only (drafts count: re-importing a file must not duplicate them).
        var contracts = dataRows.Select(r => Cell(r, "contract_number")?.ToUpperInvariant()).Where(x => x is not null).Distinct().ToList();
        var existing = await db.FinancingContracts.IgnoreQueryFilters()
            .Where(f => f.OrganizationId == orgId && contracts.Contains(f.ContractNumber))
            .Join(db.Cases.IgnoreQueryFilters(), f => f.CaseId, c => c.Id, (f, c) => new { f.ContractNumber, c.Reference, c.Status, c.ClosedAt, c.StatusChangedAt })
            .ToListAsync();

        var batch = new ImportBatch
        {
            OrganizationId = orgId, FileName = SafeName(fileName), TemplateVersion = TemplateVersion, UploadedByUserId = uploadedBy,
            TotalRows = dataRows.Count, Status = ImportBatchStatus.Validated,
        };
        var today = clock.TodayRiyadh;
        var seen = new Dictionary<string, int>();
        var pending = new List<(ImportRow Row, string? IdHash)>();
        for (var n = 0; n < dataRows.Count; n++)
        {
            var r = dataRows[n];
            var rowNumber = n + 1;
            var issues = new List<ImportIssue>();
            void Err(string key, string code, string message, string fix) =>
                issues.Add(new ImportIssue(key, Columns.First(c => c.Key == key).Label, code, message, fix, "error"));

            foreach (var col in Columns.Where(c => c.Required))
                if (Cell(r, col.Key) is null) Err(col.Key, "required", $"{col.Label} مطلوب.", "أكمل الحقل في الملف.");

            var contract = Cell(r, "contract_number")?.ToUpperInvariant();
            if (contract is not null && !(contract.Length is >= 6 and <= 40 && contract.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '-')))
                Err("contract_number", "contract_format", "رقم العقد يقبل حروفاً لاتينية وأرقاماً وشرطة فقط (6–40).", "صحّح في الملف");
            else if (contract is not null && seen.TryGetValue(contract, out var firstRow))
                Err("contract_number", "duplicate_in_file", $"رقم العقد مكرر في الملف (الصف {firstRow}).", "احذف أحد الصفين");
            if (contract is not null) seen.TryAdd(contract, rowNumber);

            var name = Cell(r, "owner_name");
            if (name is not null && name.Length < 3) Err("owner_name", "name_short", "الاسم قصير جداً.", "اكتب الاسم كما في الهوية");

            var idRaw = Cell(r, "national_id");
            var idDigits = idRaw is null ? null : new string(idRaw.Where(char.IsDigit).ToArray());
            if (idRaw is not null)
            {
                if (idDigits!.Length != 10 || idDigits.Length != idRaw.Count(ch => !char.IsWhiteSpace(ch)))
                    Err("national_id", "national_id_length", idDigits.Length != 10 ? $"{idDigits.Length} أرقام؛ يجب 10." : "رقم الهوية أرقام فقط.", "صحّح في الملف");
                else if (idDigits[0] is not ('1' or '2' or '7'))
                    Err("national_id", "national_id_prefix", "رقم الهوية يبدأ بـ 1 أو 2 (أو 7 للسجل الموحد).", "صحّح في الملف");
            }
            var mobile = Cell(r, "mobile");
            if (mobile is not null && !CaseFactory.IsValidSaudiMobile(mobile)) Err("mobile", "mobile_format", "صيغة غير صحيحة؛ يبدأ بـ 05", "اكتب الرقم بصيغة 05XXXXXXXX");

            var region = Cell(r, "region");
            if (region is not null && !Regions.Contains(region)) Err("region", "region_unknown", $"قيمة غير معروفة «{region}»؛ اختر من القائمة", "اختر من قائمة المناطق في القالب");
            var product = Cell(r, "product_type");
            if (product is not null && !CaseDraftEndpoints.ProductTypes.Contains(product)) Err("product_type", "product_unknown", $"نوع منتج غير معروف «{product}»", "اختر من قائمة المنتجات");

            decimal? Amount(string key, bool positive = false)
            {
                var raw = Cell(r, key);
                if (raw is null) return null;
                var normalized = raw.Replace(",", "").Replace(" ", "");
                if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var val))
                {
                    Err(key, "amount_format", $"نص «{raw}» بدل رقم؛ استخدم {Columns.First(c => c.Key == key).Example}", "اكتب المبلغ رقماً بخانتين عشريتين");
                    return null;
                }
                if (val < 0) { Err(key, "amount_negative", "المبلغ لا يكون سالباً.", "صحّح في الملف"); return null; }
                if (positive && val == 0) { Err(key, "amount_zero", "المبلغ يجب أن يكون أكبر من صفر.", "صحّح في الملف"); return null; }
                return Math.Round(val, 2);
            }
            var principal = Amount("principal", positive: true);
            var profit = Amount("profit");
            var late = Amount("late_fees");
            var other = Amount("other_fees");
            var outstanding = Amount("outstanding_amount", positive: true);
            Amount("original_amount");
            Amount("original_installment");
            Amount("arrears_amount");
            if (principal is not null && outstanding is not null && issues.All(i => i.Code is not "amount_format" and not "amount_negative"))
            {
                var sum = principal.Value + (profit ?? 0) + (late ?? 0) + (other ?? 0);
                if (sum != outstanding) Err("outstanding_amount", "total_mismatch", $"المبلغ القائم {outstanding:N2} لا يساوي مجموع البنود {sum:N2}.", "صحّح البنود أو المبلغ القائم");
            }
            var arrearsCount = Cell(r, "arrears_installments");
            if (arrearsCount is not null && !(int.TryParse(arrearsCount, NumberStyles.None, CultureInfo.InvariantCulture, out var ac) && ac <= 360))
                Err("arrears_installments", "integer_format", "عدد الأقساط رقم صحيح بين 0 و360.", "صحّح في الملف");

            void DateCheck(string key, string futureMessage)
            {
                var raw = Cell(r, key);
                if (raw is null) return;
                if (!DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                    Err(key, "date_format", $"صيغة التاريخ غير صحيحة «{raw}»", "استخدم الصيغة YYYY-MM-DD");
                else if (d > today) Err(key, "date_future", $"تاريخ مستقبلي {raw}", futureMessage);
            }
            DateCheck("first_overdue_date", "أدخل تاريخ أول تأخر الفعلي");
            DateCheck("contract_date", "أدخل تاريخ العقد الفعلي");

            // Stored row: no plaintext identity or phone.
            var tmp = new CaseParty { FullName = "-", DisplayName = "-" };
            if (idDigits is { Length: 10 }) factory.SetNationalId(tmp, idDigits);
            if (mobile is not null && CaseFactory.IsValidSaudiMobile(mobile)) factory.SetPhone(tmp, mobile);
            var stored = Columns.Where(c => c.Key is not ("national_id" or "mobile")).ToDictionary(c => c.Key, c => Cell(r, c.Key));
            stored["contract_number"] = contract;
            stored["national_id_enc"] = tmp.NationalIdEnc;
            stored["national_id_hash"] = tmp.NationalIdHash;
            stored["national_id_masked"] = tmp.NationalIdMasked ?? (idDigits is { Length: > 0 } ? Mask.NationalId(idDigits) : null);
            stored["mobile_enc"] = tmp.PhoneEnc;
            stored["mobile_masked"] = tmp.PhoneMasked ?? (mobile is null ? null : Mask.Phone(mobile));

            var row = new ImportRow
            {
                BatchId = batch.Id, RowNumber = rowNumber, ContractNumberMasked = contract is null ? "—" : Mask.Reference(contract),
                RawJson = JsonSerializer.Serialize(stored, Json), Status = issues.Count > 0 ? ImportRowStatus.Error : ImportRowStatus.Ready,
                Issues = issues.Select(i => JsonSerializer.Serialize(i, Json)).ToList(),
            };

            if (row.Status == ImportRowStatus.Ready && contract is not null)
            {
                var match = existing.Where(e => e.ContractNumber == contract)
                    .OrderBy(e => CaseStatusInfo.IsTerminal(e.Status)).ThenByDescending(e => e.StatusChangedAt).FirstOrDefault();
                if (match is not null)
                {
                    var open = !CaseStatusInfo.IsTerminal(match.Status);
                    row.Status = ImportRowStatus.Duplicate;
                    row.DuplicateOfCaseRef = match.Reference;
                    row.Issues.Add(JsonSerializer.Serialize(open
                        ? new ImportIssue("contract_number", "رقم العقد", "open_case", match.Status == CaseStatus.Draft ? $"مرتبط بمسودة مفتوحة {match.Reference}" : $"مرتبط بحالة مفتوحة {match.Reference}", "تخطي / ربط", "duplicate")
                        : new ImportIssue("contract_number", "رقم العقد", "closed_case", $"العقد مرتبط بحالة مغلقة {match.Reference} ({(match.ClosedAt ?? match.StatusChangedAt):yyyy-MM})", "إنشاء مع سبب", "duplicate"), Json));
                }
            }
            pending.Add((row, tmp.NationalIdHash));
        }

        // Same owner (identity hash) in a closed case of this organization → possible duplicate, create only with a reason.
        var hashes = pending.Where(p => p.Row.Status == ImportRowStatus.Ready && p.IdHash is not null).Select(p => p.IdHash!).Distinct().ToList();
        if (hashes.Count > 0)
        {
            var closedOwners = await db.Parties.IgnoreQueryFilters()
                .Where(p => p.OrganizationId == orgId && p.NationalIdHash != null && hashes.Contains(p.NationalIdHash))
                .Join(db.Cases.IgnoreQueryFilters(), p => p.CaseId, c => c.Id, (p, c) => new { p.NationalIdHash, c.Reference, c.Status, c.ClosedAt, c.StatusChangedAt })
                .Where(x => x.Status == CaseStatus.Closed || x.Status == CaseStatus.Cancelled)
                .ToListAsync();
            foreach (var (row, hash) in pending.Where(p => p.Row.Status == ImportRowStatus.Ready && p.IdHash is not null))
            {
                var m = closedOwners.Where(x => x.NationalIdHash == hash).OrderByDescending(x => x.ClosedAt ?? x.StatusChangedAt).FirstOrDefault();
                if (m is null) continue;
                row.Status = ImportRowStatus.Duplicate;
                row.DuplicateOfCaseRef = m.Reference;
                row.Issues.Add(JsonSerializer.Serialize(new ImportIssue("national_id", "رقم الهوية", "same_owner_closed",
                    $"نفس المالك في حالة مغلقة {(m.ClosedAt ?? m.StatusChangedAt):yyyy-MM}", "إنشاء مع سبب", "duplicate"), Json));
            }
        }

        batch.Rows = pending.Select(p => p.Row).ToList();
        Recount(batch);
        db.ImportBatches.Add(batch);
        return batch;
    }

    public static void Recount(ImportBatch b)
    {
        b.TotalRows = b.Rows.Count;
        b.ReadyCount = b.Rows.Count(r => r.Status == ImportRowStatus.Ready);
        b.DuplicateCount = b.Rows.Count(r => r.Status == ImportRowStatus.Duplicate);
        b.ErrorCount = b.Rows.Count(r => r.Status == ImportRowStatus.Error);
        b.ImportedCount = b.Rows.Count(r => r.Status == ImportRowStatus.Imported);
    }

    private static string SafeName(string fileName)
    {
        var n = Path.GetFileName(fileName ?? "").Trim();
        if (n.Length == 0) n = "import.csv";
        return n.Length > 200 ? n[^200..] : n;
    }

    // ───────── Commit ─────────

    /// <summary>Creates a Draft case for one row. PII is copied encrypted from the stored row; no owner contact is created.</summary>
    public async Task<Case> CreateDraftAsync(ImportBatch batch, ImportRow row, Guid userId, Guid? membershipId, string mortgagee)
    {
        var raw = ParseRaw(row.RawJson);
        string? V(string k) => raw.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
        decimal D(string k) => V(k) is { } s && decimal.TryParse(s.Replace(",", "").Replace(" ", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? Math.Round(d, 2) : 0m;
        decimal? DN(string k) => V(k) is null ? null : D(k);
        DateOnly? Date(string k) => V(k) is { } s && DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        var now = clock.UtcNow;
        var today = clock.TodayRiyadh;

        var c = new Case
        {
            OrganizationId = batch.OrganizationId, Reference = await factory.NextReferenceAsync(today.Year), Status = CaseStatus.Draft, StatusChangedAt = now,
            Source = CaseSource.Import, ImportBatchId = batch.Id, CreatedByUserId = userId, AssignedManagerId = membershipId, OpenedOn = today, DraftStep = 6,
            ProductType = V("product_type") ?? "تمويل سكني · مرابحة", City = V("city"), Region = V("region"),
            DuplicateOverrideReason = row.Decision == "create_with_reason" ? row.DecisionReason : null,
        };
        var principal = D("principal");
        var total = principal + D("profit") + D("late_fees") + D("other_fees");
        var source = $"استيراد ملف · {batch.FileName}";
        c.OutstandingAmount = total;
        c.OutstandingAsOf = batch.CreatedAt;
        c.OutstandingSource = source;
        c.ArrearsAmount = DN("arrears_amount");
        c.ArrearsInstallments = V("arrears_installments") is { } ai && int.TryParse(ai, out var n) ? n : null;
        c.ArrearsSince = Date("first_overdue_date");
        db.Cases.Add(c);

        var idDigitsKind = V("national_id_masked") is { } masked && masked.StartsWith('7') ? PartyKind.Organization : PartyKind.Individual;
        var party = factory.NewParty(c.OrganizationId, c.Id, PartyRole.OwnerBorrower, V("owner_name")!, null, null, primary: true, kind: idDigitsKind);
        party.NationalIdEnc = V("national_id_enc");
        party.NationalIdHash = V("national_id_hash");
        party.NationalIdMasked = V("national_id_masked");
        party.PhoneEnc = V("mobile_enc");
        party.PhoneMasked = V("mobile_masked");
        db.Parties.Add(party);

        db.FinancingContracts.Add(new FinancingContract
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, ContractNumber = V("contract_number")!, ContractDate = Date("contract_date"),
            OriginalAmount = DN("original_amount"), OriginalInstallment = DN("original_installment"), FirstOverdueDate = Date("first_overdue_date"),
        });
        var type = V("property_type") ?? "عقار سكني";
        var p = new Property
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Type = type, City = V("city")!, District = V("district"),
            ShortLabel = $"{type.Split('·')[0].Trim()}، {V("district") ?? ""}، {V("city")}".Replace("، ،", "،"),
        };
        db.Properties.Add(p);
        db.Mortgages.Add(new Mortgage { OrganizationId = c.OrganizationId, CaseId = c.Id, Mortgagee = mortgagee });
        db.DebtSnapshots.Add(new DebtSnapshot
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Source = source, AsOf = batch.CreatedAt, SyncStatus = SyncStatus.Manual,
            Principal = principal, Profit = D("profit"), LateFees = D("late_fees"), OtherFees = D("other_fees"), Total = total, RecordedByUserId = userId,
        });
        row.Status = ImportRowStatus.Imported;
        row.CreatedCaseId = c.Id;
        return c;
    }
}
