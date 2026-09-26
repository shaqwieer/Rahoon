using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Requests;

namespace Rahoon.Api.Seed;

/// <summary>
/// Phase 1A: the «فريق رهون» team (fictional members) and two fictional individuals' requests so the team queue is not
/// empty in the demo. The live demo individual registers on a phone (/start) and is not seeded.
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

    private async Task SeedRequestsAsync(PiiProtector pii)
    {
        var team = _orgs["rahoon-team"];
        var institutions = await db.FinancingInstitutions.ToDictionaryAsync(i => i.NameAr);

        Request Make(string reference, string name, string nationalId, string phone, string lender, RequestStatus status, DateTimeOffset submitted,
            RequestPathPreference pref, decimal installment, string arrears, string city, string situation)
        {
            var userId = Guid.CreateVersion7();
            db.Users.Add(new User
            {
                Id = userId, Email = $"individual+{userId:N}@individuals.rahoon.local", FullName = name, AccountKind = AccountKind.Individual,
                MfaEnrolled = true, CreatedAt = submitted.AddDays(-1), UpdatedAt = submitted,
            });
            db.IndividualProfiles.Add(new IndividualProfile
            {
                UserId = userId, NationalIdEnc = pii.Protect(nationalId), NationalIdHash = pii.LookupHash(nationalId), NationalIdMasked = Mask.NationalId(nationalId),
                IdType = "citizen", PhoneEnc = pii.Protect(phone), PhoneHash = pii.LookupHash(phone), PhoneMasked = Mask.Phone(phone),
                PhoneVerifiedAt = submitted.AddDays(-1), TermsVersion = Modules.Identity.IndividualAuthEndpoints.TermsVersion, TermsAcceptedAt = submitted.AddDays(-1),
                CreatedAt = submitted.AddDays(-1), UpdatedAt = submitted,
            });
            var inst = institutions[lender];
            var r = new Request
            {
                OrganizationId = team.Id, Reference = reference, ApplicantUserId = userId, Status = status, WaitingOn = RequestStatusInfo.DefaultWaitingOn(status),
                StatusChangedAt = submitted, SubmittedAt = submitted, InstitutionId = inst.Id, ApplicantFullName = name, MonthlyInstallment = installment,
                ArrearsDuration = arrears, PropertyCity = city, PathPreference = pref, SituationText = situation, CreatedAt = submitted.AddHours(-2), UpdatedAt = submitted,
            };
            db.Requests.Add(r);
            db.RequestConsents.Add(new RequestConsent
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = userId, TextVersion = MyRequestEndpoints.ConsentTextVersion,
                TextSnapshot = MyRequestEndpoints.ConsentText(lender), InstitutionId = inst.Id, RecipientName = lender,
                DataCategories = ["identity", "contact", "declared_finance", "situation", "documents"], OtpVerifiedAt = submitted.AddMinutes(-5),
            });
            db.RequestUpdates.Add(new RequestUpdate
            {
                OrganizationId = team.Id, RequestId = r.Id, ApplicantUserId = userId, Kind = "submitted", Title = "أرسلت طلبك إلى فريق رهون",
                AuthorKind = "applicant", At = submitted,
            });
            _audit.Add(new AuditEvent
            {
                OrganizationId = team.Id, Type = "request.transition", Title = "مسودة ← مقدَّم", FromState = "draft", ToState = "submitted",
                ActorType = "individual", ActorUserId = userId, ActorLabel = name, Detail = "إرسال الطلب", SubjectType = "request", SubjectReference = reference,
                OccurredAt = new DateTimeOffset(submitted.UtcTicks / 10 * 10, TimeSpan.Zero), PrevHash = "", Hash = "",
            });
            return r;
        }

        Make("REQ-2026-00301", "منيرة سعد الدوسري", "1087654321", "0551110001", "مصرف الواحة", RequestStatus.Submitted, DemoToday.AddDays(-1),
            RequestPathPreference.Settlement, 3800m, "6to12m", "الدمام", "توفي زوجي وانخفض دخل الأسرة، وأريد تسوية المديونية إن أمكن.");

        var inReview = Make("REQ-2026-00302", "سعد فهد العنزي", "1076543210", "0551110002", "مصرف الأفق", RequestStatus.TeamReview, DemoToday.AddDays(-3),
            RequestPathPreference.KeepHome, 5200m, "3to6m", "الرياض", "انتقلت لعمل براتب أقل، وأحتاج قسطاً أخف لأبقى في منزلي.");
        inReview.AssignedCoordinatorId = _users["nayef"].Id;
        inReview.AssignedAt = DemoToday.AddDays(-2);
        inReview.StatusChangedAt = DemoToday.AddDays(-2);
        db.RequestUpdates.Add(new RequestUpdate
        {
            OrganizationId = team.Id, RequestId = inReview.Id, ApplicantUserId = inReview.ApplicantUserId, Kind = "status", Title = "بدأ فريق رهون دراسة طلبك",
            AuthorKind = "team", AuthorLabel = "فريق رهون", At = DemoToday.AddDays(-2),
        });

        await db.SaveChangesAsync();
        // The live demo continues from REQ-2026-00303.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO cases.reference_counters (key, value) VALUES ('request:2026', 302) ON CONFLICT (key) DO UPDATE SET value = GREATEST(cases.reference_counters.value, 302)");
    }
}
