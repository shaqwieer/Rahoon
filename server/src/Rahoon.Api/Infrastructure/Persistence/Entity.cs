namespace Rahoon.Api.Infrastructure.Persistence;

/// <summary>Base for all persisted entities. Ids are time-ordered UUIDv7.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

/// <summary>
/// Data owned by one tenant organization. Every read is filtered by the
/// request's data-organization set and every write is checked by
/// <see cref="TenantWriteGuardInterceptor"/>. Never trust a client-supplied id.
/// </summary>
public interface IOrgOwned
{
    Guid OrganizationId { get; set; }
}

public interface IHasTimestamps
{
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Optimistic concurrency via PostgreSQL xmin.</summary>
public interface IConcurrencyVersioned
{
    uint Version { get; set; }
}

public abstract class OrgEntity : Entity, IOrgOwned, IHasTimestamps
{
    public Guid OrganizationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
