using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rahoon.Api.Tests.Infrastructure;

/// <summary>
/// Browser-like API client: keeps cookies itself, sends Origin and the CSRF header,
/// and supplies Idempotency-Key on mutations (explicitly or freshly generated).
/// </summary>
public sealed class TestClient(HttpClient http)
{
    public const string Origin = "http://localhost:3000";
    private readonly Dictionary<string, string> _cookies = new();

    public string? Csrf => _cookies.GetValueOrDefault("rahoon_csrf");

    public Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(HttpMethod method, string path, object? body = null,
        string? idempotencyKey = null, bool includeCsrf = true, string? origin = Origin, bool autoKey = true) =>
        SendContentAsync(method, path, body is null ? null : JsonContent.Create(body), idempotencyKey, includeCsrf, origin, autoKey);

    /// <summary>Multipart upload with the same cookie, CSRF, Origin and Idempotency-Key handling.</summary>
    public Task<(HttpStatusCode Status, JsonNode? Body)> PostMultipartAsync(string path, MultipartFormDataContent content, string? key = null) =>
        SendContentAsync(HttpMethod.Post, path, content, key);

    /// <summary>Raw GET (file downloads): status and body bytes.</summary>
    public async Task<(HttpStatusCode Status, byte[] Bytes, string? ContentType)> GetBytesAsync(string path)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Get, path);
        if (_cookies.Count > 0) msg.Headers.Add("Cookie", string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}")));
        using var res = await http.SendAsync(msg);
        return (res.StatusCode, await res.Content.ReadAsByteArrayAsync(), res.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Raw POST (CSV exports): status, body bytes and response headers.</summary>
    public async Task<(HttpStatusCode Status, byte[] Bytes, Dictionary<string, string> Headers)> PostRawAsync(string path, object? body = null)
    {
        byte[] bytes = [];
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (status, _) = await SendContentAsync(HttpMethod.Post, path, JsonContent.Create(body ?? new { }), onResponse: async res =>
        {
            bytes = await res.Content.ReadAsByteArrayAsync();
            foreach (var h in res.Headers.Concat(res.Content.Headers)) headers[h.Key] = string.Join(",", h.Value);
        });
        return (status, bytes, headers);
    }

    private async Task<(HttpStatusCode Status, JsonNode? Body)> SendContentAsync(HttpMethod method, string path, HttpContent? content,
        string? idempotencyKey = null, bool includeCsrf = true, string? origin = Origin, bool autoKey = true, Func<HttpResponseMessage, Task>? onResponse = null)
    {
        using var msg = new HttpRequestMessage(method, path);
        if (origin is not null) msg.Headers.Add("Origin", origin);
        if (_cookies.Count > 0) msg.Headers.Add("Cookie", string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}")));
        if (includeCsrf && Csrf is not null) msg.Headers.Add("X-CSRF-Token", Csrf);
        if (method != HttpMethod.Get && (idempotencyKey is not null || autoKey)) msg.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        if (content is not null) msg.Content = content;
        using var res = await http.SendAsync(msg);
        if (res.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var sc in setCookies)
            {
                var pair = sc.Split(';')[0].Split('=', 2);
                if (pair.Length == 2)
                {
                    if (string.IsNullOrEmpty(pair[1]) || sc.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase)) _cookies.Remove(pair[0]);
                    else _cookies[pair[0]] = pair[1];
                }
            }
        }
        if (onResponse is not null)
        {
            await onResponse(res);
            return (res.StatusCode, null);
        }
        var text = await res.Content.ReadAsStringAsync();
        JsonNode? node = null;
        if (!string.IsNullOrWhiteSpace(text) && (res.Content.Headers.ContentType?.MediaType?.Contains("json") ?? false))
            node = JsonNode.Parse(text);
        return (res.StatusCode, node);
    }

    /// <summary>Sends arbitrary content (multipart, raw) with the same cookies/CSRF/idempotency headers; returns raw bytes.</summary>
    public async Task<(HttpStatusCode Status, byte[] Bytes, string? ContentType)> SendRawAsync(HttpMethod method, string path, HttpContent? content = null, string? idempotencyKey = null)
    {
        using var msg = new HttpRequestMessage(method, path);
        msg.Headers.Add("Origin", Origin);
        if (_cookies.Count > 0) msg.Headers.Add("Cookie", string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}")));
        if (Csrf is not null) msg.Headers.Add("X-CSRF-Token", Csrf);
        if (method != HttpMethod.Get) msg.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        msg.Content = content;
        using var res = await http.SendAsync(msg);
        return (res.StatusCode, await res.Content.ReadAsByteArrayAsync(), res.Content.Headers.ContentType?.MediaType);
    }

    public Task<(HttpStatusCode Status, JsonNode? Body)> PatchAsync(string path, object body) => SendAsync(HttpMethod.Patch, path, body);

    public Task<(HttpStatusCode Status, JsonNode? Body)> GetAsync(string path) => SendAsync(HttpMethod.Get, path);
    public Task<(HttpStatusCode Status, JsonNode? Body)> PostAsync(string path, object? body = null, string? key = null) => SendAsync(HttpMethod.Post, path, body ?? new { }, key);
    public Task<(HttpStatusCode Status, JsonNode? Body)> PutAsync(string path, object body) => SendAsync(HttpMethod.Put, path, body);

    public async Task LoginAsync(string email, string password, string? orgName = null)
    {
        var (s1, login) = await PostAsync("/api/auth/login", new { email, password });
        if (s1 != HttpStatusCode.OK) throw new InvalidOperationException($"login failed {s1}: {login}");
        var code = login!["sandboxCode"]!.GetValue<string>();
        var (s2, verify) = await PostAsync("/api/auth/mfa/verify", new { code });
        if (s2 != HttpStatusCode.OK) throw new InvalidOperationException($"mfa failed {s2}: {verify}");
        if (verify!["next"]!.GetValue<string>() == "/select-context")
        {
            var (_, me) = await GetAsync("/api/auth/me");
            var membership = me!["memberships"]!.AsArray().First(m => orgName is null || m!["organization"]!.GetValue<string>() == orgName)!;
            var (s3, ctx) = await PostAsync("/api/auth/context", new { membershipId = membership["id"]!.GetValue<string>() });
            if (s3 != HttpStatusCode.OK) throw new InvalidOperationException($"context failed {s3}: {ctx}");
        }
    }

    /// <summary>Completes an MFA step-up (sandbox OTP) for sensitive decisions.</summary>
    public async Task StepUpAsync()
    {
        var (_, start) = await PostAsync("/api/auth/step-up/start");
        var (s, _) = await PostAsync("/api/auth/step-up/verify", new { code = start!["sandboxCode"]!.GetValue<string>() });
        if (s != HttpStatusCode.OK) throw new InvalidOperationException("step-up failed");
    }

    public static string Str(JsonNode? n, string key) => n?[key]?.GetValue<string>() ?? "";

    private static readonly JsonSerializerOptions RawJson = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>JSON text with Arabic left unescaped — use for Contains/DoesNotContain on Arabic content (ToJsonString escapes it).</summary>
    public static string Raw(JsonNode? n) => n?.ToJsonString(RawJson) ?? "";
    public static JsonSerializerOptions Json => new(JsonSerializerDefaults.Web);
}
