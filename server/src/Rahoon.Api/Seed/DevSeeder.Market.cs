using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Seed;

/// <summary>
/// Demo data of the exit/buy platform. Test data only: every person, party, project, amount and photo is invented, the
/// requests and opportunities carry IsDemo (shown «تجريبي»), the parties' names end with «(تجريبي)», and the photos are
/// drawn illustrations. Runs once per database (skipped when any sale request exists), also on databases seeded before.
/// </summary>
public sealed partial class DevSeeder
{
    private sealed record DemoPerson(Guid Id, string Name);

    private async Task SeedMarketAsync()
    {
        if (await db.SaleRequests.AnyAsync()) { log.LogInformation("Market seed skipped: data already present."); return; }
        var org = await db.Organizations.Where(o => o.Kind == OrganizationKind.Operator).Select(o => o.Id).FirstOrDefaultAsync();
        if (org == Guid.Empty) { log.LogWarning("Market seed skipped: no operator organization."); return; }
        var team = await db.Users.Where(u => u.Email.EndsWith("@team.rahoon.example")).ToDictionaryAsync(u => u.Email, u => u);
        User? Member(string email) => team.GetValueOrDefault(email);
        var lama = Member("l.alharbi@team.rahoon.example");
        var nayef = Member("n.alyami@team.rahoon.example");
        var abeer = Member("a.alqahtani@team.rahoon.example");
        var policy = CommissionPolicy.From(config);
        var t0 = DemoToday;

        // ── Directory (fictional) ──
        string[] developers = ["شركة المسار للتطوير العقاري (تجريبي)", "دار الجنوب للتطوير (تجريبي)", "مطور الواحة السكني (تجريبي)", "شركة أفق الشرقية العقارية (تجريبي)"];
        string[] financiers = ["مصرف الأفق (تجريبي)", "بنك الريادة (تجريبي)", "شركة المسكن للتمويل العقاري (تجريبي)", "مصرف الواحة (تجريبي)"];
        var parties = new Dictionary<string, ObligationParty>();
        for (var i = 0; i < developers.Length; i++) parties[developers[i]] = new ObligationParty { Kind = "developer", NameAr = developers[i], SortOrder = i, IsDemo = true };
        for (var i = 0; i < financiers.Length; i++) parties[financiers[i]] = new ObligationParty { Kind = "financier", NameAr = financiers[i], SortOrder = i, IsDemo = true };
        db.ObligationParties.AddRange(parties.Values);
        await db.SaveChangesAsync();

        // ── People (fictional; mobiles 0561110xxx) ──
        DemoPerson Person(string name, string phone, int daysAgo)
        {
            var id = Guid.CreateVersion7();
            var since = t0.AddDays(-daysAgo);
            db.Users.Add(new User
            {
                Id = id, Email = $"individual+{id:N}@individuals.rahoon.local", FullName = name, AccountKind = AccountKind.Individual, Status = UserStatus.Active,
                MfaEnrolled = true, CreatedAt = since, UpdatedAt = since,
            });
            db.IndividualProfiles.Add(new IndividualProfile
            {
                UserId = id, PhoneEnc = pii.Protect(phone), PhoneHash = pii.LookupHash(phone), PhoneMasked = Mask.Phone(phone), PhoneVerifiedAt = since,
                TermsVersion = PhoneAuthEndpoints.TermsVersion, TermsAcceptedAt = since, CreatedAt = since, UpdatedAt = since,
            });
            return new DemoPerson(id, name);
        }

        var s1 = Person("خالد عبدالله المطيري", "0561110001", 3);
        var s2 = Person("نورة سعد القحطاني", "0561110002", 9);
        var s3 = Person("فهد ناصر العتيبي", "0561110003", 14);
        var s4 = Person("سارة محمد الشهري", "0561110004", 30);
        var s5 = Person("عبدالرحمن علي الدوسري", "0561110005", 26);
        var s6 = Person("ريم خالد الزهراني", "0561110006", 20);
        var s7 = Person("ماجد فيصل الحربي", "0561110007", 18);
        var s8 = Person("هيفاء سعود العنزي", "0561110008", 24);
        var b1 = Person("سلطان عمر القرني", "0561110011", 2);
        var b2 = Person("لينا أحمد الغامدي", "0561110012", 12);
        await db.SaveChangesAsync();

        var no = 0;
        SaleRequest Sale(DemoPerson who, SaleRequestStatus status, string type, string city, string district, string? project, string mode,
            Dictionary<string, string> answers, int daysAgo, double? lat = null, double? lng = null, string wish = "approximate")
        {
            no++;
            var at = t0.AddDays(-daysAgo);
            var r = new SaleRequest
            {
                OrganizationId = org, ApplicantUserId = who.Id, Reference = $"SR-2026-{no:D5}", ClientDraftId = Guid.CreateVersion7(), Status = status, StatusChangedAt = at.AddDays(1),
                SubmittedAt = at, PropertyType = type, City = city, District = district, Project = project, ObligationMode = mode, Answers = answers,
                Latitude = lat, Longitude = lng, LocationDisplayWish = lat is null ? null : wish, ContactName = who.Name, RelationshipDeclared = "owner",
                DeclarationsAcceptedAt = at, DeclarationsVersion = SaleRequestEndpoints.DeclarationsVersion, IsDemo = true, CreatedAt = at, UpdatedAt = at.AddDays(1),
            };
            db.SaleRequests.Add(r);
            Ev(r.OrganizationId, "sale_request", r.Id, who.Id, "submitted", "استلمنا طلبك", true, at, "applicant", who.Name, to: nameof(SaleRequestStatus.Submitted));
            return r;
        }

        SaleObligation Obl(SaleRequest r, string kind, string party, Dictionary<string, string> answers)
        {
            var o = new SaleObligation
            {
                OrganizationId = org, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, PartyId = parties[party].Id, Answers = answers,
                SortOrder = r.Obligations.Count,
            };
            r.Obligations.Add(o);
            return o;
        }

        void Ev(Guid orgId, string type, Guid subject, Guid applicant, string kind, string title, bool visible, DateTimeOffset at, string actor = "team", string? label = null,
            string? body = null, string? reason = null, string? from = null, string? to = null)
        {
            db.MarketEvents.Add(new MarketEvent
            {
                OrganizationId = orgId, SubjectType = type, SubjectId = subject, ApplicantUserId = applicant, Kind = kind, Title = title, Body = body, Reason = reason,
                FromStatus = from, ToStatus = to, ActorKind = actor, ActorLabel = label ?? (actor == "team" ? "فريق رهون" : "النظام"), VisibleToApplicant = visible, At = at,
                ReadAt = visible ? at.AddHours(2) : null,
            });
        }

        async Task<List<ListingPhoto>> Photos(SaleRequest r, int count, int variant, FileReviewStatus review, int shade = 0)
        {
            var list = new List<ListingPhoto>();
            for (var i = 0; i < count; i++)
            {
                var bytes = DemoImages.Building(variant, i + shade);
                using var ms = new MemoryStream(bytes);
                var stored = await storage.SaveAsync(ms, $"demo-{r.Reference}-{i + 1}.png", org, area: "market-photos", allowedTypes: ["image/png"]);
                var p = new ListingPhoto
                {
                    OrganizationId = org, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, FileName = $"صورة تجريبية {i + 1}.png", ContentType = stored.ContentType,
                    SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey, SortOrder = i, IsCover = i == 0, ReviewStatus = review,
                    UploadedByUserId = r.ApplicantUserId,
                };
                db.ListingPhotos.Add(p);
                list.Add(p);
            }
            return list;
        }

        async Task<PrivateDocument> Doc(SaleRequest r, string kind, FileReviewStatus review, Guid? obligationId = null)
        {
            using var ms = new MemoryStream(DemoImages.Pdf(kind));
            var stored = await storage.SaveAsync(ms, kind + ".pdf", org, area: "market-docs");
            var d = new PrivateDocument
            {
                OrganizationId = org, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, ObligationId = obligationId, Kind = kind,
                FileName = $"{FieldCatalog.Documents.First(x => x.Key == kind).Label} (تجريبي).pdf", ContentType = stored.ContentType, SizeBytes = stored.SizeBytes,
                Sha256 = stored.Sha256, StorageKey = stored.StorageKey, UploadedByUserId = r.ApplicantUserId, ReviewStatus = review,
                ReviewedAt = review == FileReviewStatus.Pending ? null : t0, ReviewedByUserId = review == FileReviewStatus.Pending ? null : nayef?.Id,
            };
            db.PrivateDocuments.Add(d);
            return d;
        }

        void Verify(SaleRequest r, SaleObligation o, string key, string value, string source, PrivateDocument? doc, int daysAgo)
        {
            db.FigureVerifications.Add(new FigureVerification
            {
                OrganizationId = org, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, FieldKey = SaleRequestFile.ObligationKey(o.Id, key), Value = value, Source = source,
                SourceDocumentId = doc?.Id, SourceDate = DateOnly.FromDateTime(t0.AddDays(-daysAgo - 3).UtcDateTime), VerifiedByUserId = nayef?.Id ?? Guid.Empty,
                VerifiedByLabel = nayef?.FullName ?? "فريق رهون", VerifiedAt = t0.AddDays(-daysAgo),
            });
        }

        var oppNo = 0;
        Opportunity Opp(SaleRequest r, OpportunityStatus status, string title, string description, string track, List<ListingPhoto> photos, string precision, int daysAgo)
        {
            oppNo++;
            var (plat, plng) = OpportunityProjection.PublicPoint(r.Latitude, r.Longitude, precision);
            var specKeys = FieldCatalog.PropertyFields(r.PropertyType, r.Answers)
                .Where(x => x.Key is not ("area" or "bedrooms" or "bathrooms" or "readiness" or "delivery_month" or "description" or "features") && r.Answers.ContainsKey(x.Key)).Select(x => x.Key);
            var o = new Opportunity
            {
                OrganizationId = org, SaleRequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Reference = $"OP-2026-{oppNo:D5}", Status = status,
                StatusChangedAt = t0.AddDays(-daysAgo), Title = title, Description = description, PropertyType = r.PropertyType!, City = r.City!, District = r.District,
                Project = r.Project, Track = track, Area = FieldCatalog.Num(r.Answers, "area"), Bedrooms = (int?)FieldCatalog.Num(r.Answers, "bedrooms"),
                Bathrooms = (int?)FieldCatalog.Num(r.Answers, "bathrooms"), Readiness = r.PropertyType == "land" ? null : r.Answers.GetValueOrDefault("readiness"),
                DeliveryMonth = r.Answers.GetValueOrDefault("delivery_month"), Specs = specKeys.ToDictionary(k => k, k => r.Answers[k]),
                Features = (r.Answers.GetValueOrDefault("features") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ExactLatitude = r.Latitude, ExactLongitude = r.Longitude, LocationPrecision = precision, PublicLatitude = plat, PublicLongitude = plng,
                PhotoIds = photos.Select(p => p.Id).ToList(), PreparedByUserId = nayef?.Id ?? Guid.Empty, PreparedByLabel = nayef?.FullName ?? "فريق رهون",
                AssignedToUserId = nayef?.Id, AssignedToLabel = nayef?.FullName, IsDemo = true, CreatedAt = t0.AddDays(-daysAgo - 2), UpdatedAt = t0.AddDays(-daysAgo),
            };
            db.Opportunities.Add(o);
            return o;
        }

        OpportunityTerms Terms(Opportunity o, int version, TermsStatus status, TermsInput input, string? transfer, string? scope, int daysAgo)
        {
            var result = MarketCalculator.Compute(input, policy);
            var t = new OpportunityTerms
            {
                OrganizationId = org, OpportunityId = o.Id, ApplicantUserId = o.ApplicantUserId, VersionNo = version, Status = status, Track = o.Track,
                InputJson = JsonSerializer.Serialize(input, JsonOptions.Web), ResultJson = JsonSerializer.Serialize(result, JsonOptions.Web),
                DueNow = result.DueNow, PurchaseTotal = result.BuyerTotal, FutureBalance = result.FutureBalance, Installment = result.Installment,
                InstallmentFrequency = result.InstallmentFrequency, InstallmentMonthlyEquivalent = result.InstallmentMonthlyEquivalent,
                LargestExtraPayment = result.LargestExtraPayment, RemainingMonths = result.RemainingMonths, NeedsNewFinancing = result.NeedsNewFinancing,
                Complete = result.Complete, TransferConditions = transfer, VerificationScope = scope, VerifiedOn = DateOnly.FromDateTime(t0.AddDays(-daysAgo - 1).UtcDateTime),
                PreparedByUserId = nayef?.Id ?? Guid.Empty, PreparedByLabel = nayef?.FullName ?? "فريق رهون",
                SentToOwnerAt = status is TermsStatus.Draft ? null : t0.AddDays(-daysAgo - 1),
                OwnerDecidedAt = status is TermsStatus.OwnerConfirmed ? t0.AddDays(-daysAgo) : null,
                OwnerConfirmationText = status is TermsStatus.OwnerConfirmed ? $"أكد صاحب العقار ملخص الفرصة {o.Reference} (الإصدار {version})." : null,
            };
            db.OpportunityTerms.Add(t);
            return t;
        }

        Dictionary<string, string> A(params (string K, string V)[] kv) => kv.ToDictionary(x => x.K, x => x.V);

        // 1 · Submitted, not assigned — developer, apartment under construction (Riyadh).
        var r1 = Sale(s1, SaleRequestStatus.Submitted, "apartment", "riyadh", "الياسمين", "مشروع سما الياسمين (تجريبي)", "developer",
            A(("area", "142"), ("bedrooms", "3"), ("bathrooms", "3"), ("readiness", "under_construction"), ("delivery_month", "2027-06")), 2);
        Obl(r1, "developer", developers[0], A(("original_price", "900000"), ("paid_approved", "280000"), ("remaining_balance", "620000"), ("installment_amount", "15500"),
            ("installment_frequency", "quarterly"), ("extra_payments", "has"), ("extra_payment_amount", "30000"), ("extra_payment_recurrence", "annual"),
            ("arrears_state", "none"), ("owner_target", "280000")));

        // 2 · Under review (نايف) — bank-financed villa (Jeddah), no payoff letter yet.
        var r2 = Sale(s2, SaleRequestStatus.UnderReview, "villa", "jeddah", "أبحر الشمالية", null, "financier",
            A(("area", "420"), ("land_area", "450"), ("bedrooms", "5"), ("bathrooms", "6"), ("readiness", "ready"), ("features", "maid_room,driver_room,annex")), 8, 21.7372, 39.1098);
        r2.AssignedToUserId = nayef?.Id; r2.AssignedToLabel = nayef?.FullName; r2.AssignedAt = t0.AddDays(-7);
        Obl(r2, "financier", financiers[0], A(("purchase_price", "1850000"), ("current_installment", "9800"), ("installment_frequency", "monthly"), ("remaining_months", "180"),
            ("arrears_state", "has"), ("arrears_amount", "29400"), ("arrears_count", "3"), ("payoff_amount", FieldCatalog.Unknown), ("payoff_includes_arrears", "unknown"),
            ("asking_price", "2100000")));
        await Doc(r2, "financing_contract", FileReviewStatus.Pending);
        await Photos(r2, 2, 1, FileReviewStatus.Pending);
        Ev(org, "sale_request", r2.Id, s2.Id, "status", "بدأ فريق رهون مراجعة طلبك", true, t0.AddDays(-7), label: nayef?.FullName, from: "Submitted", to: "UnderReview");

        // 3 · Needs completion — developer townhouse (Khobar): statement, location and photos requested.
        var r3 = Sale(s3, SaleRequestStatus.NeedsCompletion, "townhouse", "khobar", "الخزامى", null, "developer",
            A(("area", "260"), ("bedrooms", "4"), ("readiness", "ready")), 13);
        r3.AssignedToUserId = nayef?.Id; r3.AssignedToLabel = nayef?.FullName;
        var o3 = Obl(r3, "developer", developers[1], A(("paid_approved", "350000"), ("remaining_balance", FieldCatalog.Unknown), ("installment_amount", "8000"),
            ("installment_frequency", "monthly"), ("extra_payments", "none"), ("arrears_state", "unknown")));
        await Doc(r3, "developer_contract", FileReviewStatus.Accepted);
        db.CompletionRequests.Add(new CompletionRequest
        {
            OrganizationId = org, SubjectType = "sale_request", SubjectId = r3.Id, ApplicantUserId = s3.Id,
            Items = ["doc:payment_proof", SaleRequestFile.ObligationKey(o3.Id, "remaining_balance"), SaleRequestFile.ObligationKey(o3.Id, "arrears_state"), "location", "photos"],
            Note = "نحتاج كشف المطور الحديث لنعرف الرصيد المتبقي وهل توجد متأخرات، مع تحديد الموقع على الخريطة وصور للعقار.",
            RequestedByUserId = nayef?.Id ?? Guid.Empty, RequestedByLabel = nayef?.FullName ?? "فريق رهون", RequestedAt = t0.AddDays(-10),
        });
        Ev(org, "sale_request", r3.Id, s3.Id, "completion_requested", "يحتاج طلبك استكمالًا", true, t0.AddDays(-10), label: nayef?.FullName,
            body: "نحتاج كشف المطور الحديث لنعرف الرصيد المتبقي وهل توجد متأخرات، مع تحديد الموقع على الخريطة وصور للعقار.", from: "UnderReview", to: "NeedsCompletion");

        // 4 · Published — developer apartment (Riyadh, النرجس), the brief's worked example 1 as the published figures.
        var r4 = Sale(s4, SaleRequestStatus.ApprovedForListing, "apartment", "riyadh", "النرجس", "أبراج النرجس (تجريبي)", "developer",
            A(("area", "168"), ("bedrooms", "3"), ("bathrooms", "4"), ("floor", "2"), ("elevator", "true"), ("parking", "one"), ("finishing", "full"), ("readiness", "ready"),
              ("description", "شقة بتشطيب كامل قرب طريق الملك سلمان، مطبخ مجهز وغرفة خادمة، والمبنى بمصعد وموقف خاص. (بيانات تجريبية)"),
              ("features", "maid_room,kitchen,central_ac")), 29, 24.8352, 46.6556, "approximate");
        var o4 = Obl(r4, "developer", developers[2], A(("original_price", "1000000"), ("paid_approved", "300000"), ("remaining_balance", "700000"), ("installment_amount", "12000"),
            ("installment_frequency", "monthly"), ("remaining_installments", "57"), ("extra_payments", "none"), ("arrears_state", "has"), ("arrears_amount", "20000"),
            ("arrears_count", "2"), ("arrears_in_balance", "yes"), ("owner_target", "290000"), ("transfer_allowed", "yes")));
        var c4 = await Doc(r4, "developer_contract", FileReviewStatus.Accepted, o4.Id);
        var st4 = await Doc(r4, "payment_proof", FileReviewStatus.Accepted, o4.Id);
        await Doc(r4, "payment_schedule", FileReviewStatus.Accepted, o4.Id);
        Verify(r4, o4, "paid_approved", "300000", "developer_statement", st4, 22);
        Verify(r4, o4, "remaining_balance", "700000", "developer_statement", st4, 22);
        Verify(r4, o4, "arrears_amount", "20000", "developer_statement", st4, 22);
        var p4 = await Photos(r4, 4, 0, FileReviewStatus.Accepted);
        db.ExternalApprovals.Add(new ExternalApproval
        {
            OrganizationId = org, SaleRequestId = r4.Id, ApplicantUserId = s4.Id, ObligationId = o4.Id, Status = ExternalApprovalStatus.Requested,
            Note = "أُرسل طلب موافقة النقل للمطور (تجريبي).", RecordedByUserId = nayef?.Id ?? Guid.Empty, RecordedByLabel = nayef?.FullName ?? "فريق رهون", RecordedAt = t0.AddDays(-20),
        });
        Ev(org, "sale_request", r4.Id, s4.Id, "approved", "اعتمد الفريق طلبك لإعداد فرصة", true, t0.AddDays(-21), label: nayef?.FullName, from: "UnderReview", to: "ApprovedForListing");
        var op4 = Opp(r4, OpportunityStatus.Published, "شقة 3 غرف جاهزة في حي النرجس، الرياض",
            "شقة بتشطيب كامل في الدور الثاني، مطبخ مجهز وغرفة خادمة وتكييف مركزي، والمبنى بمصعد وموقف خاص. الخروج دون زيادة فوق المدفوع المعتمد مع تخفيض 10,000 ريال. (فرصة تجريبية)",
            "developer", p4, "approximate", 15);
        var tm4 = Terms(op4, 1, TermsStatus.OwnerConfirmed, new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 300000, RemainingBalance = 700000, ArrearsState = "has", Arrears = 20000, ArrearsInBalance = "yes", ArrearsPayer = "buyer", Reduction = 10000,
                Installment = 12000, InstallmentFrequency = "monthly", RemainingInstallments = 57,
            },
            SellerCosts = 0, BuyerCostsNow = 15000, BuyerCostsLater = 0,
            States = new() { ["paid_approved"] = FigureStates.Verified, ["remaining_balance"] = FigureStates.Verified, ["arrears"] = FigureStates.Verified, ["installment_amount"] = FigureStates.Declared, ["buyer_costs_now"] = FigureStates.Estimated },
        }, "يتطلب النقل موافقة المطور ورسوم نقل يحددها المطور؛ طلبنا الموافقة وننتظر الرد. المتأخرات (20,000) يتحملها المشتري عند الإتمام.",
           "المدفوع والرصيد والمتأخرات من كشف المطور. القسط كما في جدول الدفعات. تكاليف المشتري تقدير.", 16);
        op4.PublishedTermsId = tm4.Id; op4.PublishedAt = t0.AddDays(-15); op4.FirstPublishedAt = t0.AddDays(-15); op4.PublishedByUserId = abeer?.Id ?? lama?.Id;
        op4.Checklist = OpportunityFlow.Checklist.Select(c => c.Key).ToList();
        Ev(org, "opportunity", op4.Id, s4.Id, "published", "نُشرت فرصتك للمشترين", true, t0.AddDays(-15), label: abeer?.FullName, from: "ReadyToPublish", to: "Published");

