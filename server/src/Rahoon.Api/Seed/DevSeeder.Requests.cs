using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Requests;

namespace Rahoon.Api.Seed;

/// <summary>
/// Phase 1A: the «فريق رهون» team (fictional members) and fictional individuals whose requests sit at every MVP stage,
/// so each screen can be tried without replaying the whole journey. Individuals sign in at /start with the national ID
/// and mobile listed in README («Demo logins»); the SMS code is shown on screen.
/// </summary>
public sealed partial class DevSeeder
{
    private void SeedRahoonTeamUsers()
    {
        var team = _orgs["rahoon-team"];
        User("lama", "l.alharbi@team.rahoon.example", "لمى الحربي", "0550000601", (team, SystemRoles.TeamLead, "قائدة الفريق"));
        User("nayef", "n.alyami@team.rahoon.example", "نايف اليامي", "0550000602", (team, SystemRoles.TeamCoordinator, "منسق حالات"));
        User("turki", "t.alshehri@team.rahoon.example", "تركي الشهري", "0550000603", (team, SystemRoles.TeamCoordinator, "منسق حالات"));
        User("abeer", "a.alqahtani@team.rahoon.example", "عبير القحطاني", "0550000604", (team, SystemRoles.TeamVerifier, "مراجِعة العروض"));
    }

    private sealed record DemoIndividual(Guid UserId, string Name);

    private async Task SeedRequestsAsync(PiiProtector pii)
    {
        var team = _orgs["rahoon-team"];
        var institutions = await db.FinancingInstitutions.ToDictionaryAsync(i => i.NameAr);
        var nayef = _users["nayef"];
        var abeer = _users["abeer"];
        var t0 = DemoToday;

        DemoIndividual Person(string name, string nationalId, string phone, DateTimeOffset since)
        {
            var userId = Guid.CreateVersion7();
            db.Users.Add(new User
            {
                Id = userId, Email = $"individual+{userId:N}@individuals.rahoon.local", FullName = name, AccountKind = AccountKind.Individual,
                MfaEnrolled = true, CreatedAt = since, UpdatedAt = since,
            });
            db.IndividualProfiles.Add(new IndividualProfile
            {
                UserId = userId, NationalIdEnc = pii.Protect(nationalId), NationalIdHash = pii.LookupHash(nationalId), NationalIdMasked = Mask.NationalId(nationalId),
                IdType = nationalId[0] == '1' ? "citizen" : "resident", PhoneEnc = pii.Protect(phone), PhoneHash = pii.LookupHash(phone), PhoneMasked = Mask.Phone(phone),
                PhoneVerifiedAt = since, TermsVersion = IndividualAuthEndpoints.TermsVersion, TermsAcceptedAt = since, CreatedAt = since, UpdatedAt = since,
            });
            return new DemoIndividual(userId, name);
        }

        void Update(Request r, string kind, string title, DateTimeOffset at, string? body = null, string author = "team", bool visible = true) =>
            db.RequestUpdates.Add(new RequestUpdate
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, Title = title, Body = body,
                AuthorKind = author, AuthorLabel = author == "team" ? "فريق رهون" : null, VisibleToApplicant = visible, At = at,
            });

        void Transition(Request r, DemoIndividual who, string from, string to, string title, DateTimeOffset at, User? by = null) =>
            _audit.Add(new AuditEvent
            {
                OrganizationId = team.Id, Type = "request.transition", Title = title, FromState = from, ToState = to,
                ActorType = by is null ? "individual" : "rahoon_team", ActorUserId = by?.Id ?? who.UserId, ActorLabel = by?.FullName ?? who.Name,
                SubjectType = "request", SubjectReference = r.Reference, OccurredAt = new DateTimeOffset(at.UtcTicks / 10 * 10, TimeSpan.Zero), PrevHash = "", Hash = "",
            });

