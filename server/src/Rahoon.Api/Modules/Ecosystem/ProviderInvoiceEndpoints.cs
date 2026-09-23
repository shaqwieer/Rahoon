using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Ecosystem;

public sealed record CreateInvoiceBody(string AssignmentReference);
public sealed record InvoiceReviewBody(string Decision, string? Reason);
public sealed record InvoicePaymentBody(string Reference);

/// <summary>
/// Delivery → invoice → approval (V07). The provider issues an invoice only from an assignment that is delivered and
/// accepted, for the amount in the institution's framework agreement; the institution's invoice.approve holder (never
/// the submitter) approves or rejects with a reason; payment happens outside the platform and only its reference is
/// recorded. Invoices are plain rows scoped explicitly: provider side by ProviderOrganizationId, lender side by
/// LenderOrganizationId.
/// </summary>
public static class ProviderInvoiceEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var p = app.MapGroup("/api/provider").RequireOrg(OrganizationKind.ServiceProvider);
        p.MapGet("/invoices", ProviderList).RequireAnyPermission(P.InvoiceSubmit, P.AssignmentWork);
        p.MapGet("/invoices/invoiceable", Invoiceable).RequirePermission(P.InvoiceSubmit);
        p.MapPost("/invoices", Create).RequirePermission(P.InvoiceSubmit).Idempotent();
        p.MapPost("/invoices/{number}/submit", Submit).RequirePermission(P.InvoiceSubmit).Idempotent();
        p.MapGet("/performance", Performance).RequireAnyPermission(P.InvoiceSubmit, P.AssignmentWork);

        var l = app.MapGroup("/api/institution/provider-invoices").RequireOrg(OrganizationKind.Lender).RequirePermission(P.InvoiceApprove);
        l.MapGet("", LenderList);
        l.MapPost("/{id:guid}/review", Review).Idempotent();
        l.MapPost("/{id:guid}/payment", RecordPayment).Idempotent();
    }

    public static (string Label, string Tone) StatusView(ProviderInvoice i) => i.Status switch
    {
        ProviderInvoiceStatus.Draft => ("مسودة", "neutral"),
        ProviderInvoiceStatus.UnderReview => ("قيد المراجعة", "warn"),
        ProviderInvoiceStatus.Approved => ("معتمدة للسداد", "info"),
        ProviderInvoiceStatus.Paid => ("مدفوعة · مرجع مسجل", "ok"),
        _ => ($"مرفوضة: {i.RejectionReason}", "err"),
    };

    /// <summary>Assignments of the caller's provider org (explicit filter; historical rows are no longer data-accessible).</summary>
    private static IQueryable<ProviderAssignment> MyAssignments(RahoonDbContext db, RequestContext rc) =>
        db.Assignments.IgnoreQueryFilters().Where(a => a.ProviderOrganizationId == rc.OrganizationId);

    private static async Task<IResult> ProviderList(RahoonDbContext db, RequestContext rc)
    {
        var rows = await db.Set<ProviderInvoice>().AsNoTracking().Where(i => i.ProviderOrganizationId == rc.OrganizationId)
            .OrderByDescending(i => i.IssuedOn).ThenByDescending(i => i.CreatedAt).ToListAsync();
        var lenders = await LenderNamesAsync(db, rows.Select(r => r.LenderOrganizationId));
        return Results.Ok(new
        {
            items = rows.Select(i =>
            {
                var (label, tone) = StatusView(i);
                return new
                {
                    i.Id, number = i.Number, assignment = $"{i.AssignmentReference} · {lenders.GetValueOrDefault(i.LenderOrganizationId)}", i.Amount, date = i.IssuedOn,
                    status = i.Status.ToString(), statusLabel = label, tone, i.RejectionReason, i.PaymentReference,
                    canSubmit = i.Status == ProviderInvoiceStatus.Draft,
                };
            }),
            note = "الفاتورة تُرسل للمنشأة المكلِّفة؛ السداد خارج المنصة ويُسجل مرجعه.",
        });
    }

    private static async Task<Dictionary<Guid, string>> LenderNamesAsync(RahoonDbContext db, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Organizations.AsNoTracking().Where(o => list.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.NameAr);
    }

    private static async Task<IResult> Invoiceable(RahoonDbContext db, RequestContext rc)
    {
        var rows = await MyAssignments(db, rc).AsNoTracking()
            .Where(a => a.DeliveredAt != null && (a.Status == AssignmentStatus.Accepted || a.Status == AssignmentStatus.Closed)
                        && !db.Set<ProviderInvoice>().Any(i => i.AssignmentId == a.Id && i.Status != ProviderInvoiceStatus.Rejected))
            .OrderByDescending(a => a.DeliveredAt).Take(100)
            .Select(a => new { a.Reference, a.OrganizationId, a.Title, a.DeliveredAt }).ToListAsync();
        var lenders = await LenderNamesAsync(db, rows.Select(r => r.OrganizationId));
        var fees = await db.Set<InstitutionProvider>().IgnoreQueryFilters().Where(e => e.ProviderOrganizationId == rc.OrganizationId)
            .ToDictionaryAsync(e => e.OrganizationId, e => e.FrameworkFee);
        return Results.Ok(new
        {
            items = rows.Select(r => new { assignment = r.Reference, lender = lenders.GetValueOrDefault(r.OrganizationId), r.Title, r.DeliveredAt, amount = fees.GetValueOrDefault(r.OrganizationId) }),
        });
    }

    private static async Task<IResult> Create(CreateInvoiceBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit)
    {
        var a = await MyAssignments(db, rc).FirstOrDefaultAsync(x => x.Reference == req.AssignmentReference) ?? throw new NotFoundException();
        if (a.DeliveredAt is null || a.Status is not (AssignmentStatus.Accepted or AssignmentStatus.Closed))
            throw new DomainException("not_invoiceable", "الفاتورة تُنشأ من تكليف مُسلَّم ومقبول فقط.");
        if (await db.Set<ProviderInvoice>().AnyAsync(i => i.AssignmentId == a.Id && i.Status != ProviderInvoiceStatus.Rejected))
            throw new ConflictException("invoice_exists", "توجد فاتورة لهذا التكليف.");
        var agreement = await db.Set<InstitutionProvider>().IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.OrganizationId == a.OrganizationId && e.ProviderOrganizationId == rc.OrganizationId);
        var amount = agreement?.FrameworkFee ?? a.FeeAmount ?? throw new DomainException("no_framework_fee", "لا توجد رسوم في الاتفاقية الإطارية لهذا التكليف.");
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == rc.OrganizationId);
        var year = clock.TodayRiyadh.Year;
        var key = $"invoice:{org.ShortCode}:{year}";
        var seq = await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 201)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync();
        var invoice = new ProviderInvoice
        {
            Number = $"INV-{org.ShortCode.ToUpperInvariant()}-{year}-{seq[0]:D3}", AssignmentId = a.Id, AssignmentReference = a.Reference,
            LenderOrganizationId = a.OrganizationId, ProviderOrganizationId = rc.OrganizationId!.Value, Amount = amount, IssuedOn = clock.TodayRiyadh, CreatedByUserId = rc.UserId,
        };
        db.Set<ProviderInvoice>().Add(invoice);
        await audit.RecordAsync(new AuditEntry("invoice.created", $"إنشاء فاتورة {invoice.Number} من التكليف {a.Reference}", Detail: $"{amount:N2} ر.س من الاتفاقية الإطارية", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { number = invoice.Number, amount, status = invoice.Status.ToString() });
    }

    private static async Task<IResult> Submit(string number, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var i = await db.Set<ProviderInvoice>().FirstOrDefaultAsync(x => x.Number == number && x.ProviderOrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (i.Status != ProviderInvoiceStatus.Draft) throw new ConflictException("not_draft", "الفاتورة مُرسلة مسبقاً.");
        i.Status = ProviderInvoiceStatus.UnderReview;
        i.SubmittedByUserId = rc.UserId;
        i.SubmittedAt = clock.UtcNow;
        var reviewers = await db.Memberships.IgnoreQueryFilters()
            .Where(m => m.OrganizationId == i.LenderOrganizationId && m.Status == MembershipStatus.Active && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.InvoiceApprove)))
            .Select(m => m.UserId).ToListAsync();
        foreach (var u in reviewers) notifier.Notify(u, i.LenderOrganizationId, "invoice", $"فاتورة مقدم خدمة للمراجعة {i.Number}", $"{i.Amount:N2} ر.س · {i.AssignmentReference}", "/settings/providers/invoices");
        await audit.RecordAsync(new AuditEntry("invoice.submitted", $"إرسال الفاتورة {i.Number} للمراجعة", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = i.Status.ToString() });
    }

    private static async Task<IResult> Performance(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        // Provider's own view across the institutions it served; institutions only ever see their own slice (conflict #4).
        var since = clock.UtcNow.AddMonths(-12);
        var rows = await MyAssignments(db, rc).AsNoTracking().Where(a => a.DeliveredAt != null && a.DeliveredAt >= since)
            .Select(a => new { a.DueOn, a.DeliveredAt, Reworked = db.AssignmentSubmissions.IgnoreQueryFilters().Any(s => s.AssignmentId == a.Id && s.Status == SubmissionStatus.Returned) })
            .ToListAsync();
        var n = rows.Count;
        var onTime = rows.Count(r => DateOnly.FromDateTime(r.DeliveredAt!.Value.ToOffset(TimeSpan.FromHours(3)).DateTime) <= r.DueOn);
        var reworks = rows.Count(r => r.Reworked);
        decimal? Pct(int x) => n == 0 ? null : Math.Round((decimal)x / n, 4);
        return Results.Ok(new
        {
            title = "أداؤك · 12 شهراً",
            metrics = new object[]
            {
                new { key = "on_time", label = "التسليم في الموعد", value = Pct(onTime), note = $"{onTime} من {n} تكليفاً" },
                new { key = "first_time", label = "التقارير المقبولة من أول مرة", value = Pct(n - reworks), note = reworks == 1 ? "إعادة واحدة" : $"{reworks} إعادات" },
                new { key = "response", label = "متوسط الرد على الاستفسار", value = (decimal?)null, note = "الهدف ≤ 24 ساعة · لا يُقاس بعد" },
                new { key = "complaints", label = "شكاوى مرتبطة", value = (decimal?)0, note = "—" },
            },
            info = "ترى المنشآت أداءك لديها فقط. يمكنك الاعتراض على أي تقييم خلال 14 يوماً.",
        });
    }

    private static async Task<IResult> LenderList(string? status, RahoonDbContext db, RequestContext rc)
    {
        var q = db.Set<ProviderInvoice>().AsNoTracking().Where(i => i.LenderOrganizationId == rc.OrganizationId && i.Status != ProviderInvoiceStatus.Draft);
        if (Enum.TryParse<ProviderInvoiceStatus>(status, true, out var st)) q = q.Where(i => i.Status == st);
        var rows = await q.OrderByDescending(i => i.IssuedOn).Take(200).ToListAsync();
        var providers = await LenderNamesAsync(db, rows.Select(r => r.ProviderOrganizationId));
        var fees = await db.Set<InstitutionProvider>().Where(e => e.OrganizationId == rc.OrganizationId).ToDictionaryAsync(e => e.ProviderOrganizationId, e => e.FrameworkFee);
        return Results.Ok(new
        {
            items = rows.Select(i =>
            {
                var (label, tone) = StatusView(i);
                var expected = fees.GetValueOrDefault(i.ProviderOrganizationId);
                return new
                {
                    i.Id, number = i.Number, provider = providers.GetValueOrDefault(i.ProviderOrganizationId), assignment = i.AssignmentReference, i.Amount, expectedAmount = expected,
                    amountMatches = expected is null || expected == i.Amount, date = i.IssuedOn, status = i.Status.ToString(), statusLabel = label, tone,
                    canReview = i.Status == ProviderInvoiceStatus.UnderReview && i.SubmittedByUserId != rc.UserId, canRecordPayment = i.Status == ProviderInvoiceStatus.Approved,
                };
            }),
        });
    }

    private static async Task<IResult> Review(Guid id, InvoiceReviewBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        var decision = req.Decision?.ToLowerInvariant();
        new Validator().Require(decision is "approve" or "reject", "decision", "اختر القرار.")
            .Require(decision != "reject" || (req.Reason?.Trim().Length ?? 0) >= 5, "reason", "سبب الرفض إلزامي.").ThrowIfInvalid();
        var i = await db.Set<ProviderInvoice>().FirstOrDefaultAsync(x => x.Id == id && x.LenderOrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (i.Status != ProviderInvoiceStatus.UnderReview) throw new ConflictException("not_under_review", "الفاتورة ليست قيد المراجعة.");
        // Maker-checker: whoever submitted the invoice can never approve or reject it.
        if (i.SubmittedByUserId == rc.UserId || i.CreatedByUserId == rc.UserId) throw new ForbiddenException("لا يعتمد الفاتورة من أرسلها (فصل المهام).");
        if (decision == "approve")
        {
            var fee = await db.Set<InstitutionProvider>().Where(e => e.OrganizationId == rc.OrganizationId && e.ProviderOrganizationId == i.ProviderOrganizationId).Select(e => e.FrameworkFee).FirstOrDefaultAsync();
            if (fee is { } f && f != i.Amount)
                throw new DomainException("amount_mismatch", $"المبلغ مختلف عن الاتفاقية الإطارية ({f:N2}). ارفض الفاتورة بسبب.");
        }
        i.Status = decision == "approve" ? ProviderInvoiceStatus.Approved : ProviderInvoiceStatus.Rejected;
        i.DecidedByUserId = rc.UserId;
        i.DecidedAt = clock.UtcNow;
        i.RejectionReason = decision == "reject" ? req.Reason!.Trim() : null;
        if (i.SubmittedByUserId is { } submitter)
            notifier.Notify(submitter, i.ProviderOrganizationId, "invoice", decision == "approve" ? $"اعتُمدت الفاتورة {i.Number} للسداد" : $"رُفضت الفاتورة {i.Number}", i.RejectionReason, "/provider/invoices", tone: decision == "approve" ? "ok" : "err");
        await audit.RecordAsync(new AuditEntry("invoice.decision", $"{(decision == "approve" ? "اعتماد" : "رفض")} فاتورة مقدم الخدمة {i.Number}", Reason: i.RejectionReason,
            Detail: $"{i.Amount:N2} ر.س · {i.AssignmentReference}", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = i.Status.ToString() });
    }

    private static async Task<IResult> RecordPayment(Guid id, InvoicePaymentBody req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
    {
        new Validator().Require(!string.IsNullOrWhiteSpace(req.Reference) && req.Reference.Trim().Length <= 60, "reference", "مرجع التحويل إلزامي.").ThrowIfInvalid();
        var i = await db.Set<ProviderInvoice>().FirstOrDefaultAsync(x => x.Id == id && x.LenderOrganizationId == rc.OrganizationId) ?? throw new NotFoundException();
        if (i.Status != ProviderInvoiceStatus.Approved) throw new ConflictException("not_approved", "يُسجل مرجع السداد لفاتورة معتمدة فقط.");
        i.Status = ProviderInvoiceStatus.Paid;
        i.PaymentReference = req.Reference.Trim();
        i.PaidAt = clock.UtcNow;
        i.PaidRecordedByUserId = rc.UserId;
        if (i.SubmittedByUserId is { } submitter)
            notifier.Notify(submitter, i.ProviderOrganizationId, "invoice", $"سُددت الفاتورة {i.Number}", $"المرجع {i.PaymentReference}", "/provider/invoices", tone: "ok");
        await audit.RecordAsync(new AuditEntry("invoice.paid", $"تسجيل مرجع سداد الفاتورة {i.Number}", Detail: $"المرجع {i.PaymentReference} · السداد خارج المنصة", OrganizationId: rc.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new { status = i.Status.ToString(), i.PaymentReference });
    }
}
