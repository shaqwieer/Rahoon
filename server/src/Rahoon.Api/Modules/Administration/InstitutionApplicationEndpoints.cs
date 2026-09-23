using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Auth;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Administration;

public sealed record DemoRequest(
    string? OrgName, string? OrgType, string? PortfolioSize, string? ContactName, string? JobTitle, string? Email, string? Mobile, string? Message, bool Consent,
    string? Website /* honeypot: humans leave it empty */);
public sealed record ApplicationReviewRequest(string Action, string? Note, string? VerificationNote, string? AdminName, string? AdminPhone);

/// <summary>
/// S02 public demo request → PA02 institution applications. No account and no case data are created by the request.
/// Approval provisions an Organization in «Onboarding» with the platform default roles/SLA/limits and invites the
/// contact as org_admin; the institution becomes active when that admin accepts (S05).
/// </summary>
public static partial class InstitutionApplicationEndpoints
{
    public static readonly IReadOnlyDictionary<string, string> OrgTypes = new Dictionary<string, string>
    {
        ["bank"] = "بنك", ["finance"] = "تمويل", ["real_estate_finance"] = "تمويل عقاري",
    };

    public static readonly string[] PortfolioBands = ["أقل من 100 حالة", "100 – 500 حالة", "500 – 2,000 حالة", "أكثر من 2,000 حالة"];