        Request Make(DemoIndividual who, string reference, string lender, RequestStatus status, DateTimeOffset submitted,
            RequestPathPreference pref, decimal installment, string arrears, string city, string situation, string? contract = null)
        {
            var inst = institutions[lender];
            var r = new Request
            {
                OrganizationId = team.Id, Reference = reference, ApplicantUserId = who.UserId, Status = status, WaitingOn = RequestStatusInfo.DefaultWaitingOn(status),
                StatusChangedAt = submitted, SubmittedAt = submitted, InstitutionId = inst.Id, ApplicantFullName = who.Name, MonthlyInstallment = installment,
                ArrearsDuration = arrears, PropertyCity = city, PathPreference = pref, SituationText = situation, ContractNumber = contract,
                CreatedAt = submitted.AddHours(-2), UpdatedAt = submitted,
            };
            db.Requests.Add(r);
            db.RequestConsents.Add(new RequestConsent
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = who.UserId, TextVersion = MyRequestEndpoints.ConsentTextVersion,
                TextSnapshot = MyRequestEndpoints.ConsentText(lender), InstitutionId = inst.Id, RecipientName = lender,
                DataCategories = ["identity", "contact", "declared_finance", "situation", "documents"], OtpVerifiedAt = submitted.AddMinutes(-5),
            });
            Update(r, "submitted", "أرسلت طلبك إلى فريق رهون", submitted, author: "applicant");
            Transition(r, who, "draft", "submitted", "مسودة ← مقدَّم", submitted);
            return r;
        }

        void PickUp(Request r, DemoIndividual who, DateTimeOffset at, bool identity = true)
        {
            r.AssignedCoordinatorId = nayef.Id;
            r.AssignedAt = at;
            r.StatusChangedAt = at;
            Update(r, "status", "بدأ فريق رهون دراسة طلبك", at);
            Transition(r, who, "submitted", "team_review", "مقدَّم ← قيد دراسة فريق رهون", at, nayef);
            if (!identity) return;
            r.IdentityCheckedAt = at.AddHours(2);
            r.IdentityCheckedByUserId = nayef.Id;
            r.IdentityCheckNote = "طابقنا صورة الهوية مع الاسم ورقم الجوال (بيانات تجريبية).";
            Update(r, "identity_check", "تحقق فريق رهون من هوية العميل", at.AddHours(2), r.IdentityCheckNote, visible: false);
        }

        void Coordinate(Request r, DemoIndividual who, DateTimeOffset at, string? visibleText = null, string kind = "general", string summary = "عرضنا ملخص الطلب بموافقة العميل وطلبنا دراسته.")
        {
            db.CoordinationEntries.Add(new CoordinationEntry
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = kind, Channel = kind == "general" ? "phone" : "email",
                OccurredAt = at, Counterpart = "إدارة التحصيل — أ. خالد (بيانات تجريبية)", Summary = summary,
                VisibleToApplicant = visibleText is not null, ApplicantText = visibleText, RecordedByUserId = nayef.Id, RecordedByLabel = nayef.FullName, CreatedAt = at, UpdatedAt = at,
            });
            if (visibleText is not null) Update(r, "coordination", "تحديث من التنسيق مع جهتك الممولة", at, visibleText);
        }

        void StartCoordination(Request r, DemoIndividual who, DateTimeOffset at)
        {
            Coordinate(r, who, at.AddHours(-1), "تواصلنا مع جهتك الممولة وأرسلنا لها ملخص طلبك.");
            r.Status = RequestStatus.LenderCoordination;
            r.WaitingOn = RequestWaitingOn.Lender;
            r.StatusChangedAt = at;
            Update(r, "status", "بدأ فريق رهون التنسيق مع جهتك الممولة", at, "نشارك جهتك البيانات التي وافقت عليها فقط، ونبلغك بما يصلنا منها.");
            Transition(r, who, "team_review", "lender_coordination", "قيد دراسة فريق رهون ← قيد التنسيق مع جهتك الممولة", at, nayef);
        }

        async Task<RequestDocument> LetterAsync(Request r, string lender, string lenderRef, DateTimeOffset at, bool visible)
        {
            await using var stream = new MemoryStream(DemoPdf($"{lender} — {lenderRef} — خطاب تجريبي"));
            var stored = await storage.SaveAsync(stream, $"{lenderRef}.pdf", team.Id);
            var doc = new RequestDocument
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, Kind = "lender_letter", Name = "خطاب الجهة الممولة", Source = "team",
                Visibility = visible ? RequestDocumentVisibility.ApplicantAndTeam : RequestDocumentVisibility.TeamOnly, AddedAfterSubmit = true, VersionCount = 1,
                CreatedAt = at, UpdatedAt = at,
            };
            var v = new RequestDocumentVersion
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, DocumentId = doc.Id, VersionNo = 1, FileName = $"{lenderRef}.pdf",
                ContentType = stored.ContentType, SizeBytes = stored.SizeBytes, Sha256 = stored.Sha256, StorageKey = stored.StorageKey,
                UploadedByUserId = nayef.Id, UploadedByLabel = nayef.FullName, UploadedAt = at, ScanStatus = ScanStatus.Clean, CreatedAt = at, UpdatedAt = at,
            };
            doc.CurrentVersionId = v.Id;
            db.RequestDocuments.Add(doc);
            db.RequestDocumentVersions.Add(v);
            return doc;
        }

        RequestOffer Offer(Request r, RequestDocument letter, string path, string lenderRef, DateTimeOffset recorded, bool published)
        {
            var o = new RequestOffer
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = r.ApplicantUserId, VersionNo = 1, Path = path, LenderReference = lenderRef,
                LenderLetterDate = DateOnly.FromDateTime(recorded.AddDays(-1).UtcDateTime), LetterDocumentId = letter.Id, ShareLetterWithApplicant = true,
                RecordedByUserId = nayef.Id, RecordedByLabel = nayef.FullName, RecordedAt = recorded, CreatedAt = recorded, UpdatedAt = recorded,
                EffectText = "",
            };
            if (path == "p1")
            {
                o.NewInstallment = 3100m;
                o.TermMonths = 240;
                o.StartText = "من القسط التالي بعد توقيع الملحق لدى الجهة";
                o.EffectText = "ينخفض قسطك الشهري من 4,200 إلى 3,100 ريال، وتطول مدة التمويل، وتبقى في منزلك.";
            }
            else
            {
                o.SettlementAmount = 380000m;
                o.PaymentConditions = "دفعة واحدة خلال الأجل المذكور في خطاب الجهة";
                o.RemainingText = "بعد السداد تُصدر الجهة مخالصة ويُفك الرهن، ولا يتبقى عليك شيء من هذا التمويل.";
                o.EffectText = "تُسدَّد المديونية بمبلغ 380,000 ريال بدلاً من 452,000، ويُفك الرهن بعد السداد.";
            }
            o.LenderValidityText = "صالح 30 يوماً من تاريخ الخطاب";
            if (published)
            {
                o.Status = RequestOfferStatus.Published;
                o.VerifiedByUserId = abeer.Id;
                o.VerifiedByLabel = abeer.FullName;
                o.VerifiedAt = recorded.AddHours(3);
                o.PublishedAt = recorded.AddHours(3);
                o.VerificationChecklist = [.. OfferEndpoints.VerifyChecklist];
            }
            db.RequestOffers.Add(o);
            return o;
        }

        // A — submitted, not yet assigned (team: «غير مسندة»).
        var munira = Person("منيرة سعد الدوسري", "1087654321", "0551110001", t0.AddDays(-2));
        Make(munira, "REQ-2026-00301", "مصرف الواحة", RequestStatus.Submitted, t0.AddDays(-1),
            RequestPathPreference.Settlement, 3800m, "6to12m", "الدمام", "توفي زوجي وانخفض دخل الأسرة، وأريد تسوية المديونية إن أمكن.");

        // B — in review with نايف (identity not yet checked) + a closed request (offer accepted, relayed): several requests on one account.
        var saad = Person("سعد فهد العنزي", "1076543210", "0551110002", t0.AddDays(-30));
        var review = Make(saad, "REQ-2026-00302", "مصرف الأفق", RequestStatus.TeamReview, t0.AddDays(-3),
            RequestPathPreference.KeepHome, 5200m, "3to6m", "الرياض", "انتقلت لعمل براتب أقل، وأحتاج قسطاً أخف لأبقى في منزلي.");
        PickUp(review, saad, t0.AddDays(-2), identity: false);

        var closed = Make(saad, "REQ-2026-00306", "شركة السنبلة للتمويل", RequestStatus.Closed, t0.AddDays(-25),
            RequestPathPreference.KeepHome, 4200m, "lt3m", "الرياض", "تمويل ثانٍ لدى شركة تمويل؛ أحتاج إعادة جدولة.", "SN-2024-55120");
        PickUp(closed, saad, t0.AddDays(-24));
        StartCoordination(closed, saad, t0.AddDays(-22));
        var closedLetter = await LetterAsync(closed, "شركة السنبلة للتمويل", "SN-2026-0917", t0.AddDays(-15), visible: true);
        var closedOffer = Offer(closed, closedLetter, "p1", "SN-2026-0917", t0.AddDays(-15), published: true);
        Update(closed, "offer_published", "وصل عرض من جهتك الممولة", t0.AddDays(-15).AddHours(3), "سجّل فريق رهون العرض من خطاب جهتك، وتحقق منه عضو آخر من الفريق. راجعه، والقرار لك.");
        var responseAt = t0.AddDays(-12);
        var relayId = Guid.CreateVersion7();
        db.RequestResponses.Add(new RequestResponse
        {
            OrganizationId = team.Id, RequestId = closed.Id, ApplicantUserId = saad.UserId, OfferId = closedOffer.Id, Reference = "REQ-2026-00306-R1", Kind = "accept",
            ConsentTextSnapshot = OfferEndpoints.AcceptText("شركة السنبلة للتمويل", "SN-2026-0917"), OtpVerifiedAt = responseAt, At = responseAt,
            RelayedAt = responseAt.AddDays(1), RelayedByUserId = nayef.Id, RelayEntryId = relayId, CreatedAt = responseAt, UpdatedAt = responseAt,
        });
        Update(closed, "response", "سجّلنا موافقتك على العرض", responseAt, author: "applicant");
        db.CoordinationEntries.Add(new CoordinationEntry
        {
            Id = relayId, OrganizationId = team.Id, RequestId = closed.Id, ApplicantUserId = saad.UserId, Kind = "response_relay", Channel = "email",
            OccurredAt = responseAt.AddDays(1), Counterpart = "إدارة التحصيل — أ. خالد (بيانات تجريبية)", Summary = "أرسلنا موافقة العميل بالبريد الرسمي.",
            VisibleToApplicant = true, ApplicantText = "نقلنا ردك إلى جهتك الممولة، وسنبلغك بالخطوة التالية.", RecordedByUserId = nayef.Id, RecordedByLabel = nayef.FullName,
            CreatedAt = responseAt.AddDays(1), UpdatedAt = responseAt.AddDays(1),
        });
        Update(closed, "relay", "نقلنا ردك إلى جهتك الممولة", responseAt.AddDays(1), "نقلنا ردك إلى جهتك الممولة، وسنبلغك بالخطوة التالية.");
        closed.Status = RequestStatus.Closed;
        closed.OutcomeCode = "offer_accepted";
        closed.OutcomeSummary = "قبلت عرض جهتك ونقلنا موافقتك إليها. تنفيذ الاتفاق يتم مع جهتك الممولة.";
        closed.ClosedAt = responseAt.AddDays(2);
        closed.StatusChangedAt = responseAt.AddDays(2);
        closed.WaitingOn = RequestWaitingOn.None;
        Update(closed, "closed", "أُغلق طلبك", responseAt.AddDays(2), closed.OutcomeSummary);

        // C — the team asked for information; waits on the individual («نحتاج معلومة منك»).
        var haya = Person("هيا عبدالرحمن القحطاني", "1065432109", "0551110003", t0.AddDays(-6));
        var info = Make(haya, "REQ-2026-00303", "بنك الريادة", RequestStatus.InfoRequested, t0.AddDays(-5),
            RequestPathPreference.NotSure, 3600m, "3to6m", "جدة", "مرضت وتوقفت عن العمل لأشهر، ولا أعرف ما الأنسب لي.");
        PickUp(info, haya, t0.AddDays(-4));
        info.StatusBeforeInfoRequest = RequestStatus.TeamReview;
        info.StatusChangedAt = t0.AddDays(-3);
        info.NextStepText = "نحتاج كشف حساب آخر 3 أشهر لنفهم دخلك الحالي قبل التواصل مع جهتك.";
        Update(info, "info_request", "طلب فريق رهون معلومة منك", t0.AddDays(-3), "• كشف حساب آخر 3 أشهر\n\n" + info.NextStepText);
        db.RequestMessages.Add(new RequestMessage
        {
            OrganizationId = team.Id, RequestId = info.Id, ApplicantUserId = haya.UserId, AuthorKind = "team", AuthorUserId = nayef.Id, AuthorLabel = "فريق رهون",
            Body = "مرحباً هيا، يمكنك رفع الكشف من «إضافة معلومة أو مستند» في صفحة طلبك.", At = t0.AddDays(-3), CreatedAt = t0.AddDays(-3), UpdatedAt = t0.AddDays(-3),
        });

        // D — coordinating; the lender's offer is recorded by نايف and waits for عبير's verification.
        var faisal = Person("فيصل ناصر الشمري", "1054321098", "0551110004", t0.AddDays(-12));
        var pending = Make(faisal, "REQ-2026-00304", "مصرف الأفق", RequestStatus.LenderCoordination, t0.AddDays(-11),
            RequestPathPreference.KeepHome, 4200m, "3to6m", "الرياض", "انخفض دخلي بعد تغيير العمل، وأريد البقاء في منزلي.", "AF-2019-448210");
        PickUp(pending, faisal, t0.AddDays(-10));
        StartCoordination(pending, faisal, t0.AddDays(-8));
        var pendingLetter = await LetterAsync(pending, "مصرف الأفق", "AF-2026-7781", t0.AddDays(-1), visible: false);
        Offer(pending, pendingLetter, "p1", "AF-2026-7781", t0.AddDays(-1), published: false);
        Update(pending, "offer_recorded", "سُجّل عرض الجهة v1 وينتظر التحقق", t0.AddDays(-1), visible: false);

        // E — a verified settlement offer is published; the individual can accept (SMS code), ask, or decline.
        var nouf = Person("نوف خالد العتيبي", "2043210987", "0551110005", t0.AddDays(-15));
        var available = Make(nouf, "REQ-2026-00305", "مصرف النخيل", RequestStatus.OfferAvailable, t0.AddDays(-14),
            RequestPathPreference.Settlement, 6100m, "gt12m", "الخبر", "تعثرت منذ أكثر من سنة، وأستطيع دفع مبلغ مقطوع من بيع أرض أملكها.", "NK-2018-10077");
        PickUp(available, nouf, t0.AddDays(-13));
        StartCoordination(available, nouf, t0.AddDays(-11));
        var availableLetter = await LetterAsync(available, "مصرف النخيل", "NK-2026-3302", t0.AddDays(-2), visible: true);
        Offer(available, availableLetter, "p2", "NK-2026-3302", t0.AddDays(-2), published: true);
        available.Status = RequestStatus.OfferAvailable;
        available.StatusChangedAt = t0.AddDays(-2).AddHours(3);
        available.WaitingOn = RequestWaitingOn.Applicant;
        Update(available, "offer_published", "وصل عرض من جهتك الممولة", t0.AddDays(-2).AddHours(3), "سجّل فريق رهون العرض من خطاب جهتك، وتحقق منه عضو آخر من الفريق. راجعه، والقرار لك.");

        // F — judged not suitable (Q4 interim); the individual can object to the decision.
        var majed = Person("ماجد سليمان الزهراني", "1032109876", "0551110006", t0.AddDays(-8));
        var notSuitable = Make(majed, "REQ-2026-00307", "شركة المسكن للتمويل العقاري", RequestStatus.NotEligible, t0.AddDays(-7),
            RequestPathPreference.NotSure, 2500m, "lt3m", "المدينة المنورة", "القسط تمويل سيارة وليس عقاراً، لكني لم أعرف أين أقدّم.");
        PickUp(notSuitable, majed, t0.AddDays(-6));
        notSuitable.NotEligibleReason = "التمويل المذكور تمويل سيارة وليس تمويلاً عقارياً، وهو خارج نطاق الخدمة حالياً. إن كان لديك تمويل عقاري متعثر فقدّم طلباً جديداً له.";
        notSuitable.StatusChangedAt = t0.AddDays(-5);
        notSuitable.WaitingOn = RequestWaitingOn.None;
        Update(notSuitable, "not_eligible", "رأى فريق رهون أن الطلب غير مناسب للخدمة حالياً", t0.AddDays(-5), notSuitable.NotEligibleReason);

        await db.SaveChangesAsync();
        // The live demo continues from REQ-2026-00308.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO cases.reference_counters (key, value) VALUES ('request:2026', 307) ON CONFLICT (key) DO UPDATE SET value = GREATEST(cases.reference_counters.value, 307)");
    }
}