        // 5 · Published — bank-financed villa (Riyadh, حطين), payoff from the financier's letter.
        var r5 = Sale(s5, SaleRequestStatus.ApprovedForListing, "villa", "riyadh", "حطين", null, "financier",
            A(("area", "390"), ("land_area", "375"), ("built_area", "520"), ("bedrooms", "6"), ("bathrooms", "7"), ("floors_count", "2"), ("garden", "true"), ("parking", "two_plus"),
              ("finishing", "full"), ("readiness", "ready"), ("description", "فيلا دورين وملحق مع فناء، قريبة من الخدمات. (بيانات تجريبية)"), ("features", "maid_room,driver_room,annex,pool")),
            25, 24.7640, 46.6011, "exact");
        var o5 = Obl(r5, "financier", financiers[1], A(("purchase_price", "2300000"), ("current_installment", "11200"), ("installment_frequency", "monthly"), ("remaining_months", "210"),
            ("arrears_state", "none"), ("payoff_amount", "1650000"), ("payoff_valid_until", "2026-11-15"), ("asking_price", "2400000")));
        var pl5 = await Doc(r5, "payoff_letter", FileReviewStatus.Accepted, o5.Id);
        await Doc(r5, "financing_contract", FileReviewStatus.Accepted, o5.Id);
        await Doc(r5, "financier_statement", FileReviewStatus.Accepted, o5.Id);
        await Doc(r5, "ownership_proof", FileReviewStatus.Accepted);
        Verify(r5, o5, "payoff_amount", "1650000", "payoff_letter", pl5, 19);
        var p5 = await Photos(r5, 3, 1, FileReviewStatus.Accepted, shade: 2);
        db.ExternalApprovals.Add(new ExternalApproval
        {
            OrganizationId = org, SaleRequestId = r5.Id, ApplicantUserId = s5.Id, ObligationId = o5.Id, Status = ExternalApprovalStatus.Conditional,
            Conditions = "سداد كامل المبلغ في خطاب الجهة قبل إفراغ العقار (تجريبي).", DocumentId = pl5.Id, DecisionDate = DateOnly.FromDateTime(t0.AddDays(-19).UtcDateTime),
            ExpiresOn = new DateOnly(2026, 11, 15), RecordedByUserId = nayef?.Id ?? Guid.Empty, RecordedByLabel = nayef?.FullName ?? "فريق رهون", RecordedAt = t0.AddDays(-18),
        });
        var op5 = Opp(r5, OpportunityStatus.Published, "فيلا 6 غرف مع ملحق ومسبح في حي حطين، الرياض",
            "فيلا دورين بتشطيب كامل وملحق وفناء ومسبح. مبلغ السداد لجهة التمويل من خطابها الصالح حتى منتصف نوفمبر 2026. (فرصة تجريبية)", "financier", p5, "exact", 12);
        var tm5 = Terms(op5, 1, TermsStatus.OwnerConfirmed, new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 2400000, PayoffAmount = 1650000, PayoffValidUntil = "2026-11-15", ArrearsState = "none" },
            SellerCosts = 25000, BuyerCostsNow = 0, BuyerCostsLater = 0, NeedsNewFinancing = true,
            States = new() { ["sale_price"] = FigureStates.Declared, ["payoff_amount"] = FigureStates.Verified, ["seller_costs"] = FigureStates.Estimated },
        }, "يُسدد مبلغ الجهة كاملًا عند الإتمام ثم يُفرغ العقار للمشتري. يمكن الشراء نقدًا أو بتمويل جديد يخضع لموافقة جهة المشتري.",
           "مبلغ السداد من خطاب الجهة. سعر البيع كما طلبه المالك.", 13);
        op5.PublishedTermsId = tm5.Id; op5.PublishedAt = t0.AddDays(-12); op5.FirstPublishedAt = t0.AddDays(-12); op5.PublishedByUserId = abeer?.Id ?? lama?.Id;
        op5.Checklist = OpportunityFlow.Checklist.Select(c => c.Key).ToList();

        // 6 · Awaiting the owner's confirmation — developer apartment (Dammam).
        var r6 = Sale(s6, SaleRequestStatus.ApprovedForListing, "apartment", "dammam", "الشاطئ", null, "developer",
            A(("area", "130"), ("bedrooms", "2"), ("bathrooms", "2"), ("floor", "5"), ("elevator", "true"), ("readiness", "ready"),
              ("description", "شقة بإطلالة جزئية على البحر قرب الكورنيش. (بيانات تجريبية)")), 19, 26.4520, 50.1180);
        var o6 = Obl(r6, "developer", developers[3], A(("paid_approved", "190000"), ("remaining_balance", "410000"), ("installment_amount", "7000"),
            ("installment_frequency", "monthly"), ("remaining_installments", "58"), ("extra_payments", "none"), ("arrears_state", "none")));
        await Doc(r6, "developer_contract", FileReviewStatus.Accepted, o6.Id);
        await Doc(r6, "payment_proof", FileReviewStatus.Accepted, o6.Id);
        var p6 = await Photos(r6, 3, 0, FileReviewStatus.Accepted, shade: 2);
        var op6 = Opp(r6, OpportunityStatus.AwaitingOwnerConfirmation, "شقة غرفتين قرب الكورنيش في حي الشاطئ، الدمام",
            "شقة جاهزة بإطلالة جزئية على البحر، الدور الخامس مع مصعد. (فرصة تجريبية)", "developer", p6, "approximate", 2);
        var tm6 = Terms(op6, 1, TermsStatus.SentToOwner, new TermsInput
        {
            Developer = new DeveloperTerms { PaidApproved = 190000, RemainingBalance = 410000, ArrearsState = "none", Installment = 7000, InstallmentFrequency = "monthly", RemainingInstallments = 58 },
            SellerCosts = 0, BuyerCostsNow = 8000, BuyerCostsLater = 0,
            States = new() { ["paid_approved"] = FigureStates.Declared, ["remaining_balance"] = FigureStates.Declared, ["buyer_costs_now"] = FigureStates.Estimated },
        }, "يتطلب النقل موافقة المطور.", "الأرقام كما أدخلتها المالكة؛ ننتظر كشف المطور.", 3);
        op6.DraftTermsId = tm6.Id;
        Ev(org, "opportunity", op6.Id, s6.Id, "sent_to_owner", "ملخص الفرصة بانتظار تأكيدك (الإصدار 1)", true, t0.AddDays(-2), label: nayef?.FullName,
            body: "راجع الأرقام وشروط النقل ودقة الموقع والصور، ثم أكد الملخص أو اطلب تعديله. لن تُنشر الفرصة قبل تأكيدك.", from: "Preparing", to: "AwaitingOwnerConfirmation");

        // 7 · Being prepared — bank-financed land (Riyadh, العارض): no rooms, costs not entered yet (incomplete, never 0).
        var r7 = Sale(s7, SaleRequestStatus.ApprovedForListing, "land", "riyadh", "العارض", null, "financier",
            A(("area", "625"), ("land_use", "residential"), ("frontages", "2"), ("street_width", "20"), ("features", "corner,paved"),
              ("description", "أرض سكنية زاوية على شارعين. (بيانات تجريبية)")), 17, 24.8890, 46.6012);
        var o7 = Obl(r7, "financier", financiers[2], A(("arrears_state", "none"), ("payoff_amount", "540000"), ("asking_price", "1150000")));
        var p7 = await Photos(r7, 2, 3, FileReviewStatus.Pending);
        var op7 = Opp(r7, OpportunityStatus.Preparing, "أرض سكنية زاوية في حي العارض، الرياض", "أرض سكنية زاوية على شارعين بعرض 20 م. (فرصة تجريبية)", "financier", p7, "approximate", 4);
        var tm7 = Terms(op7, 1, TermsStatus.Draft, new TermsInput
        {
            Financier = new FinancierTerms { SalePrice = 1150000, PayoffAmount = 540000, ArrearsState = "none" }, SellerCosts = null, BuyerCostsNow = null, BuyerCostsLater = null,
            NeedsNewFinancing = true, States = new() { ["sale_price"] = FigureStates.Declared, ["payoff_amount"] = FigureStates.Declared },
        }, null, null, 4);
        op7.DraftTermsId = tm7.Id;
        _ = o7;

        // 8 · Published with an incomplete estimate — developer townhouse under construction (Jeddah): costs unknown, so due-now is unknown.
        var r8 = Sale(s8, SaleRequestStatus.ApprovedForListing, "townhouse", "jeddah", "الصفا", "حدائق الصفا (تجريبي)", "developer",
            A(("area", "240"), ("bedrooms", "4"), ("bathrooms", "4"), ("floors_count", "2"), ("readiness", "under_construction"), ("delivery_month", "2027-03"),
              ("description", "تاون هاوس دورين ضمن مشروع مسوّر تحت الإنشاء. (بيانات تجريبية)")), 23, 21.5810, 39.2130);
        var o8 = Obl(r8, "developer", developers[0], A(("paid_approved", "260000"), ("remaining_balance", "840000"), ("installment_amount", "21000"),
            ("installment_frequency", "quarterly"), ("remaining_installments", "32"), ("extra_payments", "has"), ("extra_payment_amount", "50000"),
            ("extra_payment_recurrence", "annual"), ("extra_payment_date", "2027-01-15"), ("arrears_state", "none")));
        var st8 = await Doc(r8, "payment_proof", FileReviewStatus.Accepted, o8.Id);
        Verify(r8, o8, "paid_approved", "260000", "developer_statement", st8, 18);
        var p8 = await Photos(r8, 3, 2, FileReviewStatus.Accepted);
        var op8 = Opp(r8, OpportunityStatus.Published, "تاون هاوس 4 غرف تحت الإنشاء في حي الصفا، جدة",
            "تاون هاوس دورين ضمن مشروع مسوّر، التسليم المتوقع مارس 2027. القسط ربع سنوي مع دفعة سنوية. (فرصة تجريبية)", "developer", p8, "approximate", 10);
        var tm8 = Terms(op8, 1, TermsStatus.OwnerConfirmed, new TermsInput
        {
            Developer = new DeveloperTerms
            {
                PaidApproved = 260000, RemainingBalance = 840000, ArrearsState = "none", Installment = 21000, InstallmentFrequency = "quarterly", RemainingInstallments = 32,
                ExtraPayment = 50000, ExtraPaymentRecurrence = "annual", ExtraPaymentDate = "2027-01-15",
            },
            SellerCosts = 0, BuyerCostsNow = null, BuyerCostsLater = 0,
            States = new() { ["paid_approved"] = FigureStates.Verified, ["remaining_balance"] = FigureStates.Declared, ["installment_amount"] = FigureStates.Declared },
        }, "رسوم النقل لدى المطور لم تُحدد بعد.", "المدفوع من كشف المطور. الرصيد والأقساط كما أدخلتها المالكة.", 11);
        op8.PublishedTermsId = tm8.Id; op8.PublishedAt = t0.AddDays(-10); op8.FirstPublishedAt = t0.AddDays(-10); op8.PublishedByUserId = abeer?.Id ?? lama?.Id;
        op8.Checklist = OpportunityFlow.Checklist.Select(c => c.Key).ToList();

        // ── Buyers ──
        var br1 = new BuyerRequest
        {
            OrganizationId = org, ApplicantUserId = b1.Id, Reference = "BR-2026-00001", ClientDraftId = Guid.CreateVersion7(), Status = BuyerRequestStatus.Submitted,
            StatusChangedAt = t0.AddDays(-1), SubmittedAt = t0.AddDays(-1), AvailableNow = 350000, InstallmentComfort = 14000, InstallmentFrequency = "monthly",
            PurchaseMode = "cash", Cities = ["riyadh"], PropertyTypes = ["apartment", "duplex"], BedroomsMin = 3, Readiness = "any", ContactName = b1.Name,
            DeclarationsAcceptedAt = t0.AddDays(-1), DeclarationsVersion = BuyerEndpoints.DeclarationsVersion, IsDemo = true, CreatedAt = t0.AddDays(-1), UpdatedAt = t0.AddDays(-1),
        };
        var br2 = new BuyerRequest
        {
            OrganizationId = org, ApplicantUserId = b2.Id, Reference = "BR-2026-00002", ClientDraftId = Guid.CreateVersion7(), Status = BuyerRequestStatus.ApprovedForMatching,
            StatusChangedAt = t0.AddDays(-9), SubmittedAt = t0.AddDays(-11), AvailableNow = 400000, InstallmentComfort = 13000, InstallmentFrequency = "monthly", MaxPrice = 1100000,
            PurchaseMode = "cash", Cities = ["riyadh"], PropertyTypes = ["apartment"], BedroomsMin = 2, Readiness = "ready", ContactName = b2.Name,
            ReviewedAvailableNow = 400000, CapacityReviewNote = "كشف حساب بنكي حديث (تجريبي).", CapacityReviewedAt = t0.AddDays(-9), CapacityReviewedByLabel = nayef?.FullName,
            DeclarationsAcceptedAt = t0.AddDays(-11), DeclarationsVersion = BuyerEndpoints.DeclarationsVersion, AssignedToUserId = nayef?.Id, AssignedToLabel = nayef?.FullName,
            IsDemo = true, CreatedAt = t0.AddDays(-11), UpdatedAt = t0.AddDays(-9),
        };
        db.BuyerRequests.AddRange(br1, br2);
        Ev(org, "buyer_request", br1.Id, b1.Id, "submitted", "استلمنا طلب الشراء", true, t0.AddDays(-1), "applicant", b1.Name, to: "Submitted");
        Ev(org, "buyer_request", br2.Id, b2.Id, "approved", "ملفك معتمد للمطابقة", true, t0.AddDays(-9), label: nayef?.FullName,
            body: "اعتماد فريق رهون لملفك لا يعني موافقة بنك أو جهة تمويل.", from: "UnderReview", to: "ApprovedForMatching");

        var in1 = new Interest
        {
            OrganizationId = org, ApplicantUserId = b2.Id, Reference = "IN-2026-00001", OpportunityId = op4.Id, TermsId = tm4.Id, BuyerRequestId = br2.Id,
            Message = "أرغب بمعرفة موعد رد المطور على النقل.", ContactPreference = "call", ContactName = b2.Name, StatusChangedAt = t0.AddDays(-5), CreatedAt = t0.AddDays(-5), UpdatedAt = t0.AddDays(-5),
        };
        db.Interests.Add(in1);
        Ev(org, "interest", in1.Id, b2.Id, "created", $"أرسلت اهتمامك بالفرصة {op4.Reference}", true, t0.AddDays(-5), "applicant", b2.Name,
            body: "وصل اهتمامك لفريق رهون وسيتواصل معك. الاهتمام لا يحجز العقار ولا يعد عرضًا ملزمًا.");
        db.SavedOpportunities.Add(new SavedOpportunity { OrganizationId = org, ApplicantUserId = b2.Id, OpportunityId = op5.Id });

        db.ContactMessages.Add(new ContactMessage
        {
            OrganizationId = org, Reference = "CM-2026-00001", Name = "زائر تجريبي", PhoneEnc = pii.Protect("0561110099"), PhoneMasked = Mask.Phone("0561110099"),
            Topic = "buy", Message = "هل تتوفر فرص في الخبر بقسط ربع سنوي؟ (رسالة تجريبية)", CreatedAt = t0.AddDays(-1), UpdatedAt = t0.AddDays(-1),
        });

        await db.SaveChangesAsync();
        foreach (var (prefix, value) in new[] { ("SR", no), ("OP", oppNo), ("BR", 2), ("IN", 1), ("CM", 1) })
        {
            var key = $"market:{prefix}:2026";
            await db.Database.ExecuteSqlAsync($"INSERT INTO cases.reference_counters (key, value) VALUES ({key}, {(long)value}) ON CONFLICT (key) DO UPDATE SET value = GREATEST(cases.reference_counters.value, {(long)value})");
        }
        log.LogInformation("Market demo data seeded (test data, labelled تجريبي).");
    }
}
