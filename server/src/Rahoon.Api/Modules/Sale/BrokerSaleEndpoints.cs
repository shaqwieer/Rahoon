using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;

namespace Rahoon.Api.Modules.Sale;

/// <summary>
/// The assigned broker's view of a sale file (L31 scope). Access goes strictly assignment → sale: only a live
/// Brokerage assignment of the caller's organization that is the sale's current broker assignment. The broker
/// sees the disclosure-matrix projection only — never the owner, case reference, lender, minimum or debt — and
/// loses access at mandate end, on offer approval or on owner withdrawal.
/// </summary>
public static class BrokerSaleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/broker/sales").RequireOrg(OrganizationKind.ServiceProvider).RequirePermission(P.AssignmentWork);
        g.MapGet("", List);
        g.MapGet("/{buyerReference}", Detail);
        g.MapPost("/{buyerReference}/offers", AddOffer).Idempotent();
    }

    private static IQueryable<ProviderAssignment> LiveAssignments(RahoonDbContext db, RequestContext rc, DateTimeOffset now) =>
        db.Assignments.Where(a => a.ProviderOrganizationId == rc.OrganizationId && a.Type == AssignmentType.Brokerage
                                  && a.Status != AssignmentStatus.Cancelled && a.Status != AssignmentStatus.Closed
                                  && a.AccessExpiresAt != null && a.AccessExpiresAt > now);

    private static async Task<(VoluntarySale Sale, ProviderAssignment Assignment)> LoadAsync(string buyerReference, RahoonDbContext db, RequestContext rc, IClock clock, bool track = false)
    {
        var now = clock.UtcNow;
        var assignment = await LiveAssignments(db, rc, now)
            .Where(a => db.Set<VoluntarySale>().Any(s => s.BrokerAssignmentId == a.Id && s.BuyerReference == buyerReference)).FirstOrDefaultAsync()
            ?? throw new NotFoundException();
        var q = db.Set<VoluntarySale>().Where(s => s.BrokerAssignmentId == assignment.Id && s.BuyerReference == buyerReference && s.Status == SaleStatus.Active);
        if (!track) q = q.AsNoTracking();
        var sale = await q.FirstOrDefaultAsync() ?? throw new NotFoundException();
        return (sale, assignment);
    }

    private static async Task<IResult> List(RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var now = clock.UtcNow;
        var rows = await LiveAssignments(db, rc, now).AsNoTracking()
            .Join(db.Set<VoluntarySale>().Where(s => s.Status == SaleStatus.Active), a => a.Id, s => s.BrokerAssignmentId, (a, s) => new
            {
                a.Reference, a.Title, a.PropertyLabel, a.AccessExpiresAt, a.FeesLabel, s.BuyerReference, s.ListingStatus,
            }).ToListAsync();
        return Results.Ok(new
        {
            items = rows.Select(r => new
            {
                assignment = r.Reference, buyerReference = r.BuyerReference, title = r.Title, property = r.PropertyLabel, accessExpiresAt = r.AccessExpiresAt,
                commission = r.FeesLabel, listingReady = r.ListingStatus == ListingStatus.ComplianceReviewed,
            }),
        });
    }

    private static async Task<IResult> Detail(string buyerReference, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales)
    {
        var (s, a) = await LoadAsync(buyerReference, db, rc, clock);
        var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == s.CaseId);
        var src = await SaleEndpoints.ListingSourceAsync(db, c, s, sales);
        var ready = s.ListingStatus == ListingStatus.ComplianceReviewed;
        var offers = await db.Set<BuyerOffer>().AsNoTracking().Where(o => o.SaleId == s.Id && o.EnteredBySide == "broker").OrderBy(o => o.Code).ToListAsync();
        return Results.Ok(new
        {
            assignment = a.Reference, accessExpiresAt = a.AccessExpiresAt, commission = a.FeesLabel,
            scopeNote = "ملف البيع فقط — لا يشمل بيانات المالك أو الحالة أو التمويل. ليس إعلاناً عاماً.",
            listingReady = ready, listingNote = ready ? null : "بانتظار مراجعة الامتثال لملخص العرض قبل مشاركته مع المشترين.",
            brokerView = ready ? DisclosurePolicy.Project(src, DisclosureAudience.Broker) : null,
            buyerPreview = ready ? DisclosurePolicy.Project(src, DisclosureAudience.QualifiedBuyer) : null,
            offers = offers.Select(o => new
            {
                code = o.Code, price = o.Price, payment = SaleService.PaymentLabel(o.PaymentMethod), o.ValidUntil, status = SaleService.OfferStatusLabel(o.Status),
            }),
        });
    }

    private static async Task<IResult> AddOffer(string buyerReference, BuyerOfferBody req, RahoonDbContext db, RequestContext rc, IClock clock, SaleService sales, AuditLog audit)
    {
        SaleEndpoints.ValidateOffer(req, clock.TodayRiyadh);
        await using var tx = await db.Database.BeginTransactionAsync();
        var (s, a) = await LoadAsync(buyerReference, db, rc, clock, track: true);
        if (s.ListingStatus != ListingStatus.ComplianceReviewed) throw new ConflictException("listing_not_ready", "لا تُقبل العروض قبل مراجعة الامتثال لملخص العرض.");
        await sales.RequireSignedConsentAsync(s);
        var offer = SaleEndpoints.NewOffer(s, req, "broker", rc.UserId, clock.UtcNow, await SaleEndpoints.NextOfferCodeAsync(db, s.Id));
        db.Set<BuyerOffer>().Add(offer);
        var c = await db.Cases.AsNoTracking().FirstAsync(x => x.Id == s.CaseId);
        await sales.NotifyManagerAsync(c, $"عرض شراء جديد {offer.Code} عبر الوسيط", $"{offer.Price:N0} ر.س · {SaleService.PaymentLabel(offer.PaymentMethod)}", $"/cases/{c.Reference}/sale/offers");
        await audit.RecordAsync(new AuditEntry("sale.offer_received", $"تسجيل عرض شراء {offer.Code} عبر الوسيط", c.Id, c.Reference,
            Detail: $"{offer.Price:N2} · {SaleService.PaymentLabel(offer.PaymentMethod)} · {a.Reference}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { code = offer.Code, status = SaleService.OfferStatusLabel(offer.Status) });
    }
}
