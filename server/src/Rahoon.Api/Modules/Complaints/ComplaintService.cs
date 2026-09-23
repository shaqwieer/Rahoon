using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Audit;
using Rahoon.Api.Modules.Cases;
using Rahoon.Api.Modules.Communications;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Solutions;

namespace Rahoon.Api.Modules.Complaints;

public sealed record ComplaintDecisionRequest(string Decision, string Response, bool ExtendOfferDeadline, int? ExtensionDays);
public sealed record ComplaintDraftRequest(string? Decision, string? Response);
public sealed record FindingRequest(string Severity, string Text);
public sealed record LenderComplaintRequest(string CaseReference, string Type, string Subject, string Body, string Via);

/// <summary>
/// Complaints and objections (L23/D13). Reviewed by Compliance, independent of the case team.
/// While open: referral and cancellation are blocked (transition guards) and the owner-response SLA is paused.
/// </summary>
public sealed class ComplaintService(RahoonDbContext db, RequestContext rc, IClock clock, AuditLog audit, Notifier notifier)
{
    public async Task<Complaint> SubmitAsync(Case c, ComplaintType type, string subject, string body, string via, Guid? submittedBy, string submittedByLabel)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var year = clock.TodayRiyadh.Year;
        var count = await db.Complaints.IgnoreQueryFilters().CountAsync(x => x.Reference.StartsWith($"CMP-{year}-"));
        var reviewer = await IndependentReviewerAsync(c);
        var complaint = new Complaint
        {
            OrganizationId = c.OrganizationId, Reference = $"CMP-{year}-{count + 142:D4}", CaseId = c.Id, Type = type, Subject = subject.Trim(), Body = body,
            SubmittedVia = via, SubmittedByUserId = submittedBy, SubmittedByLabel = submittedByLabel, SubmittedAt = clock.UtcNow,
            Status = reviewer is null ? ComplaintStatus.Received : ComplaintStatus.InReview, ReviewerUserId = reviewer,
            DueOn = BusinessDays.Add(clock.TodayRiyadh, 5),
        };
        db.Complaints.Add(complaint);

        // Pause the owner-response clock while the complaint is open (A06: «توقف عند شكوى مفتوحة»).
        if (c.Status is CaseStatus.AwaitingCustomer or CaseStatus.Negotiation && c.SlaPausedAt is null)
        {
            c.SlaPausedAt = clock.UtcNow;
            c.SlaPausedReason = $"شكوى مفتوحة {complaint.Reference}";
        }
        if (reviewer is { } r)
        {
            notifier.Notify(r, c.OrganizationId, "complaint", $"شكوى جديدة {complaint.Reference}", subject, $"/complaints/{complaint.Reference}", c.Id, "warn");
            notifier.Task(c.OrganizationId, c.Id, $"مراجعة الشكوى {complaint.Reference}", r, complaint.DueOn, "complaint", $"/complaints/{complaint.Reference}", submittedBy ?? rc.UserId);
        }
        // The case team sees only a tag, never the complaint text.
        if (c.AssignedManagerId is { } mid && await db.Memberships.Where(m => m.Id == mid).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync() is { } mgr)
            notifier.Notify(mgr, c.OrganizationId, "complaint", $"شكوى مفتوحة على الحالة {c.Reference}", "تراجعها جهة مستقلة. تُحجب الإحالة والإلغاء حتى المعالجة.", $"/cases/{c.Reference}", c.Id, "warn");