    [GeneratedRegex(@"[^a-z0-9]")]
    private static partial Regex NonSlug();

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/lookups/demo-request", () => Results.Ok(new
        {
            orgTypes = OrgTypes.Select(t => new { key = t.Key, label = t.Value }), portfolioBands = PortfolioBands,
        }));
        app.MapPost("/api/public/demo-requests", Submit).RequireRateLimiting("auth");

        var g = app.MapGroup("/api/platform/institution-applications").RequireOrg(OrganizationKind.Platform).RequirePermission(P.PlatformInstitutions);
        g.MapGet("", List);
        g.MapGet("/{reference}", Detail);
        g.MapPost("/{reference}/review", Review).Idempotent();
    }

    public static (string Stage, string Tone) StageOf(ApplicationStatus s) => s switch
    {
        ApplicationStatus.New => ("استلام", "neutral"),
        ApplicationStatus.InReview => ("مراجعة الامتثال", "info"),
        ApplicationStatus.MoreInfoRequested => ("بانتظار المنشأة", "warning"),
        ApplicationStatus.Approved => ("إعداد المنشأة", "success"),
        _ => ("مرفوض", "error"),
    };

    private static async Task<IResult> Submit(DemoRequest req, RahoonDbContext db, RequestContext rc, IClock clock)
    {
        var email = StaffInvitationService.NormalizeEmail(req.Email);
        var orgType = OrgTypes.ContainsKey(req.OrgType ?? "") ? req.OrgType! : OrgTypes.FirstOrDefault(t => t.Value == req.OrgType).Key;
        var v = new Validator()
            .Require(!string.IsNullOrWhiteSpace(req.OrgName) && req.OrgName.Trim().Length is >= 3 and <= 200, "orgName", "أدخل اسم المنشأة.")
            .Require(orgType is not null, "orgType", "اختر نوع المنشأة.")
            .Require(req.PortfolioSize is null || PortfolioBands.Contains(req.PortfolioSize), "portfolioSize", "اختر حجم المحفظة من القائمة.")
            .Require(!string.IsNullOrWhiteSpace(req.ContactName) && req.ContactName.Trim().Length is >= 3 and <= 120, "contactName", "أدخل الاسم.")
            .Require(!string.IsNullOrWhiteSpace(req.JobTitle) && req.JobTitle.Trim().Length <= 120, "jobTitle", "أدخل المسمى الوظيفي.")
            .Require(email is not null, "email", "أدخل بريداً إلكترونياً صحيحاً.")
            .Require(email is null || !EmailDomains.IsPersonal(email), "email", "استخدم بريد المنشأة وليس بريداً شخصياً")
            .Require(string.IsNullOrWhiteSpace(req.Mobile) || CaseFactory.IsValidSaudiMobile(req.Mobile), "mobile", "أدخل رقم جوال سعودي صحيحاً أو اتركه فارغاً.")
            .Require((req.Message?.Length ?? 0) <= 1000, "message", "الرسالة حتى 1000 حرف.")
            .Require(req.Consent, "consent", "الموافقة على استخدام البيانات للتواصل مطلوبة.");
        v.ThrowIfInvalid();
        if (!string.IsNullOrEmpty(req.Website)) return Results.Ok(new { reference = "APP-" + clock.TodayRiyadh.Year + "-0000" }); // bot: same shape, nothing stored

        using var _ = rc.BeginSystemScope();
        var since = clock.UtcNow.AddHours(-24);
        if (await db.InstitutionApplications.CountAsync(a => a.ContactEmail == email && a.CreatedAt >= since) >= 2)
            throw new DomainException("too_many_requests", "استلمنا طلباً من هذا البريد مؤخراً، وسيتواصل معك فريقنا خلال يومي عمل.", StatusCodes.Status429TooManyRequests);

        await using var tx = await db.Database.BeginTransactionAsync();
        var year = clock.TodayRiyadh.Year;
        var key = $"app:{year}";
        var n = (await db.Database.SqlQuery<long>($"""
            INSERT INTO cases.reference_counters (key, value) VALUES ({key}, 1)
            ON CONFLICT (key) DO UPDATE SET value = cases.reference_counters.value + 1
            RETURNING value AS "Value"
            """).ToListAsync())[0];
        var app = new InstitutionApplication
        {
            Reference = $"APP-{year}-{n:D4}", OrgName = req.OrgName!.Trim(), OrgType = OrgTypes[orgType!], ContactName = req.ContactName!.Trim(), ContactEmail = email!,
            ContactPhone = CaseFactory.NormalizePhone(req.Mobile), JobTitle = req.JobTitle!.Trim(), PortfolioSize = req.PortfolioSize, Message = req.Message?.Trim(),
            Status = ApplicationStatus.New, VerificationNote = "جديد", CreatedAt = clock.UtcNow, ConsentAt = clock.UtcNow,
        };
        db.InstitutionApplications.Add(app);
        SandboxEmail.Record(db, clock, null, email!, $"وصلنا طلبك رقم {app.Reference}. سيتواصل معك فريق رهون خلال يومي عمل. لم تُنشأ أي حسابات بعد.", "TPL-DEMO-ACK");
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new
        {
            reference = app.Reference,
            message = $"رقم الطلب {app.Reference}. سيتواصل معك فريقنا خلال يومي عمل على البريد المؤسسي.",
            note = "لم تُنشأ أي حسابات بعد. لن نطلب بيانات عملاء قبل توقيع الاتفاقية.",
        });
    }

    private static object Row(InstitutionApplication a)
    {
        var (stage, tone) = StageOf(a.Status);
        return new
        {
            a.Id, a.Reference, name = a.OrgName, type = a.OrgType, verification = a.VerificationNote ?? "جديد", verificationTone = tone, stage,
            status = a.Status, a.CreatedAt,
        };
    }

    private static async Task<IResult> List(string? status, RahoonDbContext db)
    {
        var q = db.InstitutionApplications.AsNoTracking();
        q = status == "closed" ? q.Where(a => a.Status == ApplicationStatus.Rejected) : status == "all" ? q : q.Where(a => a.Status != ApplicationStatus.Rejected);
        var list = await q.OrderByDescending(a => a.CreatedAt).ToListAsync();
        var activeInstitutions = await db.Organizations.CountAsync(o => o.Kind == OrganizationKind.Lender && o.Status == OrganizationStatus.Active);
        return Results.Ok(new
        {
            counts = new { applications = list.Count(a => a.Status is not (ApplicationStatus.Approved or ApplicationStatus.Rejected)), activeInstitutions },
            note = "التحقق من الترخيص يدوي في المرحلة 1: يرفع مقدم الطلب المستندات ويراجعها الامتثال. لا يُفترض تكامل مع سجلات رسمية.",
            items = list.Select(Row),
        });
    }

    private static async Task<IResult> Detail(string reference, RahoonDbContext db)
    {
        var a = await db.InstitutionApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == reference) ?? throw new NotFoundException();
        var reviewer = a.ReviewedByUserId is { } r ? await db.Users.Where(u => u.Id == r).Select(u => u.FullName).FirstOrDefaultAsync() : null;
        return Results.Ok(new
        {
            application = Row(a), a.ContactName, a.ContactEmail, contactPhone = a.ContactPhone is { } p ? Mask.Phone(p) : null, a.JobTitle, a.PortfolioSize, a.Message,
            a.ReviewNote, reviewer, a.ConsentAt, a.ConsentPolicyVersion, a.CreatedOrganizationId, a.DecidedAt,
            actions = a.Status switch
            {
                ApplicationStatus.New => new[] { "in_review", "reject" },
                ApplicationStatus.InReview => ["more_info", "approve", "reject"],
                ApplicationStatus.MoreInfoRequested => ["in_review", "reject"],
                _ => [],
            },
        });
    }

    private static async Task<IResult> Review(string reference, ApplicationReviewRequest req, RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit,
        StaffInvitationService invitations, AuthOptions options)
    {
        var action = req.Action?.Trim().ToLowerInvariant();
        new Validator()
            .Require(action is "in_review" or "more_info" or "approve" or "reject", "action", "اختر الإجراء.")
            .Require(action is not ("more_info" or "reject") || !string.IsNullOrWhiteSpace(req.Note), "note", "اكتب الملاحظة أو السبب.")
            .Require(string.IsNullOrWhiteSpace(req.AdminPhone) || CaseFactory.IsValidSaudiMobile(req.AdminPhone), "adminPhone", "رقم جوال سعودي غير صحيح.")
            .ThrowIfInvalid();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await db.InstitutionApplications.FirstOrDefaultAsync(x => x.Reference == reference) ?? throw new NotFoundException();
        var allowed = a.Status switch
        {
            ApplicationStatus.New => action is "in_review" or "reject",
            ApplicationStatus.InReview => action is "more_info" or "approve" or "reject",
            ApplicationStatus.MoreInfoRequested => action is "in_review" or "reject",
            _ => false,
        };
        if (!allowed) throw new ConflictException("invalid_transition", "لا يمكن تنفيذ هذا الإجراء في مرحلة الطلب الحالية.");

        string? sandboxToken = null;
        var from = StageOf(a.Status).Stage;
        a.ReviewedByUserId = rc.UserId;
        a.ReviewNote = req.Note?.Trim() ?? a.ReviewNote;
        if (!string.IsNullOrWhiteSpace(req.VerificationNote)) a.VerificationNote = req.VerificationNote.Trim();
        switch (action)
        {
            case "in_review": a.Status = ApplicationStatus.InReview; break;
            case "more_info":
                a.Status = ApplicationStatus.MoreInfoRequested;
                SandboxEmail.Record(db, clock, null, a.ContactEmail, $"طلبك {a.Reference}: نحتاج معلومات إضافية — {a.ReviewNote}", "TPL-DEMO-INFO");
                break;
            case "reject":
                a.Status = ApplicationStatus.Rejected;
                a.DecidedAt = clock.UtcNow;
                SandboxEmail.Record(db, clock, null, a.ContactEmail, $"طلبك {a.Reference}: نعتذر عن عدم قبول الطلب حالياً. {a.ReviewNote}", "TPL-DEMO-REJECT");
                break;
            case "approve":
            {
                using var _ = rc.BeginSystemScope(); // provisioning writes into the new tenant
                var org = await ProvisionAsync(db, a, rc.UserId, clock);
                a.Status = ApplicationStatus.Approved;
                a.DecidedAt = clock.UtcNow;
                a.CreatedOrganizationId = org.Id;
                a.VerificationNote ??= "مكتمل";
                var issued = await invitations.CreateAsync(org, new CreateInvitationRequest(a.ContactEmail, req.AdminName ?? a.ContactName, req.AdminPhone ?? a.ContactPhone, SystemRoles.OrgAdmin, null),
                    rc.UserId, requirePhone: false);
                await db.SaveChangesAsync(); // inside the system scope: the invitation belongs to the new tenant
                sandboxToken = options.ExposeSandboxOtp ? issued.Token : null;
                break;
            }
        }
        await audit.RecordAsync(new AuditEntry("institution.application_reviewed", $"مراجعة طلب انضمام {a.Reference}", FromState: from, ToState: StageOf(a.Status).Stage,
            Reason: a.ReviewNote, Detail: a.OrgName));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { a.Reference, status = a.Status, stage = StageOf(a.Status).Stage, organizationId = a.CreatedOrganizationId, sandboxToken });
    }

    /// <summary>Creates the tenant with platform defaults (PA07): role templates, stage SLAs and the default approval-limit policy.</summary>
    private static async Task<Organization> ProvisionAsync(RahoonDbContext db, InstitutionApplication a, Guid actor, IClock clock)
    {
        var domain = a.ContactEmail.Split('@')[1];
        var baseCode = NonSlug().Replace(domain.Split('.')[0].ToLowerInvariant(), "");
        if (baseCode.Length < 3) baseCode = "org" + baseCode;
        var code = baseCode;
        for (var i = 2; await db.Organizations.AnyAsync(o => o.ShortCode == code); i++) code = $"{baseCode}{i}";
        var words = a.OrgName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w is not ("شركة" or "بنك" or "مصرف" or "مؤسسة")).ToList();
        var initials = words.Count >= 2 ? $"{words[0][0]}{words[1][0]}" : a.OrgName.Replace(" ", "")[..Math.Min(2, a.OrgName.Replace(" ", "").Length)];
        var org = new Organization
        {
            NameAr = a.OrgName, ShortCode = code, Initials = initials, Kind = OrganizationKind.Lender, Status = OrganizationStatus.Onboarding,
            AllowedEmailDomains = [domain], CreatedAt = clock.UtcNow,
        };
        db.Organizations.Add(org);
        foreach (var t in SystemRoles.Templates.Where(t => t.Kind == OrganizationKind.Lender))
        {
            var role = new Role { OrganizationId = org.Id, Key = t.Key, NameAr = t.NameAr, NameEn = t.NameEn };
            role.Permissions.AddRange(t.Permissions.Distinct().Select(p => new RolePermission
            {
                PermissionKey = p, Grant = p is P.SolutionApprove or P.PiiReveal or P.UserManage ? PermissionGrant.Conditional : PermissionGrant.Allow,
            }));
            db.Roles.Add(role);
        }
        (CaseStatus s, int d, string note, bool pause)[] sla =
        [
            (CaseStatus.AwaitingData, 5, "تذكير يوم 3", false), (CaseStatus.Verification, 5, "تصعيد لمدير الفريق", false),
            (CaseStatus.Valuation, 10, "يُحسب من إسناد المقيّم", false), (CaseStatus.ProposedSolution, 5, "افتراض — يتطلب تأكيد المنتج", false),
            (CaseStatus.InternalApproval, 3, "تذكير المعتمد يوم 2", false), (CaseStatus.AwaitingCustomer, 10, "توقف عند شكوى مفتوحة", true),
            (CaseStatus.Negotiation, 7, "افتراض — يتطلب تأكيد المنتج", true), (CaseStatus.AwaitingReconciliation, 5, "تصعيد للمالية", false),
        ];
        foreach (var r in sla)
            db.SlaRules.Add(new SlaRule { OrganizationId = org.Id, Status = r.s.ToString(), BusinessDays = r.d, RuleNote = r.note, PausesOnOpenComplaint = r.pause });
        var policy = new ApprovalLimitPolicy
        {
            OrganizationId = org.Id, VersionNo = 1, Status = "effective", EffectiveFrom = clock.TodayRiyadh, ChangeSummary = "القيم الافتراضية للمنصة عند الانضمام",
            ProposedByUserId = actor, ApprovedByUserId = actor, ApprovedAt = clock.UtcNow,
        };
        policy.Tiers.AddRange(
        [
            new() { Rank = 0, RoleKey = SystemRoles.CreditAnalyst, LevelLabel = "محلل / مدير حالات", SolutionKinds = ["Reschedule"], SolutionKindsLabel = "إعادة جدولة", CanApprove = false, EscalateToLabel = "إعداد فقط، لا اعتماد" },
            new() { Rank = 1, RoleKey = SystemRoles.Approver, LevelLabel = "معتمد", SolutionKinds = ["Reschedule", "GracePeriod"], SolutionKindsLabel = "إعادة جدولة، سماح", MaxAmount = 2_000_000m, MaxWaiverPercent = 0.05m, EscalateToLabel = "معتمد أعلى" },
            new() { Rank = 2, RoleKey = SystemRoles.SeniorApprover, LevelLabel = "معتمد أول", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", MaxAmount = 5_000_000m, MaxWaiverPercent = 0.10m, EscalateToLabel = "لجنة المخاطر" },
            new() { Rank = 3, RoleKey = SystemRoles.RiskCommittee, LevelLabel = "لجنة المخاطر", SolutionKinds = ["Reschedule", "GracePeriod", "ReducedPayoff", "VoluntarySale"], SolutionKindsLabel = "كل الحلول الودية", EscalateToLabel = "—" },
        ]);
        db.ApprovalLimitPolicies.Add(policy);
        await db.SaveChangesAsync();
        return org;
    }
}
