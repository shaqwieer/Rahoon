using System.Text;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Referral;

namespace Rahoon.Api.Modules.Closure;

public sealed record TraceRow(string Key, string Label, decimal? Amount, string SourceType, string SourceTypeLabel, string Source, string? SourceRef, string? SourceDate)
{
    public bool HasSource => !string.IsNullOrWhiteSpace(SourceRef);
}

/// <summary>Reconciliation arithmetic, the F04 trace and closure prerequisites.</summary>
public sealed class ClosureService(RahoonDbContext db)
{
    public static readonly string[] SaleBases = ["judicial_sale", "voluntary_sale"];
    public static readonly string[] RequiredDocuments = ["final_clearance", "lien_release_letter", "owner_final_summary"];

    public static readonly (string Type, string Title, string Dept, bool Blocks, bool Share)[] StandardDocuments =
    [
        ("final_clearance", "خطاب المخالصة النهائية", "المالية", true, true),
        ("lien_release_letter", "خطاب فك الرهن", "القانونية", true, true),
        // B5/B10 assumption: the authority's confirmation arrives late and does not block closure.
        ("lien_release_submission_proof", "إثبات تقديم طلب فك الرهن للجهة المختصة", "القانونية", false, false),
        ("owner_final_summary", "ملخص الحالة النهائي للمالك", "مولَّد", true, true),
    ];

    public Task<Reconciliation?> CurrentReconciliationAsync(Guid caseId, bool track = true)
    {
        var q = db.Reconciliations.Include(r => r.Lines).Where(r => r.CaseId == caseId);
        if (!track) q = q.AsNoTracking();
        return q.OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
    }

    public Task<Distribution?> CurrentDistributionAsync(Guid caseId, bool track = true)
    {
        var q = db.Set<Distribution>().Include(d => d.Lines).Where(d => d.CaseId == caseId);
        if (!track) q = q.AsNoTracking();
        return q.OrderByDescending(d => d.CreatedAt).FirstOrDefaultAsync();
    }

    /// <summary>received = Σ receipts; waived = Σ waivers; difference = expected − received − waived (mirrors the DB check).</summary>
    public static void Recompute(Reconciliation r)
    {
        r.ReceivedAmount = r.Lines.Where(l => l.Kind == "receipt").Sum(l => l.Amount);
        r.WaivedAmount = r.Lines.Where(l => l.Kind == "waiver").Sum(l => l.Amount);
        r.Difference = r.ExpectedAmount - r.ReceivedAmount - r.WaivedAmount;
    }

    public static bool Editable(Reconciliation r) => r.Status is ReconciliationStatus.Draft or ReconciliationStatus.Returned;

    /// <summary>F04: every figure with its source (official | bank | core_banking | internal). Rows without a reference are flagged.</summary>
    public async Task<List<TraceRow>> TraceAsync(Case c, Reconciliation? r, Distribution? d)
    {
        var rows = new List<TraceRow>();
        if (r is null) return rows;
        static string Day(DateTimeOffset? t) => t?.ToOffset(TimeSpan.FromHours(3)).ToString("yyyy-MM-dd") ?? "";

        var sale = await db.Set<SaleResult>().AsNoTracking().Where(s => s.CaseId == c.Id && s.Status == SaleResultStatus.Confirmed).FirstOrDefaultAsync();
        if (r.Basis == "judicial_sale" && sale is not null)
        {
            rows.Add(new("sale_price", "ثمن البيع", sale.OfficialSalePrice, "official", "رسمي", $"محضر البيع {sale.SaleMinutesDate:yyyy-MM-dd} · {sale.ConfirmationSource}",
                sale.ConfirmationSource, sale.OfficialConfirmationDate?.ToString("yyyy-MM-dd")));
            rows.Add(new("procedure_costs", "تكاليف الإجراء", sale.DeclaredCosts ?? 0m, "official", "رسمي", $"إشعار الجهة · {sale.ConfirmationSource}",
                sale.ConfirmationSource, sale.OfficialConfirmationDate?.ToString("yyyy-MM-dd")));
        }
        else
            rows.Add(new("expected", "المبلغ المتوقع", r.ExpectedAmount, "internal", "داخلي", r.ExpectedSource ?? "—", r.ExpectedSource, Day(r.CreatedAt)));

        foreach (var l in r.Lines.Where(l => l.Kind == "receipt").OrderBy(l => l.ValueDate).ThenBy(l => l.Reference).ThenBy(l => l.Id))
            rows.Add(new($"receipt:{l.Id}", l.Label, l.Amount, "bank", "بنكي", $"{l.Reference} · {l.ValueDate:yyyy-MM-dd}", l.Reference, l.ValueDate?.ToString("yyyy-MM-dd")));
        foreach (var l in r.Lines.Where(l => l.Kind == "waiver").OrderBy(l => l.ValueDate).ThenBy(l => l.Reference).ThenBy(l => l.Id))
            rows.Add(new($"waiver:{l.Id}", l.Label, l.Amount, "internal", "داخلي", l.Reference ?? "—", l.Reference, l.ValueDate?.ToString("yyyy-MM-dd")));
        rows.Add(new("difference", "الفرق", r.Difference, "internal", "داخلي",
            r.Difference == 0 ? "مطابقة صفرية الفرق" : $"فرق مفسَّر: {r.DifferenceExplanation}",
            r.Difference == 0 || !string.IsNullOrWhiteSpace(r.DifferenceExplanation) ? $"reconciliation:{r.Id}" : null, Day(r.SubmittedAt)));

        if (d is not null)
        {
            foreach (var l in d.Lines.OrderBy(l => l.Seq).Where(l => l.Amount > 0))
            {
                var (type, label) = l.Type == "debt_repayment" ? ("core_banking", "نظام التمويل") : ("bank", "بنكي");
                var title = l.Type switch { "debt_repayment" => "المديونية المسددة", "owner_surplus" => "الفائض المحوّل للمالك", _ => l.Label };
                rows.Add(new($"distribution:{l.Id}", title, l.Amount, type, label,
                    l.ExecutedTxnRef is null ? $"{l.BasisRef} · لم يُنفَّذ بعد" : $"{l.ExecutedTxnRef} · {l.ExecutedOn:yyyy-MM-dd} · {l.BasisRef}", l.ExecutedTxnRef, l.ExecutedOn?.ToString("yyyy-MM-dd")));
            }
        }

        var referral = await db.Referrals.AsNoTracking().Where(x => x.CaseId == c.Id && x.ApprovedAt != null).OrderByDescending(x => x.ApprovedAt).FirstOrDefaultAsync();
        if (referral is not null)
            rows.Add(new("referral_decision", "قرار الإحالة", null, "internal", "داخلي", $"اعتماد {Day(referral.ApprovedAt)} · موافقتان", $"referral:{referral.Id}", Day(referral.ApprovedAt)));
        return rows;
    }