        await audit.RecordAsync(new AuditEntry("complaint.received", $"استلام {(type == ComplaintType.Objection ? "اعتراض" : "شكوى")} {complaint.Reference}", c.Id, c.Reference,
            Detail: $"قُدمت عبر {ViaLabel(via)} · المهلة {complaint.DueOn:yyyy-MM-dd}", OrganizationId: c.OrganizationId));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return complaint;
    }

    public static string ViaLabel(string via) => via switch
    {
        "owner_portal" => "بوابة المالك", "phone" => "الهاتف", "email" => "البريد", "branch" => "الفرع", _ => via,
    };

    /// <summary>A compliance reviewer who is not the case manager and has no task on the case.</summary>
    private async Task<Guid?> IndependentReviewerAsync(Case c)
    {
        var teamUsers = await db.Tasks.Where(t => t.CaseId == c.Id && t.AssigneeUserId != null).Select(t => t.AssigneeUserId!.Value).Distinct().ToListAsync();
        var manager = c.AssignedManagerId is null ? (Guid?)null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
        var candidates = await db.Memberships.Where(m => m.OrganizationId == c.OrganizationId && m.Status == MembershipStatus.Active
                && m.Roles.Any(r => r.Role!.Permissions.Any(p => p.PermissionKey == P.ComplaintHandle)))
            .Select(m => m.UserId).ToListAsync();
        return candidates.FirstOrDefault(u => u != manager && !teamUsers.Contains(u)) is var pick && pick != Guid.Empty ? pick : null;
    }

    public async Task DecideAsync(Complaint complaint, Case c, ComplaintDecisionRequest req)
    {
        var decision = req.Decision switch
        {
            "accepted" => ComplaintDecision.Accepted,
            "partially_accepted" => ComplaintDecision.PartiallyAccepted,
            "rejected" => ComplaintDecision.Rejected,
            _ => throw new ValidationFailedException(new Dictionary<string, string[]> { ["decision"] = ["اختر القرار."] }),
        };
        var v = new Validator();
        v.Require(!string.IsNullOrWhiteSpace(req.Response) && req.Response.Trim().Length >= 30, "response", "اكتب رداً واضحاً للمالك (30 حرفاً على الأقل).");
        v.Require(req.Response?.Contains("الجهات المختصة") == true || req.Response?.Contains("إعادة النظر") == true, "response",
            "يجب أن يذكر الرد حق طلب إعادة النظر أو التقدم للجهات المختصة.");
        v.ThrowIfInvalid();
        if (!complaint.IsOpen) throw new ConflictException("complaint_closed", "أُغلقت هذه الشكوى مسبقاً.");
        if (complaint.ReviewerUserId != rc.UserId) throw new ForbiddenException("المراجعة مسندة لمراجِع آخر.");

        complaint.Decision = decision;
        complaint.ResponseText = req.Response.Trim();
        complaint.RespondedAt = clock.UtcNow;
        complaint.Status = ComplaintStatus.Resolved;

        if (req.ExtendOfferDeadline)
        {
            var offer = await db.Offers.Where(o => o.CaseId == c.Id && (o.Status == OfferStatus.Sent)).OrderByDescending(o => o.SentAt).FirstOrDefaultAsync();
            if (offer is not null)
            {
                var days = Math.Clamp(req.ExtensionDays ?? 7, 1, 30);
                var mgr = c.AssignedManagerId is null ? (Guid?)null : await db.Memberships.Where(m => m.Id == c.AssignedManagerId).Select(m => (Guid?)m.UserId).FirstOrDefaultAsync();
                db.ApprovalRequests.Add(new ApprovalRequest
                {
                    OrganizationId = c.OrganizationId, CaseId = c.Id, Subject = ApprovalSubject.OfferExtension, SubjectId = offer.Id, SubjectVersionNo = days,
                    Title = $"تمديد مهلة العرض {days} أيام (من الشكوى {complaint.Reference})", PreparedByUserId = rc.UserId, SubmittedByUserId = rc.UserId,
                    SubmittedAt = clock.UtcNow, SubmitterNote = $"طلب من مراجعة الشكوى {complaint.Reference}", SubmitterAttested = true,
                    AssignedApproverUserId = mgr, RequiredTier = SystemRoles.CaseManager, DueOn = BusinessDays.Add(clock.TodayRiyadh, 1),
                });
                if (mgr is { } m) notifier.Notify(m, c.OrganizationId, "approval", $"طلب تمديد مهلة العرض · {c.Reference}", $"من مراجعة الشكوى {complaint.Reference}", $"/cases/{c.Reference}/solutions/negotiation", c.Id, "warn");
            }
        }

        // Resume the owner-response clock when no other complaint remains open.
        var otherOpen = await db.Complaints.AnyAsync(x => x.CaseId == c.Id && x.Id != complaint.Id && x.Status != ComplaintStatus.Resolved && x.Status != ComplaintStatus.Closed);
        if (!otherOpen && c.Status != CaseStatus.Paused && c.SlaPausedAt is not null)
        {
            var pausedDays = (int)(clock.UtcNow - c.SlaPausedAt.Value).TotalDays;
            if (c.StageDueOn is { } due) c.StageDueOn = due.AddDays(pausedDays);
            c.SlaPausedAt = null;
            c.SlaPausedReason = null;
        }

        var owner = await db.OwnerAccesses.Where(o => o.CaseId == c.Id && o.UserId != null).Select(o => o.UserId).FirstOrDefaultAsync();
        if (owner is { } ou) notifier.Notify(ou, c.OrganizationId, "complaint", $"وصلك الرد على {complaint.Reference}", complaint.ResponseText, "/owner/help", c.Id);
        db.Messages.Add(new CaseMessage
        {
            OrganizationId = c.OrganizationId, CaseId = c.Id, Channel = MessageChannel.Portal, AuthorType = "lender", AuthorUserId = rc.UserId,
            AuthorLabel = "مراجعة الشكاوى", Body = $"ردنا على {complaint.Reference}: {complaint.ResponseText}", At = clock.UtcNow,
        });
        await audit.RecordAsync(new AuditEntry("complaint.decided", $"الرد على {complaint.Reference} وإغلاق المراجعة", c.Id, c.Reference,
            Detail: decision switch { ComplaintDecision.Accepted => "مقبولة", ComplaintDecision.PartiallyAccepted => "مقبولة جزئياً", _ => "غير مقبولة" }, OrganizationId: c.OrganizationId));
    }
}
