using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Sale;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Seed;

/// <summary>
/// B8 fictional data. RH-2026-004012 (عائشة ف.) is the spec's sample at L27: in negotiation, with the owner's
/// portal request of 2026-09-20 and a valid valuation of 2,850,000 — the decision is ready to be submitted.
/// RH-2026-003944 (لطيفة ش.) carries the same figures further along (consent signed, broker «أ» assigned,
/// OF-01..OF-03 shared with the owner) so the L28–L32 screens have data.
/// </summary>
public sealed partial class DevSeeder
{
    private async Task SeedVoluntarySaleAsync()
    {
        var org = _orgs["alufuq"];

        // ── RH-2026-004012 — owner request, decision pending submission (L27) ──
        var c1 = _cases["RH-2026-004012"];
        var p1 = await db.Properties.FirstAsync(p => p.CaseId == c1.Id);
        p1.LandAreaM2 = 600; p1.BuiltAreaM2 = 680; p1.YearBuilt = 2017;
        p1.OccupancyNote = "تسكن المالكة وأبناؤها العقار.";
        db.ValuationReports.Add(new ValuationReport
        {
            OrganizationId = org.Id, CaseId = c1.Id, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = 2_850_000m, RangeLow = 2_760_000m, RangeHigh = 2_920_000m,
            Methodology = "أسلوب المقارنة بالمبيعات", ComparablesCount = 5, InspectionDate = D("2026-09-02"), ReportDate = D("2026-09-05"), ValidUntil = D("2026-12-04"),
            Status = ValuationStatus.Accepted, ReviewedByUserId = UserId("fahad"), ReviewedAt = At("2026-09-06T10:00:00"),
        });
        var party1 = await db.Parties.FirstAsync(p => p.CaseId == c1.Id && p.IsPrimary);
        var request1 = OwnerConsent(c1, party1, "sale_consent", At("2026-09-20T19:12:00"), [.. SaleTexts.RequestAcks, "explicit_request"], "RH-2026-004012|sale_request");
        db.Set<VoluntarySale>().Add(new VoluntarySale
        {
            OrganizationId = org.Id, CaseId = c1.Id, BuyerReference = "VS-2026-0031", Status = SaleStatus.Requested,
            RequestText = "بعد التفكير، أفضّل بيع الفيلا بنفسي بسعر السوق وسداد التمويل، والانتقال لمسكن أصغر.", RequestedAt = At("2026-09-20T19:12:00"),
            RequestConsentRecordId = request1.Id, ProposedMinPrice = 2_700_000m, ValuationAmount = 2_850_000m, ValuationDate = D("2026-09-05"),
            ValuerName = "مكتب تقييم معتمد «ب»", OutstandingDebt = 2_240_000m, DebtAsOf = At("2026-09-22T18:40:00"), AreaLabel = "شمال الرياض",
            OccupancyNote = "تسكن المالكة وأبناؤها العقار. اتُفق على مهلة إخلاء 60 يوماً بعد نقل الملكية، مذكورة في شروط العرض للمشترين.",
            CreatedAt = At("2026-09-20T19:12:00"),
        });
        db.Tasks.Add(new Modules.Communications.CaseTask
        {
            OrganizationId = org.Id, CaseId = c1.Id, Title = "مراجعة طلب المالكة للبيع الطوعي وإرسال القرار للاعتماد", AssigneeUserId = UserId("sara"),
            DueOn = D("2026-09-24"), Kind = "sale_request", CreatedByUserId = UserId("sara"), Link = "/cases/RH-2026-004012/sale/decision",
        });
        Audit(org.Id, c1, "owner.sale_requested", "طلب المالك فتح مسار البيع الطوعي", At("2026-09-20T19:12:00"), "owner",
            detail: "عبر البوابة · رمز تحقق · سجل موافقة وليس توقيعاً مرخّصاً", reason: "بعد التفكير، أفضّل بيع الفيلا بنفسي بسعر السوق وسداد التمويل، والانتقال لمسكن أصغر.");

        // ── RH-2026-003944 — sale track at the offers stage (L28–L32) ──
        var c2 = await BuildCaseAsync(new CaseSpec("RH-2026-003944", "alufuq", "لطيفة عبدالله الشهري", "1062223944", "0556603944", "الرياض", "حي الياسمين", "فيلا سكنية",
            CaseStatus.VoluntarySale, "sara", 2_240_000.00m, 2_050_000.00m, 160_000.00m, 30_000.00m, 185_000.00m, 10, "2025-11-01", "2026-05-18", "MF-64-3944021", 18_500.00m,
            "2026-10-12", "2026-08-27T11:00:00", ShortLabel: "فيلا سكنية، حي الياسمين، الرياض"));
        _cases[c2.Reference] = c2;
        await db.SaveChangesAsync();
        var p2 = await db.Properties.FirstAsync(p => p.CaseId == c2.Id);
        p2.LandAreaM2 = 600; p2.BuiltAreaM2 = 680; p2.YearBuilt = 2017; p2.OccupancyNote = "تسكن المالكة وأبناؤها العقار.";
        var valuation2 = new ValuationReport
        {
            OrganizationId = org.Id, CaseId = c2.Id, ValuerName = "مكتب تقييم معتمد «ب»", MarketValue = 2_850_000m, ReportDate = D("2026-08-20"), ValidUntil = D("2026-11-18"),
            Status = ValuationStatus.Accepted, ReviewedByUserId = UserId("fahad"), ReviewedAt = At("2026-08-21T10:00:00"),
        };
        db.ValuationReports.Add(valuation2);
        var party2 = await db.Parties.FirstAsync(p => p.CaseId == c2.Id && p.IsPrimary);
        var request2 = OwnerConsent(c2, party2, "sale_consent", At("2026-08-24T20:10:00"), [.. SaleTexts.RequestAcks, "explicit_request"], "RH-2026-003944|sale_request");
        var scope2 = OwnerConsent(c2, party2, "sale_scope_consent", At("2026-08-28T20:04:00"), [SaleTexts.ConsentAck], "RH-2026-003944|SALE-CONSENT-v1|min 2700000");
        var sale2 = new VoluntarySale
        {
            OrganizationId = org.Id, CaseId = c2.Id, BuyerReference = "VS-2026-0027", Status = SaleStatus.Active,
            RequestText = "أرغب في بيع العقار بسعر السوق وسداد التمويل.", RequestedAt = At("2026-08-24T20:10:00"), RequestConsentRecordId = request2.Id,
            ProposedMinPrice = 2_700_000m, DecisionReason = "بطلب المالكة؛ صافي متوقع يغطي المديونية مع فائض لها.", DecisionPreparedByUserId = UserId("sara"),
            DecisionSubmittedAt = At("2026-08-25T10:00:00"), DecisionApprovedByUserId = UserId("noura"), OpenedAt = At("2026-08-27T11:00:00"),
            ValuationReportId = valuation2.Id, ValuationAmount = 2_850_000m, ValuationDate = D("2026-08-20"), ValuerName = "مكتب تقييم معتمد «ب»",
            OutstandingDebt = 2_240_000m, DebtAsOf = At("2026-09-22T18:40:00"), ListingStatus = ListingStatus.ComplianceReviewed, AreaLabel = "شمال الرياض",
            AskingPrice = 2_850_000m, EvacuationDays = 60, VisitTerms = "بموعد", ApprovedPhotoCount = 6, ListingPreparedByUserId = UserId("sara"),
            ListingReviewedByUserId = UserId("hind"), ListingReviewedAt = At("2026-09-01T12:00:00"),
            OccupancyNote = "تسكن المالكة وأبناؤها العقار. اتُفق على مهلة إخلاء 60 يوماً بعد نقل الملكية، مذكورة في شروط العرض للمشترين.",
            CreatedAt = At("2026-08-24T20:10:00"),
        };
        db.Set<VoluntarySale>().Add(sale2);
        var decision2 = new ApprovalRequest
        {
            OrganizationId = org.Id, CaseId = c2.Id, Subject = ApprovalSubject.Sale, SubjectId = sale2.Id, SubjectVersionNo = 1, Title = "اعتماد فتح مسار البيع الطوعي",
            PreparedByUserId = UserId("sara"), SubmittedByUserId = UserId("sara"), SubmittedAt = At("2026-08-25T10:00:00"), SubmitterNote = sale2.DecisionReason!,
            SubmitterAttested = true, AssignedApproverUserId = UserId("noura"), Amount = 2_850_000m, DueOn = D("2026-08-28"), Status = ApprovalStatus.Approved,
            DecidedByUserId = UserId("noura"), DecidedAt = At("2026-08-27T11:00:00"), DecisionReason = "الشروط مكتملة والفائض المتوقع لصالح المالكة.", StepUpVerified = true,
        };
        db.ApprovalRequests.Add(decision2);
        sale2.DecisionApprovalRequestId = decision2.Id;
        var consent2 = new SaleConsent
        {
            OrganizationId = org.Id, SaleId = sale2.Id, CaseId = c2.Id, MinPrice = 2_700_000m, MandateStart = D("2026-08-28"), MandateEnd = D("2026-11-26"),
            VisitDays = ["thu", "sat"], VisitWindow = "4–7 م", ConsentRecordId = scope2.Id, SignedAt = At("2026-08-28T20:04:00"), TextHash = scope2.TextHash,
        };
        db.Set<SaleConsent>().Add(consent2);
        (string key, string title, PrepItemStatus st, string? memo, string resp)[] prep =
        [
            ("signed_consent", "موافقة المالكة الموقعة", PrepItemStatus.Done, "2026-08-28", "owner"),
            ("deed_check", "صك الملكية ومطابقة البيانات", PrepItemStatus.Done, "متحقق", "legal"),
            ("debt_letter", "خطاب المديونية للمشتري", PrepItemStatus.Done, "يُصدر عند قبول عرض", "finance"),
            ("visit_times", "تحديد أوقات الزيارة", PrepItemStatus.Done, "الخميس والسبت 4–7 م", "owner"),
            ("evacuation_plan", "خطة الإخلاء", PrepItemStatus.Done, "60 يوماً بعد نقل الملكية", "owner_case_manager"),
            ("photography", "التصوير المعتمد", PrepItemStatus.Done, "6 صور بلا أشخاص", "broker"),
            ("inspection", "فحص فني اختياري", PrepItemStatus.Scheduled, "بطلب المشتري لاحقاً", "none"),
            ("listing_review", "مراجعة ملخص العرض", PrepItemStatus.Done, "روجع 2026-09-01", "compliance"),
        ];
        var sort = 0;
        foreach (var it in prep)
            db.Set<SalePrepItem>().Add(new SalePrepItem
            {
                OrganizationId = org.Id, SaleId = sale2.Id, CaseId = c2.Id, Key = it.key, Title = it.title, Status = it.st, Memo = it.memo, Responsible = it.resp,
                SortOrder = sort++, ScheduledOn = it.st == PrepItemStatus.Scheduled ? D("2026-09-30") : null,
            });
        var broker = new ProviderAssignment
        {
            OrganizationId = org.Id, Reference = "ASG-2026-0871", CaseId = c2.Id, ProviderOrganizationId = _orgs["broker-a"].Id, AssigneeUserId = UserId("sami"),
            Type = AssignmentType.Brokerage, Title = "وساطة بيع طوعي VS-2026-0027", PropertyLabel = "فيلا سكنية · شمال الرياض", Status = AssignmentStatus.InProgress,
            DueOn = D("2026-11-26"), Scope = ["sale_file"], FeesLabel = "عمولة 2% من سعر البيع", CreatedByUserId = UserId("sara"),
            AccessExpiresAt = At("2026-11-26T23:59:00"), CreatedAt = At("2026-09-01T13:00:00"),
        };
        db.Assignments.Add(broker);
        sale2.BrokerAssignmentId = broker.Id;
        sale2.BrokerOrganizationId = broker.ProviderOrganizationId;
        (string code, decimal price, BuyerPaymentMethod m, string proof, string? cond, int days, string until, OfferCertainty cert)[] offers =
        [
            ("OF-01", 2_780_000m, BuyerPaymentMethod.FinancingPreapproved, "خطاب موافقة مبدئية", "فحص فني", 30, "2026-10-12", OfferCertainty.Medium),
            ("OF-02", 2_800_000m, BuyerPaymentMethod.Cash, "شيك مصدق (نسخة)", null, 21, "2026-10-15", OfferCertainty.High),
            ("OF-03", 2_820_000m, BuyerPaymentMethod.FinancingPending, "—", "الحصول على التمويل خلال 30 يوماً", 45, "2026-10-08", OfferCertainty.Low),
        ];
        var n = 0;
        foreach (var o in offers)
            db.Set<BuyerOffer>().Add(new BuyerOffer
            {
                OrganizationId = org.Id, SaleId = sale2.Id, CaseId = c2.Id, Code = o.code, Price = o.price, PaymentMethod = o.m, ProofOfFunds = o.proof, Conditions = o.cond,
                ProposedTransferDays = o.days, ValidUntil = D(o.until), Certainty = o.cert, BuyerNdaConfirmed = true, EnteredBySide = "broker", EnteredByUserId = UserId("sami"),
                ReceivedAt = At("2026-09-10T12:00:00").AddDays(4 * n++), Status = BuyerOfferStatus.SharedWithOwner, SharedWithOwnerAt = At("2026-09-20T10:00:00"),
            });
        Audit(org.Id, c2, "owner.sale_requested", "طلب المالك فتح مسار البيع الطوعي", At("2026-08-24T20:10:00"), "owner", detail: "عبر البوابة · رمز تحقق");
        Audit(org.Id, c2, "case.transition", "تفاوض ← بيع طوعي", At("2026-08-27T11:00:00"), "noura", from: "negotiation", to: "voluntary_sale",
            detail: "بدء مسار البيع الطوعي", reason: "الشروط مكتملة والفائض المتوقع لصالح المالكة.");
        Audit(org.Id, c2, "owner.sale_consent_signed", "موافقة المالك الصريحة بنطاق البيع", At("2026-08-28T20:04:00"), "owner",
            detail: "الحد الأدنى 2,700,000.00 · التفويض 90 يوماً حتى 2026-11-26 · رمز تحقق");
        Audit(org.Id, c2, "sale.broker_assigned", "تكليف دار الوسطاء «أ» بوساطة البيع", At("2026-09-01T13:00:00"), "sara", detail: "ASG-2026-0871 · نطاق: ملف البيع فقط");
        Audit(org.Id, c2, "sale.offers_shared", "إرسال مقارنة العروض للمالكة", At("2026-09-20T10:00:00"), "sara", detail: "OF-01 · OF-02 · OF-03 · بلا أسماء المشترين");
        await db.SaveChangesAsync();
        // Keep the buyer-reference counter clear of the seeded VS numbers.
        await db.Database.ExecuteSqlRawAsync("INSERT INTO cases.reference_counters (key, value) VALUES ('sale:2026', 100) ON CONFLICT (key) DO NOTHING");
    }

    private ConsentRecord OwnerConsent(Case c, CaseParty party, string kind, DateTimeOffset at, List<string> acks, string text)
    {
        var record = new ConsentRecord
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, Kind = kind, AcceptedAt = at, Channel = "portal", OtpDestinationMasked = party.PhoneMasked,
            OtpVerifiedAt = at, Device = "iPhone", Acknowledgements = acks, TextHash = "sha256:" + Infrastructure.Security.Tokens.Sha256Hex(text),
        };
        db.ConsentRecords.Add(record);
        return record;
    }
}