    public async Task<List<string>> DocumentProblemsAsync(Guid caseId)
    {
        var docs = await db.ClosureDocuments.AsNoTracking().Where(d => d.CaseId == caseId).ToListAsync();
        var problems = new List<string>();
        foreach (var type in RequiredDocuments)
        {
            var d = docs.FirstOrDefault(x => x.Type == type);
            var title = StandardDocuments.First(s => s.Type == type).Title;
            if (d is null) problems.Add($"{title}: غير مُنشأ");
            else if (d.Status != ClosureDocumentStatus.Ready) problems.Add($"{title}: غير جاهز");
        }
        return problems;
    }

    /// <summary>Closure prerequisites beyond the workflow guards (which check reconciliation approval and blocking documents).</summary>
    public async Task<List<string>> CloseBlockersAsync(Case c, Reconciliation? r, Distribution? d, IReadOnlyList<TraceRow> trace)
    {
        var list = new List<string>();
        if (r is null) list.Add("لا توجد تسوية مالية.");
        else
        {
            if (r.Status != ReconciliationStatus.Approved) list.Add("التسوية المالية غير معتمدة.");
            if (r.Difference != 0 && string.IsNullOrWhiteSpace(r.DifferenceExplanation)) list.Add("فرق غير مفسَّر في المطابقة.");
            if (SaleBases.Contains(r.Basis) && d is not { Status: DistributionStatus.Executed }) list.Add("التوزيع غير معتمد أو غير منفَّذ بمراجع تحويل.");
        }
        list.AddRange(await DocumentProblemsAsync(c.Id));
        var missing = trace.Where(t => !t.HasSource).Select(t => t.Label).ToList();
        if (missing.Count > 0) list.Add("أرقام بلا مصدر: " + string.Join("، ", missing));
        return list;
    }

    public static string BasisLabel(string basis) => basis switch
    {
        "judicial_sale" => "بيع قضائي خارجي",
        "voluntary_sale" => "بيع طوعي",
        "discounted_payoff" => "سداد مخفض",
        _ => "تسوية نقدية",
    };
}

/// <summary>
/// Minimal single-page PDF for generated summaries. Uses a standard Latin font (no Arabic shaping); the Arabic
/// owner-facing summary is served as structured text in the portal. Production needs a proper Arabic PDF renderer.
/// </summary>
public static class SimplePdf
{
    public static byte[] Render(IReadOnlyList<string> lines)
    {
        var content = new StringBuilder("BT /F1 11 Tf 50 790 Td 14 TL\n");
        foreach (var l in lines)
        {
            var safe = new string(l.Select(ch => ch < 128 ? ch : '?').ToArray()).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            content.Append($"({safe}) Tj T*\n");
        }
        content.Append("ET");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(sb.Length);
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = sb.Length;
        sb.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var o in offsets) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
