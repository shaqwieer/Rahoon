using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Cases;

public sealed record CorrectionRequest(string? Field, string? CurrentValue, string? ProposedValue, string? Note, string? Month);

/// <summary>
/// L07 financing &amp; debt. Read-only figures with their source and sync time; the total is Σ items.
/// «طلب تصحيح بيانات» creates a task for Finance and never edits a figure.
/// </summary>
public static class CaseFinanceEndpoints
{
    public static readonly Dictionary<string, string> CorrectionFields = new()
    {
        ["principal"] = "أصل التمويل المتبقي",
        ["profit"] = "الأرباح المستحقة",
        ["late_fees"] = "غرامات التأخير",
        ["fees"] = "الرسوم",
        ["contract_number"] = "رقم العقد",
        ["contract_date"] = "تاريخ العقد",
        ["original_amount"] = "مبلغ التمويل الأصلي",
        ["original_term"] = "المدة الأصلية",
        ["remaining_term"] = "المدة المتبقية",
        ["original_installment"] = "القسط الأصلي",
        ["first_overdue_date"] = "أول قسط متأخر",
        ["installment_history"] = "سجل الأقساط",
        ["other"] = "بند آخر",
    };

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("/finance", Get);
        g.MapPost("/finance/correction-requests", RequestCorrection).RequireAnyPermission(P.CaseEdit, P.AnalysisEdit).Idempotent();
    }

    private static (string Key, string Label, string Icon) HistoryStatus(InstallmentHistoryStatus s) => s switch
    {
        InstallmentHistoryStatus.Paid => ("paid", "مسدد", "check_circle"),
        InstallmentHistoryStatus.Unpaid => ("unpaid", "غير مسدد", "cancel"),
        InstallmentHistoryStatus.Partial => ("partial", "جزئي", "contrast"),
        _ => ("due", "مستحق", "schedule"),
    };

    private static async Task<IResult> Get(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var c = await access.GetAsync(reference, track: false);
        var org = await db.Organizations.AsNoTracking().Where(o => o.Id == c.OrganizationId).Select(o => o.NameAr).FirstAsync();
        var f = await db.FinancingContracts.AsNoTracking().FirstOrDefaultAsync(x => x.CaseId == c.Id);
        var s = await db.DebtSnapshots.AsNoTracking().Where(x => x.CaseId == c.Id && x.IsCurrent).OrderByDescending(x => x.AsOf).FirstOrDefaultAsync();
        var history = (await db.InstallmentHistory.AsNoTracking().Where(x => x.CaseId == c.Id).OrderByDescending(x => x.Month).Take(12).ToListAsync())
            .OrderBy(x => x.Month).ToList();

        object? snapshot = null;
        object? banner = null;
        if (s is not null)
        {
            var asOfLocal = s.AsOf.ToOffset(TimeSpan.FromHours(3));
            var sourceLine = $"{s.Source} · {asOfLocal:yyyy-MM-dd}";
            var items = new (string Code, string Label, decimal Amount)[]
            {
                ("principal", "أصل التمويل المتبقي", s.Principal), ("profit", "الأرباح المستحقة", s.Profit),
                ("late_fees", "غرامات التأخير", s.LateFees), ("fees", "الرسوم", s.OtherFees),
            };
            var sum = items.Sum(i => i.Amount);
            var ageHours = (clock.UtcNow - s.AsOf).TotalHours;
            var sync = s.SyncStatus switch
            {
                SyncStatus.Failed => "failed",
                SyncStatus.Manual => "manual",
                _ when s.SyncStatus == SyncStatus.Delayed || ageHours > 24 => "delayed",
                _ => "ok",
            };
            snapshot = new
            {
                source = s.Source, asOf = s.AsOf, syncStatus = sync, ageHours = Math.Round(ageHours, 1),
                staleTag = sync == "failed" ? "غير محدث" : null,
                items = items.Select(i => new { code = i.Code, label = i.Label, amount = i.Amount, source = i.Amount == 0 && i.Code == "fees" ? null : sourceLine, asOf = s.AsOf }),
                total = sum, totalSource = "مجموع البنود",
                recordedTotal = s.Total, totalVerified = sum == s.Total,
                totalWarning = sum == s.Total ? null : $"المجموع المحسوب {sum:N2} لا يطابق الإجمالي المسجل {s.Total:N2}؛ اطلب تصحيح البيانات.",
            };
            banner = sync switch
            {
                "failed" => new { tone = "err", icon = "sync_problem", text = $"فشلت آخر مزامنة مع {s.Source}؛ نعرض آخر قيمة معروفة ({asOfLocal:yyyy-MM-dd HH:mm}) بوسم «غير محدث»." },
                "delayed" => new { tone = "warn", icon = "database", text = $"تأخرت المزامنة أكثر من 24 ساعة. المبالغ مستوردة من {s.Source} وللقراءة فقط. آخر مزامنة {asOfLocal:yyyy-MM-dd HH:mm}." },
                "manual" => new { tone = "info", icon = "database", text = $"المبالغ مُدخلة من {s.Source} وللقراءة فقط. آخر تحديث {asOfLocal:yyyy-MM-dd HH:mm}." },
                _ => new { tone = "info", icon = "database", text = $"المبالغ مستوردة من {s.Source} وللقراءة فقط. آخر مزامنة {asOfLocal:yyyy-MM-dd HH:mm}." },
            };
        }

        var months = history.Select(h =>
        {
            var (key, label, icon) = HistoryStatus(h.Status);
            return new { month = h.Month, shortLabel = h.Month.Length == 7 ? h.Month[2..] : h.Month, status = key, label, icon, h.AmountDue, h.AmountPaid, aria = $"{h.Month}: {label}" };
        }).ToList();
        var missed = history.Where(h => h.Status is InstallmentHistoryStatus.Unpaid or InstallmentHistoryStatus.Partial).ToList();
        var partial = history.Where(h => h.Status == InstallmentHistoryStatus.Partial).ToList();
        var since = missed.FirstOrDefault()?.Month;
        var shortfall = missed.Sum(h => h.AmountDue - h.AmountPaid);
        // Arrears amount comes from the core system (source of truth); the history only explains it.
        var reported = c.ArrearsAmount;
        string? summary = missed.Count == 0 ? "لا أقساط غير مسددة في آخر 12 شهراً." :
            $"{CaseStaffing.Installments(missed.Count)} غير مسددة منذ {since} · مجموعها {(reported ?? shortfall):N2} ر.س"
            + (partial.Count == 1 ? $" · دفعة جزئية واحدة في {partial[0].Month} لم تغطِّ القسط." : partial.Count > 1 ? $" · {partial.Count} دفعات جزئية لم تغطِّ القسط." : ".");

        return Results.Ok(new
        {
            banner,
            snapshot,
            contract = f is null ? null : new
            {
                contractNumberMasked = Mask.Reference(f.ContractNumber), f.ContractDate, productType = c.ProductType, lender = org,
                f.OriginalAmount, f.OriginalTermMonths, f.RemainingTermMonths, f.OriginalInstallment, f.ProfitType, f.FirstOverdueDate,
                termLabel = f.OriginalTermMonths is null ? null : $"{f.OriginalTermMonths} شهراً" + (f.RemainingTermMonths is { } rem ? $" (متبقٍ {rem})" : ""),
                profitLabel = $"{f.ProfitType} · حسب العقد",
            },
            history = new
            {
                title = "سجل الأقساط — آخر 12 شهراً",
                originalInstallment = f?.OriginalInstallment,
                months,
                summary,
            },
            arrears = new
            {
                reportedAmount = reported, reportedInstallments = c.ArrearsInstallments, reportedSince = c.ArrearsSince, source = c.OutstandingSource,
                derived = new { missedCount = missed.Count, partialCount = partial.Count, since, shortfall },
            },
            canRequestCorrection = rc.Has(P.CaseEdit) || rc.Has(P.AnalysisEdit),
            correctionFields = CorrectionFields.Select(kv => new { key = kv.Key, label = kv.Value }),
        });
    }

    private static async Task<IResult> RequestCorrection(string reference, CorrectionRequest req, CaseAccess access, RahoonDbContext db, RequestContext rc,
        IClock clock, AuditLog audit, Notifier notifier)
    {
        var field = req.Field?.Trim().ToLowerInvariant();
        new Validator()
            .Require(field is not null && CorrectionFields.ContainsKey(field), "field", "اختر البند المطلوب تصحيحه.")
            .Require(!string.IsNullOrWhiteSpace(req.ProposedValue) && req.ProposedValue.Trim().Length <= 200, "proposedValue", "اكتب القيمة المتوقعة (حتى 200 حرف).")
            .Require(req.CurrentValue is null || req.CurrentValue.Length <= 200, "currentValue", "القيمة الحالية حتى 200 حرف.")
            .Require(!string.IsNullOrWhiteSpace(req.Note) && req.Note.Trim().Length is >= 10 and <= 1000, "note", "اشرح سبب التصحيح ومصدره (10 أحرف على الأقل).")
            .Require(req.Month is null || (req.Month.Length == 7 && DateOnly.TryParse(req.Month + "-01", out _)), "month", "الشهر بصيغة YYYY-MM.")
            .ThrowIfInvalid();

        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference, track: false);
        var finance = await CaseStaffing.PickAsync(db, c.OrganizationId, P.ReconciliationPrepare, [rc.UserId], SystemRoles.Finance)
                      ?? await CaseStaffing.PickAsync(db, c.OrganizationId, P.ReconciliationPrepare, [], SystemRoles.Finance)
                      ?? throw new ConflictException("no_finance", "لا يوجد موظف مالية نشط لاستلام طلب التصحيح.");
        var label = CorrectionFields[field!] + (req.Month is null ? "" : $" ({req.Month})");
        var detail = $"القيمة الحالية: {req.CurrentValue?.Trim() ?? "—"} · المتوقعة: {req.ProposedValue!.Trim()}";
        var task = new CaseTask
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Title = $"طلب تصحيح بيانات: {label} · {c.Reference}", AssigneeUserId = finance.UserId,
            AssigneeRoleKey = SystemRoles.Finance, DueOn = BusinessDays.Add(clock.TodayRiyadh, 2), Kind = "data_correction",
            Link = $"/cases/{c.Reference}/finance", CreatedByUserId = rc.UserId,
        };
        db.Tasks.Add(task);
        notifier.Notify(finance.UserId, c.OrganizationId, "task", $"طلب تصحيح بيانات مالية · {c.Reference}", $"{label} — {detail}. {req.Note!.Trim()}", task.Link, c.Id);
        await audit.RecordAsync(new AuditEntry("finance.correction_requested", $"طلب تصحيح بيانات: {label}", c.Id, c.Reference,
            Reason: req.Note.Trim(), Detail: $"{detail} · أُسند إلى {finance.Name} (المالية) · لم يُعدّل أي رقم", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { taskId = task.Id, assignee = finance.Name, dueOn = task.DueOn });
    }
}
