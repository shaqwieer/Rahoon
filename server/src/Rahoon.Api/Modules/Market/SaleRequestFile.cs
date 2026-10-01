using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Modules.Market;

public sealed record MissingItem(string Key, string Label);
public sealed record CompletenessGroup(string Key, string Label, bool Done, List<MissingItem> Missing);

/// <summary>Everything about one sale request, loaded once and projected for the owner or the team.</summary>
public sealed class SaleRequestFile
{
    public required SaleRequest Request { get; init; }
    public required List<SaleObligation> Obligations { get; init; }
    public required List<PrivateDocument> Documents { get; init; }
    public required List<ListingPhoto> Photos { get; init; }
    public required List<FigureVerification> Verifications { get; init; }
    public required List<ExternalApproval> Approvals { get; init; }
    public required List<CompletionRequest> CompletionRequests { get; init; }
    public required List<MarketEvent> Events { get; init; }
    public required List<Opportunity> Opportunities { get; init; }

    public static async Task<SaleRequestFile?> LoadAsync(RahoonDbContext db, string reference)
    {
        var r = await db.SaleRequests.FirstOrDefaultAsync(x => x.Reference == reference);
        if (r is null) return null;
        return new SaleRequestFile
        {
            Request = r,
            Obligations = await db.SaleObligations.Include(o => o.Party).Where(o => o.SaleRequestId == r.Id && o.RemovedAt == null).OrderBy(o => o.SortOrder).ToListAsync(),
            Documents = await db.PrivateDocuments.Where(d => d.SaleRequestId == r.Id && d.RemovedAt == null).OrderBy(d => d.CreatedAt).ToListAsync(),
            Photos = await db.ListingPhotos.Where(p => p.SaleRequestId == r.Id && p.RemovedAt == null).OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt).ToListAsync(),
            Verifications = await db.FigureVerifications.Where(v => v.SaleRequestId == r.Id && v.SupersededAt == null).OrderBy(v => v.VerifiedAt).ToListAsync(),
            Approvals = await db.ExternalApprovals.Where(a => a.SaleRequestId == r.Id).OrderBy(a => a.RecordedAt).ToListAsync(),
            CompletionRequests = await db.CompletionRequests.Where(c => c.SubjectId == r.Id).OrderBy(c => c.RequestedAt).ToListAsync(),
            Events = await db.MarketEvents.Where(e => e.SubjectId == r.Id).OrderBy(e => e.At).ToListAsync(),
            Opportunities = await db.Opportunities.Where(o => o.SaleRequestId == r.Id).OrderBy(o => o.CreatedAt).ToListAsync(),
        };
    }

    public CompletionRequest? OpenCompletion => CompletionRequests.LastOrDefault(c => c.AnsweredAt is null);
    public IEnumerable<string> ActiveKinds => Obligations.Select(o => o.Kind).Distinct();

    public static string ObligationKey(Guid obligationId, string key) => $"o:{obligationId}:{key}";

    public FigureVerification? Verified(string fieldKey) => Verifications.LastOrDefault(v => v.FieldKey == fieldKey);

    // ── Completeness: what the file still needs before the team can prepare and publish an opportunity ──

    public List<CompletenessGroup> Completeness()
    {
        var r = Request;
        var groups = new List<CompletenessGroup>();

        var prop = new List<MissingItem>();
        if (r.PropertyType is null) prop.Add(new("property_type", "نوع العقار"));
        if (r.City is null) prop.Add(new("city", "المدينة"));
        if (string.IsNullOrWhiteSpace(r.District)) prop.Add(new("district", "الحي"));
        foreach (var f in FieldCatalog.PropertyFields(r.PropertyType, r.Answers).Where(f => f.PublishKnown && !FieldCatalog.IsKnown(r.Answers, f.Key)))
            prop.Add(new(f.Key, f.Label));
        if (r.Latitude is null) prop.Add(new("location", "الموقع على الخريطة"));
        groups.Add(new("property", "تفاصيل العقار والموقع", prop.Count == 0, prop));

        var figs = new List<MissingItem>();
        if (Obligations.Count == 0) figs.Add(new("obligation", "جهة الالتزام"));
        var multi = Obligations.Count > 1;
        foreach (var o in Obligations)
        {
            var prefix = multi ? $"{o.PartyDisplayName}: " : "";
            if (o.PartyId is null && string.IsNullOrWhiteSpace(o.PartyOtherName)) figs.Add(new(ObligationKey(o.Id, "party"), prefix + (o.Kind == "developer" ? "اسم المطور" : "اسم جهة التمويل")));
            foreach (var f in FieldCatalog.ObligationFields(o.Kind, o.Answers).Where(f => f.PublishKnown && !FieldCatalog.IsKnown(o.Answers, f.Key) && Verified(ObligationKey(o.Id, f.Key)) is null))
                figs.Add(new(ObligationKey(o.Id, f.Key), prefix + f.Label));
        }
        groups.Add(new("figures", "الأرقام والالتزامات", figs.Count == 0, figs));

        var media = new List<MissingItem>();
        var usablePhotos = Photos.Where(p => p.ReviewStatus != FileReviewStatus.Rejected).ToList();
        if (usablePhotos.Count == 0) media.Add(new("photos", "صور العقار (صورة غلاف وصورة واحدة على الأقل)"));
        else if (!usablePhotos.Any(p => p.IsCover)) media.Add(new("cover", "اختيار صورة الغلاف"));
        foreach (var d in FieldCatalog.DocumentsFor(ActiveKinds).Where(d => d.PublishRequired))
            if (!Documents.Any(x => x.Kind == d.Key && x.ReviewStatus != FileReviewStatus.Rejected))
                media.Add(new("doc:" + d.Key, d.Label));
        groups.Add(new("media", "الصور والمستندات", media.Count == 0, media));

        var sent = r.Status is not SaleRequestStatus.Draft && OpenCompletion is null;
        groups.Add(new("review", "المراجعة والإرسال للفريق", sent && r.Status != SaleRequestStatus.NeedsCompletion,
            sent ? [] : [new("send", r.Status == SaleRequestStatus.Draft ? "إرسال الطلب" : "إرسال الملف للمراجعة بعد الاستكمال")]));
        return groups;
    }

    /// <summary>Labels for completion-request items (field, obligation field, document, location, photos).</summary>
    public string ItemLabel(string key)
    {
        if (key.StartsWith("doc:")) return FieldCatalog.Documents.FirstOrDefault(d => d.Key == key[4..])?.Label ?? key;
        if (key.StartsWith("o:"))
        {
            var parts = key.Split(':');
            var o = Obligations.FirstOrDefault(x => x.Id.ToString() == parts[1]);
            var label = parts[2] == "party" ? "اسم الجهة" : FieldCatalog.ByKey.TryGetValue(parts[2], out var f) ? f.Label : parts[2];
            return Obligations.Count > 1 && o is not null ? $"{o.PartyDisplayName}: {label}" : label;
        }
        return key switch
        {
            "location" => "الموقع على الخريطة",
            "photos" => "صور العقار",
            "cover" => "صورة الغلاف",
            "district" => "الحي",
            "other" => "أخرى (موضحة في الملاحظة)",
            _ => FieldCatalog.ByKey.TryGetValue(key, out var pf) ? pf.Label : key,
        };
    }

    // ── Preliminary figures (owner calculator on the file; the team prepares the real terms) ──

    private (decimal? Value, string? State) Fig(SaleObligation o, string key)
    {
        if (Verified(ObligationKey(o.Id, key)) is { } v && decimal.TryParse(v.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var vd))
            return (vd, FigureStates.Verified);
        var a = FieldCatalog.Num(o.Answers, key);
        return (a, a is null ? null : FigureStates.Declared);
    }

    private string? Choice(SaleObligation o, string key) =>
        Verified(ObligationKey(o.Id, key))?.Value ?? (o.Answers.TryGetValue(key, out var v) ? v : null);

    /// <summary>Calculator input built from the answers (and the team's verified figures, which take precedence).</summary>
    public TermsInput PreliminaryInput(decimal? sellerCosts = null, decimal? buyerCostsNow = null, decimal? buyerCostsLater = null, string arrearsPayer = "buyer")
    {
        var states = new Dictionary<string, string>();
        DeveloperTerms? dev = null;
        FinancierTerms? fin = null;
        if (Obligations.FirstOrDefault(o => o.Kind == "developer") is { } d)
        {
            var (p, ps) = Fig(d, "paid_approved");
            var (bal, bs) = Fig(d, "remaining_balance");
            var (arr, ars) = Fig(d, "arrears_amount");
            var (inst, ins) = Fig(d, "installment_amount");
            var target = FieldCatalog.Num(d.Answers, "owner_target");
            var reduction = p is { } pv && target is { } tv && tv < pv ? pv - tv : 0m;
            dev = new DeveloperTerms
            {
                PaidApproved = p, RemainingBalance = bal, ArrearsState = Choice(d, "arrears_state") ?? "unknown", Arrears = arr,
                ArrearsInBalance = Choice(d, "arrears_in_balance"), ArrearsPayer = arrearsPayer, Reduction = reduction,
                Installment = inst, InstallmentFrequency = Choice(d, "installment_frequency") is { } fq && fq != FieldCatalog.Unknown ? fq : null,
                RemainingInstallments = (int?)FieldCatalog.Num(d.Answers, "remaining_installments"),
                ExtraPayment = Choice(d, "extra_payments") == "has" ? FieldCatalog.Num(d.Answers, "extra_payment_amount") : null,
                ExtraPaymentRecurrence = d.Answers.GetValueOrDefault("extra_payment_recurrence"),
                ExtraPaymentDate = d.Answers.GetValueOrDefault("extra_payment_date") is { } ed && ed != FieldCatalog.Unknown ? ed : null,
            };
            if (ps is not null) states["paid_approved"] = ps;
            if (bs is not null) states["remaining_balance"] = bs;
            if (ars is not null) states["arrears"] = ars;
            if (ins is not null) states["installment_amount"] = ins;
        }
        if (Obligations.FirstOrDefault(o => o.Kind == "financier") is { } f)
        {
            var (price, prs) = Fig(f, "asking_price");
            var (payoff, pos) = Fig(f, "payoff_amount");
            var (arr, ars) = Fig(f, "arrears_amount");
            fin = new FinancierTerms
            {
                SalePrice = price, PayoffAmount = payoff, ArrearsState = Choice(f, "arrears_state") ?? "unknown", Arrears = arr,
                PayoffIncludesArrears = Choice(f, "payoff_includes_arrears"),
                PayoffValidUntil = f.Answers.GetValueOrDefault("payoff_valid_until") is { } pu && pu != FieldCatalog.Unknown ? pu : null,
            };
            if (prs is not null) states["sale_price"] = prs;
            if (pos is not null) states["payoff_amount"] = pos;
            if (ars is not null) states["arrears"] = ars;
        }
        return new TermsInput
        {
            Developer = dev, Financier = fin, SellerCosts = sellerCosts, BuyerCostsNow = buyerCostsNow, BuyerCostsLater = buyerCostsLater,
            NeedsNewFinancing = fin is not null, States = states,
        };
    }

    // ── Projections ──

    public object ObligationDto(SaleObligation o) => new
    {
        id = o.Id, kind = o.Kind, partyId = o.PartyId, partyOtherName = o.PartyOtherName, partyName = o.PartyDisplayName, relationNote = o.RelationNote,
        answers = o.Answers,
        verified = Verifications.Where(v => v.FieldKey.StartsWith($"o:{o.Id}:")).Select(v => new { key = v.FieldKey.Split(':')[2], v.Value, v.Source, v.SourceDate }),
    };

    public object DocumentDto(PrivateDocument d) => new
    {
        id = d.Id, kind = d.Kind, kindLabel = FieldCatalog.Documents.FirstOrDefault(x => x.Key == d.Kind)?.Label ?? d.Kind, d.ObligationId, d.FileName, d.SizeBytes,
        d.ContentType, reviewStatus = d.ReviewStatus, d.ReviewNote, uploadedAt = d.CreatedAt, d.Source,
        url = $"/api/market/sale-requests/{Request.Reference}/documents/{d.Id}/file",
    };

    public object PhotoDto(ListingPhoto p) => new
    {
        id = p.Id, url = $"/api/market/sale-requests/{Request.Reference}/photos/{p.Id}/file", p.IsCover, p.SortOrder, reviewStatus = p.ReviewStatus, p.ReviewNote,
    };

    public object CompletionDto(CompletionRequest c) => new
    {
        id = c.Id, items = c.Items.Select(k => new { key = k, label = ItemLabel(k) }), c.Note, c.RequestedAt, c.RequestedByLabel, c.AnsweredAt,
    };

    public static object EventDto(MarketEvent e) => new
    {
        id = e.Id, e.Kind, e.Title, e.Body, e.Reason, e.At, actor = e.ActorKind, actorLabel = e.ActorLabel, e.FromStatus, e.ToStatus, visible = e.VisibleToApplicant,
    };
}
