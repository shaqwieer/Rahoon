using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Security;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Assessment;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Documents;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Providers;
using Rahoon.Api.Modules.Solutions;
using TaskStatus = Rahoon.Api.Modules.Communications.TaskStatus;

namespace Rahoon.Api.Modules.Cases;

public sealed record TransitionRequest(string Action, string? Reason, string? ExpectedStatus);
public sealed record RevealRequest(string Reason);

public static class CaseEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/cases/{reference}").RequirePermission(P.CaseView);
        g.MapGet("", Workspace);
        g.MapGet("/actions", Actions);
        g.MapPost("/transitions", Transition).Idempotent();
        g.MapPost("/parties/{partyId:guid}/reveal", Reveal).RequirePermission(P.PiiReveal);
        g.MapGet("/activity", Activity);
    }

    /// <summary>Header + overview payload for the case workspace (P0 anchor).</summary>
    private static async Task<IResult> Workspace(string reference, CaseAccess access, RahoonDbContext db, RequestContext rc, IClock clock,
        NextActionBuilder nextAction, CaseWorkflow workflow)
    {
        var c = await access.GetAsync(reference, track: false);
        var today = clock.TodayRiyadh;
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == c.OrganizationId);
        var primary = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id && p.IsPrimary);
        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.CaseId == c.Id);
        var manager = c.AssignedManagerId is null ? null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => new { m.User!.FullName }).FirstOrDefaultAsync();
        var owner = await db.OwnerAccesses.AsNoTracking().FirstOrDefaultAsync(o => o.CaseId == c.Id && o.PartyId == (primary != null ? primary.Id : Guid.Empty));
        var valuation = await db.ValuationReports.AsNoTracking().Where(r => r.CaseId == c.Id && r.Status == ValuationStatus.Accepted).OrderByDescending(r => r.ReportDate).FirstOrDefaultAsync();
        var sla = CaseDisplay.Sla(c, today);
        var meta = CaseStatusInfo.Of(c.Status);

        var solutions = await db.Solutions.AsNoTracking().Where(s => s.CaseId == c.Id).OrderByDescending(s => s.VersionNo).ToListAsync();
        var solutionUserIds = solutions.Select(s => s.PreparedByUserId)
            .Concat(solutions.Where(s => s.ReviewedByUserId != null).Select(s => s.ReviewedByUserId!.Value)).Distinct().ToList();
        var solutionUsers = await db.Users.Where(u => solutionUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var pendingApprovals = await db.ApprovalRequests.AsNoTracking().Where(a => a.CaseId == c.Id).ToListAsync();

        var docs = await db.Documents.AsNoTracking().Where(d => d.CaseId == c.Id).ToListAsync();
        var expiring = docs.Where(d => d.ValidUntil is { } v && v >= today && v <= today.AddDays(30)).OrderBy(d => d.ValidUntil)
            .Select(d => new { d.Id, d.Name, days = d.ValidUntil!.Value.DayNumber - today.DayNumber, text = $"{d.Name} · تنتهي خلال {CaseDisplay.Days(d.ValidUntil!.Value.DayNumber - today.DayNumber)}" }).ToList();
        var attentionDocs = docs.Count(d => d.Status is DocumentStatus.Requested or DocumentStatus.Rejected or DocumentStatus.InReview or DocumentStatus.Uploaded) + expiring.Count;

        var tasks = await db.Tasks.AsNoTracking().Where(t => t.CaseId == c.Id && t.Status == TaskStatus.Open).OrderBy(t => t.DueOn)
            .Select(t => new { t.Id, t.Title, t.DueOn, Assignee = db.Users.Where(u => u.Id == t.AssigneeUserId).Select(u => u.FullName).FirstOrDefault() }).ToListAsync();

        var providers = await db.Assignments.AsNoTracking().Where(a => a.CaseId == c.Id)
            .Select(a => new { a.Reference, a.Title, a.Status, a.DeliveredAt, a.AccessExpiresAt, Provider = db.Organizations.Where(o => o.Id == a.ProviderOrganizationId).Select(o => o.NameAr).First() })
            .ToListAsync();

        var activity = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id).OrderByDescending(e => e.Seq).Take(4)
            .Select(e => new { e.Type, e.Title, e.ActorLabel, e.OccurredAt, e.Blocked }).ToListAsync();

        var actions = await workflow.AvailableActionsAsync(c);
        var saleDef = CaseWorkflow.Def("start_voluntary_sale");
        object? saleAction = null;
        if (rc.Has(P.SaleManage) && saleDef.From.Contains(c.Status))
        {
            var reasons = await workflow.EvaluateGuardsAsync(c, saleDef);
            saleAction = new { label = "بدء مسار البيع الطوعي", enabled = reasons.Count == 0, reasons, href = $"/cases/{c.Reference}/sale" };
        }

        decimal? ltv = valuation is null || c.OutstandingAmount is null ? null : Math.Round(c.OutstandingAmount.Value / valuation.MarketValue * 100, 1);
        var latestSolution = solutions.FirstOrDefault();

        return Results.Ok(new
        {
            header = new
            {
                reference = c.Reference, product = c.ProductType, lender = org.NameAr, opened = c.OpenedOn?.ToString("yyyy-MM-dd"),
                title = CaseDisplay.Title(primary?.DisplayName ?? "—", property?.ShortLabel),
                titleShort = CaseDisplay.Title(primary?.DisplayName ?? "—", property is null ? null : $"{property.Type.Split('·')[0].Trim().Split(' ')[0]}، {property.District?.Replace("حي ", "")}"),
                status = meta.Key, statusLabel = meta.LabelAr, stage = CaseStatusInfo.StageOf(c), stageNames = CaseStatusInfo.StageNames,
                slaTone = sla.Tone, slaText = sla.DueOn is null ? sla.Text : $"مهلة المرحلة: {sla.Text} · {sla.DueOn}", slaShort = sla.Text,
                manager = manager?.FullName, ownerVerified = owner?.IdentityVerifiedAt != null,
                ownerAccessLabel = owner?.InvitationStatus switch
                {
                    OwnerInvitationStatus.Accepted => "المالك متحقق · دعوة مقبولة",
                    OwnerInvitationStatus.Sent => "دعوة مرسلة · بانتظار المالك",
                    _ => "لم تُرسل دعوة للمالك بعد",
                },
                pauseReason = c.PauseReason,
                version = c.Version,
            },
            tabs = new
            {
                documentsBadge = attentionDocs > 0 ? attentionDocs.ToString() : null,
                solutionsBadge = latestSolution is null ? null : $"v{latestSolution.VersionNo}",
                commsBadge = tasks.Count > 0 ? tasks.Count.ToString() : null,
                showSale = c.Status == CaseStatus.VoluntarySale || await db.ConsentRecords.AnyAsync(x => x.CaseId == c.Id && x.Kind == "sale_consent"),
                showReferral = c.Status is CaseStatus.JudicialReferral or CaseStatus.ExternalJudicialSale || await db.Referrals.AnyAsync(r => r.CaseId == c.Id),
                showClosure = c.Status is CaseStatus.AwaitingReconciliation or CaseStatus.Closed,
            },
            nextAction = await nextAction.BuildAsync(c),
            figures = new object[]
            {
                new { key = "outstanding", label = "المديونية القائمة", value = c.OutstandingAmount, unit = "ر.س", icon = "database",
                    source = c.OutstandingAsOf is null ? null : $"{c.OutstandingSource} · {c.OutstandingAsOf.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}" },
                new { key = "arrears", label = "المتأخرات", value = c.ArrearsAmount, unit = "ر.س", icon = "event_busy",
                    source = c.ArrearsInstallments is > 0 ? $"{c.ArrearsInstallments} أقساط · منذ {c.ArrearsSince:yyyy-MM}" : "لا متأخرات" },
                new { key = "market_value", label = "القيمة السوقية", value = valuation?.MarketValue, unit = "ر.س", icon = "query_stats",
                    source = valuation is null ? "لا يوجد تقييم معتمد" : $"تقييم {valuation.ValuerName.Replace("مكتب تقييم معتمد ", "مكتب ")} · {valuation.ReportDate:yyyy-MM-dd}" },
                new { key = "ltv", label = "التمويل إلى القيمة", value = ltv, unit = "%", icon = "percent", source = ltv is null ? "يتطلب تقييماً معتمداً" : "محسوب من القيمتين أعلاه" },
            },
            solutions = solutions.Select(s =>
            {
                var appr = pendingApprovals.Where(a => a.SubjectId == s.Id).OrderByDescending(a => a.SubmittedAt).FirstOrDefault();
                var (stage, stateText, tone) = s.Status switch
                {
                    SolutionStatus.Draft => ("إعداد", "مسودة", "neutral"),
                    SolutionStatus.InReview => ("موافقة", "لم تُرسل بعد", "neutral"),
                    SolutionStatus.PendingApproval => ("موافقة", "بانتظار المعتمد", "warn"),
                    SolutionStatus.Approved => ("موافقة", "معتمد", "ok"),
                    SolutionStatus.Returned => ("مراجعة", "أُعيد", "err"),
                    SolutionStatus.Rejected => ("موافقة", "مرفوض", "err"),
                    SolutionStatus.Offered => ("العرض", "أُرسل للمالك", "info"),
                    SolutionStatus.Accepted => ("العرض", "قبله المالك", "ok"),
                    SolutionStatus.Declined => ("العرض", "رفضه المالك", "err"),
                    SolutionStatus.Countered => ("العرض", "عرض مقابل", "warn"),
                    SolutionStatus.Superseded => ("—", "حل محله إصدار أحدث", "neutral"),
                    _ => ("—", s.Status.ToString(), "neutral"),
                };
                return new
                {
                    version = s.VersionNo, kind = s.Kind.ToString(), status = s.Status.ToString(), termMonths = s.TermMonths,
                    installment = s.InstallmentAmount, waiver = s.WaiverAmount, preparedBy = solutionUsers.GetValueOrDefault(s.PreparedByUserId),
                    preparedAt = s.PreparedAt, returnReason = s.ReturnReason, returnedBy = s.ReviewedByUserId is { } r ? solutionUsers.GetValueOrDefault(r) : null,
                    stage, stateText, tone, approvalDue = appr?.DueOn,
                };
            }),
            documents = new { total = docs.Count, verified = docs.Count(d => d.Status == DocumentStatus.Verified && !(d.ValidUntil is { } v && v <= today.AddDays(30))), expiring,
                needsAttention = docs.Where(d => d.Status is DocumentStatus.Rejected or DocumentStatus.Requested).Select(d => new { d.Id, d.Name, status = d.Status.ToString() }) },
            tasks = tasks.Select(t => new { t.Id, t.Title, assignee = t.Assignee is null ? null : CaseDisplay.ShortName(t.Assignee), due = t.DueOn,
                dueText = t.DueOn is { } d ? (d.DayNumber - today.DayNumber is var n && n < 0 ? CaseDisplay.LateDays(-n) : n <= 2 ? (n == 0 ? "اليوم" : CaseDisplay.Days(n)) : d.ToString("yyyy-MM-dd")) : null }),
            parties = new
            {
                primary = primary is null ? null : new
                {
                    primary.Id, name = primary.DisplayName, initials = Identity.AuthEndpoints.Initials(primary.FullName), role = PartyRoleLabel(primary.Role),
                    nationalIdMasked = primary.NationalIdMasked, language = primary.PreferredLanguage == "ar" ? "يفضّل العربية" : "يفضّل الإنجليزية",
                    canReveal = rc.Has(P.PiiReveal),
                },
                providers = providers.Select(p => new
                {
                    p.Provider, p.Reference, p.Title,
                    text = p.DeliveredAt is null ? $"التسليم المتوقع {p.AccessExpiresAt:yyyy-MM-dd}"
                        : $"سلّم {p.DeliveredAt.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd} · " + (p.AccessExpiresAt < clock.UtcNow ? $"الوصول انتهى {p.AccessExpiresAt!.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}" : $"وصول قراءة حتى {p.AccessExpiresAt!.Value.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd}"),
                }),
            },
            activity = activity.Select(a => new { a.Type, a.Title, meta = $"{a.ActorLabel} · {a.OccurredAt.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm}", a.Blocked }),
            sensitive = new
            {
                actions = actions.Where(a => a.Key is "pause" or "resume"),
                canRequestCancel = rc.Has(P.CaseCancel) && !CaseStatusInfo.IsTerminal(c.Status),
                sale = saleAction,
                referralNote = rc.Has(P.ReferralInitiate) ? null : "الإحالة القضائية لا تظهر لدورك؛ تبدأها الإدارة القانونية بقرار منفصل.",
                canInitiateReferral = rc.Has(P.ReferralInitiate) && CaseStatusInfo.SolutionStates.Contains(c.Status),
            },
            actions = actions.Where(a => a.Key is not ("pause" or "resume")),
        });
    }

    public static string PartyRoleLabel(PartyRole r) => r switch
    {
        PartyRole.OwnerBorrower => "المالك والمقترض",
        PartyRole.CoBorrower => "مقترض مشارك",
        PartyRole.Guarantor => "كفيل",
        PartyRole.Agent => "وكيل",
        PartyRole.LegalRepresentative => "ممثل نظامي",
        PartyRole.Occupant => "شاغل العقار",
        PartyRole.InformalRepresentative => "ممثل غير رسمي",
        _ => r.ToString(),
    };

    private static async Task<IResult> Actions(string reference, CaseAccess access, CaseWorkflow workflow)
    {
        var c = await access.GetAsync(reference, track: false);
        return Results.Ok(await workflow.AvailableActionsAsync(c));
    }

    /// <summary>Generic manual transitions (start verification, pause/resume, …). Domain transitions have their own endpoints.</summary>
    private static async Task<IResult> Transition(string reference, TransitionRequest req, CaseAccess access, CaseWorkflow workflow, RahoonDbContext db)
    {
        var def = CaseWorkflow.Transitions.FirstOrDefault(t => t.Key == req.Action && t.Manual) ?? throw new DomainException("unknown_action", "إجراء غير معروف.", 400);
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = await access.GetAsync(reference);
        var expected = req.ExpectedStatus is null ? null : CaseStatusInfo.Parse(req.ExpectedStatus);
        await workflow.TransitionAsync(c, def.Key, req.Reason?.Trim(), expected);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return Results.Ok(new { status = CaseStatusInfo.Key(c.Status), label = CaseStatusInfo.Of(c.Status).LabelAr });
    }

    /// <summary>Audited 60-second reveal of a party's identity number and phone (A-10).</summary>
    private static async Task<IResult> Reveal(string reference, Guid partyId, RevealRequest req, CaseAccess access, RahoonDbContext db,
        PiiProtector pii, RequestContext rc, IClock clock, AuditLog audit)
    {
        if (string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Trim().Length < 5) Validate.Throw("reason", "اكتب سبب الكشف (5 أحرف على الأقل).");
        var c = await access.GetAsync(reference, track: false);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Id == partyId && p.CaseId == c.Id) ?? throw new NotFoundException();
        var now = clock.UtcNow;
        db.PiiRevealLogs.Add(new PiiRevealLog { OrganizationId = c.OrganizationId, CaseId = c.Id, PartyId = party.Id, UserId = rc.UserId, Field = "national_id,phone", Reason = req.Reason.Trim(), RevealedAt = now, ExpiresAt = now.AddSeconds(60) });
        await audit.RecordAsync(new AuditEntry("pii.reveal", "كشف رقم الهوية والجوال كاملاً", c.Id, c.Reference, Reason: req.Reason.Trim(), Detail: "مدة الكشف 60 ثانية", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        return Results.Ok(new
        {
            nationalId = party.NationalIdEnc is null ? null : pii.Unprotect(party.NationalIdEnc),
            phone = party.PhoneEnc is null ? null : pii.Unprotect(party.PhoneEnc),
            fullName = party.FullName,
            expiresAt = now.AddSeconds(60),
        });
    }

    private static async Task<IResult> Activity(string reference, int? take, CaseAccess access, RahoonDbContext db)
    {
        var c = await access.GetAsync(reference, track: false);
        var events = await db.AuditEvents.AsNoTracking().Where(e => e.CaseId == c.Id).OrderByDescending(e => e.Seq).Take(Math.Clamp(take ?? 50, 1, 500))
            .Select(e => new { e.Seq, e.Type, e.Title, e.ActorLabel, e.ActorRole, e.Reason, e.Detail, e.Blocked, e.OccurredAt, e.FromState, e.ToState, e.Evidence, e.Hash, e.PrevHash })
            .ToListAsync();
        return Results.Ok(events);
    }
}
