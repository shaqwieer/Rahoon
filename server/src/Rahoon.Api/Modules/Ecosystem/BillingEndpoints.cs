using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record PlanBody(string NameAr, decimal? MonthlyPrice, int? ActiveCaseLimit, string LimitLabel);
public sealed record SubscriptionBody(string? PlanKey, bool Trial);
public sealed record BillingPaymentBody(string Reference);

/// <summary>
/// Pricing & billing (PA17), platform finance only. Plans are data (the pricing model is an assumption pending
/// product confirmation); usage = live active-case count per institution (an aggregate — no case data crosses the
/// tenant boundary). No payment processing: a payment is recorded by its external reference.
/// </summary>
public static class BillingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/platform/billing").RequireOrg(OrganizationKind.Platform).RequirePermission(P.PlatformBilling);
        g.MapGet("", Overview);
        g.MapPut("/plans/{key}", UpdatePlan).Idempotent();
        g.MapPut("/subscriptions/{institutionId:guid}", UpdateSubscription).Idempotent();
        g.MapPost("/invoices/{number}/payment", RecordPayment).Idempotent();
    }

    private static readonly CaseStatus[] Inactive = [CaseStatus.Draft, CaseStatus.Closed, CaseStatus.Cancelled];

    private static async Task<IResult> Overview(RahoonDbContext db, IClock clock)
    {
        var today = clock.TodayRiyadh;
        var plans = await db.Set<BillingPlan>().AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        var subs = await db.Set<InstitutionSubscription>().AsNoTracking().ToListAsync();
        var orgs = await db.Organizations.AsNoTracking().Where(o => o.Kind == OrganizationKind.Lender).OrderBy(o => o.CreatedAt).ToListAsync();
        // Usage metric only: count of active cases per tenant (aggregate across the tenant boundary, no rows exposed).
        var usage = await db.Cases.IgnoreQueryFilters().Where(c => !Inactive.Contains(c.Status)).GroupBy(c => c.OrganizationId)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var invoices = await db.Set<PlatformInvoice>().AsNoTracking().OrderByDescending(i => i.IssuedOn).ToListAsync();
        return Results.Ok(new
        {
            assumptionBadge = "نموذج التسعير افتراض — يتطلب تأكيد المنتج",
            plans = plans.Select(p => new
            {
                p.Key, name = p.NameAr, price = p.MonthlyPrice, priceLabel = p.MonthlyPrice is { } m ? $"{m:N0} ر.س / شهر" : "حسب الاتفاق", p.ActiveCaseLimit, limit = p.LimitLabel,
                institutions = subs.Count(s => s.PlanKey == p.Key),
            }),
            institutions = orgs.Select(o =>
            {
                var sub = subs.FirstOrDefault(s => s.InstitutionOrganizationId == o.Id);
                var plan = plans.FirstOrDefault(p => p.Key == sub?.PlanKey);
                var inv = invoices.FirstOrDefault(i => i.InstitutionOrganizationId == o.Id);
                var active = usage.GetValueOrDefault(o.Id);
                var (status, tone) = sub is { Trial: true } ? ("تجربة", "neutral")
                    : inv is null ? ("—", "neutral")
                    : inv.Status == PlatformInvoiceStatus.Paid ? ("مدفوعة", "ok")
                    : inv.DueOn < today ? ($"متأخرة {CaseDisplay.Days(today.DayNumber - inv.DueOn.DayNumber)}", "err")
                    : ("صادرة", "info");
                return new
                {
                    id = o.Id, name = o.NameAr, plan = plan?.NameAr ?? "—", planKey = sub?.PlanKey, trial = sub?.Trial ?? false,
                    activeCases = sub is { Trial: true } ? (int?)null : active,
                    overLimit = plan?.ActiveCaseLimit is { } lim && active > lim,
                    invoice = inv?.Number, amount = inv?.Amount, invoiceActiveCases = inv?.ActiveCases, dueOn = inv?.DueOn, status, tone, paymentReference = inv?.PaymentReference,
                };
            }),
            note = "السداد خارج المنصة؛ يُسجَّل المرجع فقط. لا معالجة مدفوعات داخل رهون.",
        });
    }

    private static async Task<IResult> UpdatePlan(string key, PlanBody req, RahoonDbContext db, RequestContext rc, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.NameAr), "nameAr", "اسم الخطة مطلوب.")
            .Require(req.MonthlyPrice is null or >= 0, "monthlyPrice", "السعر لا يكون سالباً.")
            .Require(req.ActiveCaseLimit is null or > 0, "activeCaseLimit", "الحد أكبر من صفر.").ThrowIfInvalid();
        var p = await db.Set<BillingPlan>().FirstOrDefaultAsync(x => x.Key == key) ?? throw new NotFoundException();
        var before = $"{p.MonthlyPrice?.ToString("N0") ?? "حسب الاتفاق"} · {p.LimitLabel}";
        p.NameAr = req.NameAr.Trim();
        p.MonthlyPrice = req.MonthlyPrice;
        p.ActiveCaseLimit = req.ActiveCaseLimit;
        p.LimitLabel = string.IsNullOrWhiteSpace(req.LimitLabel) ? p.LimitLabel : req.LimitLabel.Trim();
        p.UpdatedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("billing.plan_updated", $"تعديل خطة التسعير «{p.NameAr}»", Detail: $"{before} ← {p.MonthlyPrice?.ToString("N0") ?? "حسب الاتفاق"} · {p.LimitLabel}", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { p.Key, p.MonthlyPrice, p.ActiveCaseLimit });
    }

    private static async Task<IResult> UpdateSubscription(Guid institutionId, SubscriptionBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        if (!await db.Organizations.AnyAsync(o => o.Id == institutionId && o.Kind == OrganizationKind.Lender)) throw new NotFoundException();
        if (!req.Trial && (req.PlanKey is null || !await db.Set<BillingPlan>().AnyAsync(p => p.Key == req.PlanKey))) Validate.Throw("planKey", "اختر الخطة.");
        var sub = await db.Set<InstitutionSubscription>().FirstOrDefaultAsync(s => s.InstitutionOrganizationId == institutionId);
        if (sub is null) db.Set<InstitutionSubscription>().Add(sub = new InstitutionSubscription { InstitutionOrganizationId = institutionId, StartedOn = clock.TodayRiyadh });
        sub.PlanKey = req.Trial ? null : req.PlanKey;
        sub.Trial = req.Trial;
        sub.UpdatedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("billing.subscription_updated", "تغيير خطة منشأة", Detail: req.Trial ? "تجربة" : req.PlanKey, OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { sub.PlanKey, sub.Trial });
    }

    private static async Task<IResult> RecordPayment(string number, BillingPaymentBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reference) && req.Reference.Trim().Length <= 60, "reference", "مرجع التحويل إلزامي.").ThrowIfInvalid();
        var inv = await db.Set<PlatformInvoice>().FirstOrDefaultAsync(i => i.Number == number) ?? throw new NotFoundException();
        if (inv.Status == PlatformInvoiceStatus.Paid) throw new ConflictException("already_paid", "الفاتورة مسجلة كمدفوعة.");
        inv.Status = PlatformInvoiceStatus.Paid;
        inv.PaymentReference = req.Reference.Trim();
        inv.PaidAt = clock.UtcNow;
        inv.RecordedByUserId = rc.UserId;
        await audit.RecordAsync(new AuditEntry("billing.invoice_paid", $"تسجيل مرجع سداد {inv.Number}", Detail: $"المرجع {inv.PaymentReference} · السداد خارج المنصة", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = inv.Status.ToString(), inv.PaymentReference });
    }
}
