using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Infrastructure.Time;
using Rahoon.Api.Modules.Administration;
using Rahoon.Api.Modules.Identity;

namespace Rahoon.Api.Infrastructure.Http;

/// <summary>Server-side authorization for minimal-API endpoints. Browser checks are cosmetic only.</summary>
public static class EndpointAccess
{
    /// <summary>Authenticated, MFA-complete session (any scope).</summary>
    public static TBuilder RequireSession<TBuilder>(this TBuilder b) where TBuilder : IEndpointConventionBuilder =>
        b.AddEndpointFilter(async (ctx, next) =>
        {
            var rc = ctx.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            if (!rc.IsAuthenticated || rc.Stage != SessionStage.Active)
                return Results.Problem(title: "انتهت الجلسة. سجّل الدخول مرة أخرى.", statusCode: 401, extensions: new Dictionary<string, object?> { ["code"] = "unauthenticated" });
            return await next(ctx);
        });

    /// <summary>Staff of an organization of the given kind with an active membership.</summary>
    public static TBuilder RequireOrg<TBuilder>(this TBuilder b, OrganizationKind kind) where TBuilder : IEndpointConventionBuilder =>
        b.RequireSession().AddEndpointFilter(async (ctx, next) =>
        {
            var rc = ctx.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            if (rc.Scope != SessionScope.Organization || rc.OrganizationKind != kind) throw new ForbiddenException();
            return await next(ctx);
        });

    /// <summary>Caller must hold every listed permission via their membership roles.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder b, params string[] permissions) where TBuilder : IEndpointConventionBuilder =>
        b.RequireSession().AddEndpointFilter(async (ctx, next) =>
        {
            var rc = ctx.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            if (rc.Scope != SessionScope.Organization || permissions.Any(p => !rc.Has(p))) throw new ForbiddenException();
            return await next(ctx);
        });

    /// <summary>Caller must hold at least one of the listed permissions.</summary>
    public static TBuilder RequireAnyPermission<TBuilder>(this TBuilder b, params string[] permissions) where TBuilder : IEndpointConventionBuilder =>
        b.RequireSession().AddEndpointFilter(async (ctx, next) =>
        {
            var rc = ctx.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            if (rc.Scope != SessionScope.Organization || !permissions.Any(rc.Has)) throw new ForbiddenException();
            return await next(ctx);
        });

    public static TBuilder RequireOwner<TBuilder>(this TBuilder b) where TBuilder : IEndpointConventionBuilder =>
        b.RequireSession().AddEndpointFilter(async (ctx, next) =>
        {
            var rc = ctx.HttpContext.RequestServices.GetRequiredService<RequestContext>();
            if (!rc.IsOwner || rc.OwnerCaseId is null) throw new ForbiddenException();
            return await next(ctx);
        });

    /// <summary>
    /// Requires a recent MFA step-up (sensitive decisions: approvals, cancellation, referral, closure).
    /// </summary>
    public static void EnsureStepUp(RequestContext rc, IClock clock)
    {
        if (rc.StepUpUntil is not { } until || until < clock.UtcNow)
            throw new DomainException("step_up_required", "هذا الإجراء يتطلب إعادة إدخال رمز التحقق.", StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Replays the stored response when the same Idempotency-Key is sent again by the same user,
    /// and rejects reuse of a key with a different payload. Protects every state-changing submit
    /// from double clicks and network retries.
    /// </summary>
    public static TBuilder Idempotent<TBuilder>(this TBuilder b) where TBuilder : IEndpointConventionBuilder =>
        b.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var key = http.Request.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
                return Results.Problem(title: "مفتاح منع التكرار مطلوب.", statusCode: 400, extensions: new Dictionary<string, object?> { ["code"] = "idempotency_key_required" });

            var rc = http.RequestServices.GetRequiredService<RequestContext>();
            var db = http.RequestServices.GetRequiredService<RahoonDbContext>();
            var clock = http.RequestServices.GetRequiredService<IClock>();
            var endpoint = $"{http.Request.Method} {http.Request.Path}";
            var bodyHash = HashArguments(ctx.Arguments);

            var existing = await db.IdempotencyRecords.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == rc.UserId && r.Key == key);
            if (existing is not null)
            {
                if (existing.Endpoint != endpoint || existing.RequestHash != bodyHash)
                    return Results.Problem(title: "أُعيد استخدام مفتاح منع التكرار لطلب مختلف.", statusCode: 422, extensions: new Dictionary<string, object?> { ["code"] = "idempotency_mismatch" });
                http.Response.Headers["Idempotent-Replay"] = "true";
                return existing.ResponseJson is null
                    ? Results.StatusCode(existing.StatusCode)
                    : Results.Content(existing.ResponseJson, "application/json", Encoding.UTF8, existing.StatusCode);
            }

            var result = await next(ctx);

            var (status, json) = result switch
            {
                IStatusCodeHttpResult { StatusCode: { } sc } s and IValueHttpResult v => (sc, JsonSerializer.Serialize(v.Value, JsonOptions.Web)),
                IValueHttpResult v => (200, JsonSerializer.Serialize(v.Value, JsonOptions.Web)),
                IStatusCodeHttpResult { StatusCode: { } sc } => (sc, (string?)null),
                _ => (200, (string?)null),
            };
            if (status < 400)
            {
                db.ChangeTracker.Clear();
                db.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = key, UserId = rc.UserId, Endpoint = endpoint, RequestHash = bodyHash,
                    StatusCode = status, ResponseJson = json, CreatedAt = clock.UtcNow,
                });
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException) { /* concurrent duplicate: the first writer wins; response already produced */ }
            }
            return result;
        });

    private static string HashArguments(IList<object?> args)
    {
        var relevant = args.Where(a => a is not null && a is not HttpContext && a is not RequestContext && a is not RahoonDbContext
                                       && a.GetType().Namespace?.StartsWith("Rahoon.Api.Modules") == true || a is string or Guid);
        var json = JsonSerializer.Serialize(relevant.ToList(), JsonOptions.Web);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
