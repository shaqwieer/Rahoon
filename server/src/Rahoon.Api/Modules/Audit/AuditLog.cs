using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.Audit;

public sealed record AuditEntry(
    string Type,
    string Title,
    Guid? CaseId = null,
    string? CaseReference = null,
    string? FromState = null,
    string? ToState = null,
    string? Reason = null,
    string? Detail = null,
    bool Blocked = false,
    IReadOnlyList<string>? Evidence = null,
    object? Data = null,
    Guid? OrganizationId = null);

/// <summary>
/// Append-only, hash-chained audit trail. Each event hashes its content with the
/// previous event's hash within the same organization chain; a transaction-scoped
/// advisory lock serializes appends so the chain cannot fork.
/// </summary>
public sealed class AuditLog(RahoonDbContext db, RequestContext rc, IClock clock, IServiceScopeFactory scopes)
{
    public const string Genesis = "sha256:genesis";

    /// <summary>Adds the event to the current unit of work (committed with the business change).</summary>
    public async Task RecordAsync(AuditEntry e)
    {
        var evt = Build(e, rc);
        await ChainAsync(db, evt);
        db.AuditEvents.Add(evt);
    }

    /// <summary>
    /// Records a refused action in its own transaction so it survives the rollback of the
    /// failed business operation ("المحجوب يُسجل أيضاً").
    /// </summary>
    public async Task RecordBlockedAsync(AuditEntry e)
    {
        var evt = Build(e with { Blocked = true }, rc);
        using var scope = scopes.CreateScope();
        var other = scope.ServiceProvider.GetRequiredService<RahoonDbContext>();
        using (other.Request.BeginSystemScope())
        {
            await using var tx = await other.Database.BeginTransactionAsync();
            // Callers must record blocked attempts before appending to the chain in their own
            // transaction; the timeout turns any ordering mistake into an error, never a hang.
            await other.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '5s'");
            await ChainAsync(other, evt);
            other.AuditEvents.Add(evt);
            await other.SaveChangesAsync();
            await tx.CommitAsync();
        }
    }

    private AuditEvent Build(AuditEntry e, RequestContext ctx) => new()
    {
        OrganizationId = e.OrganizationId ?? ctx.OrganizationId,
        CaseId = e.CaseId,
        CaseReference = e.CaseReference,
        Type = e.Type,
        Title = e.Title,
        FromState = e.FromState,
        ToState = e.ToState,
        ActorType = ctx.IsAuthenticated ? ctx.ActorType : "system",
        ActorUserId = ctx.IsAuthenticated ? ctx.UserId : null,
        ActorLabel = ctx.IsAuthenticated ? ctx.UserName : "النظام",
        ActorRole = ctx.IsOwner ? "المالك" : ctx.PrimaryRoleNameAr,
        Reason = e.Reason,
        Detail = e.Detail,
        Blocked = e.Blocked,
        Evidence = e.Evidence?.ToList() ?? [],
        DataJson = e.Data is null ? null : JsonSerializer.Serialize(e.Data),
        IpMasked = ctx.IpMasked,
        // PostgreSQL keeps microseconds; truncate so the hash verifies after a round-trip.
        OccurredAt = new DateTimeOffset(clock.UtcNow.UtcTicks / 10 * 10, TimeSpan.Zero),
        PrevHash = "",
        Hash = "",
    };

    private static async Task ChainAsync(RahoonDbContext context, AuditEvent evt)
    {
        var chainKey = evt.OrganizationId?.ToString() ?? "platform";
        if (context.Database.CurrentTransaction is not null)
            await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({chainKey}))");

        // Include events already staged in this unit of work (several events per transaction).
        var staged = context.ChangeTracker.Entries<AuditEvent>()
            .Where(x => x.State == EntityState.Added && (x.Entity.OrganizationId?.ToString() ?? "platform") == chainKey)
            .Select(x => x.Entity).LastOrDefault();

        var prev = staged?.Hash ?? await context.AuditEvents.IgnoreQueryFilters()
            .Where(a => a.OrganizationId == evt.OrganizationId)
            .OrderByDescending(a => a.Seq).Select(a => a.Hash).FirstOrDefaultAsync() ?? Genesis;

        evt.PrevHash = prev;
        evt.Hash = ComputeHash(evt);
    }

    public static string ComputeHash(AuditEvent e)
    {
        var canonical = string.Join('|', e.PrevHash, e.Id, e.OrganizationId, e.CaseId, e.Type, e.Title, e.FromState, e.ToState,
            e.ActorUserId, e.Reason, e.Detail, e.Blocked, string.Join(',', e.Evidence), e.DataJson,
            e.OccurredAt.UtcDateTime.ToString("O"));
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Recomputes the chain for an organization; returns the first broken sequence number, if any.</summary>
    public static async Task<long?> VerifyChainAsync(RahoonDbContext context, Guid? orgId)
    {
        var prev = Genesis;
        await foreach (var e in context.AuditEvents.IgnoreQueryFilters().Where(a => a.OrganizationId == orgId).OrderBy(a => a.Seq).AsAsyncEnumerable())
        {
            if (e.PrevHash != prev || ComputeHash(e) != e.Hash) return e.Seq;
            prev = e.Hash;
        }
        return null;
    }
}
