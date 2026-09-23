using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Infrastructure.Http;

/// <summary>Business rule refused the request. Reasons are shown to the user verbatim (Arabic).</summary>
public class DomainException(string code, string message, int status = StatusCodes.Status422UnprocessableEntity, IReadOnlyList<string>? reasons = null)
    : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public IReadOnlyList<string> Reasons { get; } = reasons ?? [];
}

public sealed class ForbiddenException(string? message = null)
    : DomainException("forbidden", message ?? "لا تملك صلاحية لهذا الإجراء.", StatusCodes.Status403Forbidden);

/// <summary>Same response for "does not exist" and "not yours" — never leak existence across tenants.</summary>
public sealed class NotFoundException()
    : DomainException("not_found", "العنصر غير موجود أو لا تملك صلاحية الوصول إليه.", StatusCodes.Status404NotFound);

public sealed class ConflictException(string code, string message) : DomainException(code, message, StatusCodes.Status409Conflict);

public sealed class ValidationFailedException(IDictionary<string, string[]> errors, string? message = null)
    : DomainException("validation", message ?? "يوجد أخطاء في البيانات المدخلة.", StatusCodes.Status400BadRequest)
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}

public static class Validate
{
    public static void Throw(string field, string message) => throw new ValidationFailedException(new Dictionary<string, string[]> { [field] = [message] });
}

public sealed class Validator
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public IReadOnlyDictionary<string, string[]> Errors => _errors.ToDictionary(k => k.Key, v => v.Value.ToArray());

    public Validator Require(bool ok, string field, string message)
    {
        if (!ok)
        {
            if (!_errors.TryGetValue(field, out var list)) _errors[field] = list = [];
            list.Add(message);
        }
        return this;
    }

    public void ThrowIfInvalid(string? summary = null)
    {
        if (_errors.Count == 0) return;
        var msg = summary ?? (_errors.Count == 1 ? "يوجد خطأ واحد قبل المتابعة." : _errors.Count == 2 ? "يوجد خطآن قبل المتابعة." : $"يوجد {_errors.Count} أخطاء قبل المتابعة.");
        throw new ValidationFailedException(_errors.ToDictionary(k => k.Key, v => v.Value.ToArray()), msg);
    }
}

public sealed class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        ProblemDetails problem;
        switch (ex)
        {
            case ValidationFailedException v:
                problem = Build(v.Status, v.Code, v.Message);
                problem.Extensions["errors"] = v.Errors;
                break;
            case DomainException d:
                problem = Build(d.Status, d.Code, d.Message);
                if (d.Reasons.Count > 0) problem.Extensions["reasons"] = d.Reasons;
                break;
            case DbUpdateConcurrencyException:
                problem = Build(StatusCodes.Status409Conflict, "concurrency", "تغيّرت البيانات منذ فتحها من مستخدم آخر. حدّث الصفحة ثم أعد المحاولة.");
                break;
            case TenantViolationException tv:
                logger.LogWarning(tv, "Tenant write guard blocked a write");
                problem = Build(StatusCodes.Status403Forbidden, "forbidden", "لا تملك صلاحية لهذا الإجراء.");
                break;
            case DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } }:
                problem = Build(StatusCodes.Status409Conflict, "duplicate", "هذا السجل موجود مسبقاً أو أُرسل الطلب مرتين.");
                break;
            case BadHttpRequestException bad:
                problem = Build(StatusCodes.Status400BadRequest, "bad_request", "الطلب غير صالح.");
                logger.LogInformation(bad, "Bad request");
                break;
            default:
                var errorRef = "ERR-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
                logger.LogError(ex, "Unhandled error {ErrorRef}", errorRef);
                problem = Build(StatusCodes.Status500InternalServerError, "server_error", $"حدث خطأ غير متوقع. بياناتك لم تتغير. المرجع: {errorRef}");
                break;
        }

        http.Response.StatusCode = problem.Status ?? 500;
        await http.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json", ct);
        return true;
    }

    private static ProblemDetails Build(int status, string code, string message) => new()
    {
        Status = status,
        Title = message,
        Type = $"https://rahoon.example/problems/{code}",
        Extensions = { ["code"] = code },
    };
}
