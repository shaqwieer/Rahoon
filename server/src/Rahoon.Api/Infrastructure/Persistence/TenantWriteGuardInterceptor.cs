using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Infrastructure.Persistence;

public sealed class TenantViolationException(string message) : Exception(message);

/// <summary>
/// Stamps timestamps and refuses to persist tenant-owned rows outside the caller's
/// data organizations — a second line of defense behind the query filters.
/// </summary>
public sealed class TenantWriteGuardInterceptor(IClock clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is not RahoonDbContext db) return;
        var rc = db.Request;
        var now = clock.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.Entity is IHasTimestamps ts)
            {
                if (entry.State == EntityState.Added)
                {
                    if (ts.CreatedAt == default) ts.CreatedAt = now;
                    ts.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    ts.UpdatedAt = now;
                }
            }

            if (entry.Entity is not IOrgOwned owned || rc.SystemBypass) continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            if (entry.State == EntityState.Added && owned.OrganizationId == Guid.Empty && rc.OrganizationId is { } org)
                owned.OrganizationId = org;

            if (entry.Entity is IApplicantOwned applicantRow && entry.State == EntityState.Modified
                && entry.Property(nameof(IApplicantOwned.ApplicantUserId)).IsModified)
                throw new TenantViolationException("Changing a row's applicant is not permitted.");

            if (rc.IsIndividual)
            {
                // Individuals hold no tenant data set: they may write only their own applicant-owned rows,
                // and only inside an operator organization (checked by the caller that sets OrganizationId).
                if (entry.Entity is not IApplicantOwned mine || mine.ApplicantUserId != rc.UserId || mine.OrganizationId == Guid.Empty)
                    throw new TenantViolationException($"Write to {entry.Metadata.ClrType.Name} outside the individual's own requests was blocked.");
                if (entry.State == EntityState.Deleted)
                    throw new TenantViolationException("Individuals cannot delete request records.");
                continue;
            }

            if (!rc.DataOrganizationIds.Contains(owned.OrganizationId))
                throw new TenantViolationException($"Write to {entry.Metadata.ClrType.Name} outside the caller's organizations was blocked.");

            if (entry.State == EntityState.Modified && entry.Property(nameof(IOrgOwned.OrganizationId)).IsModified)
                throw new TenantViolationException("Changing a row's organization is not permitted.");
        }
    }
}
