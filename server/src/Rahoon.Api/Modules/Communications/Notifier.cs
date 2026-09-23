using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Communications;

/// <summary>In-app notifications (S08). Added to the caller's unit of work.</summary>
public sealed class Notifier(RahoonDbContext db, IClock clock)
{
    public void Notify(Guid userId, Guid? orgId, string category, string title, string? body = null, string? link = null, Guid? caseId = null, string tone = "info") =>
        db.Notifications.Add(new Notification
        {
            UserId = userId, OrganizationId = orgId, CaseId = caseId, Category = category, Title = title, Body = body, Link = link, Tone = tone,
            CreatedAt = clock.UtcNow,
        });

    public void Task(Guid orgId, Guid? caseId, string title, Guid? assignee, DateOnly? due, string kind, string? link, Guid createdBy) =>
        db.Tasks.Add(new CaseTask
        {
            OrganizationId = orgId, CaseId = caseId, Title = title, AssigneeUserId = assignee, DueOn = due, Kind = kind, Link = link, CreatedByUserId = createdBy,
        });
}
