using System.Net;

namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>
/// Official sources of the directory. Each source returns only what its publisher states. Network rules: one request at a
/// time with a pause between requests, a timeout on each, up to three attempts with backoff on timeouts and 5xx/429, and no
/// attempt to get around a refusal (401/403/404 or a login wall end that source with a reported failure).
/// </summary>
public static partial class DirectorySources
{
    public const string HttpClientName = "directory-sources";
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<string> Keys => Sources.Keys.ToList();

    public static async Task<SourceResult> FetchAsync(string key, HttpClient http, ILogger log, DateOnly today, SourceOptions? options = null)
    {
        try
        {
            return await Sources[key](http, log, today, options ?? new SourceOptions());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SourceRefusedException or FormatException)
        {
            log.LogWarning(ex, "Directory source {Source} failed", key);
            return new SourceResult(key, [], [$"source unavailable: {ex.Message}"], ["nothing was changed for this source's organizations"]);
        }
    }

    /// <summary>The server answered with a refusal; the importer does not try to get around it.</summary>
    public sealed class SourceRefusedException(string message) : Exception(message);

    /// <summary>GET with timeout, polite pause and bounded retries on transient failures only.</summary>
    internal static async Task<string> GetStringAsync(HttpClient http, string url, ILogger log)
    {
        for (var attempt = 1; ; attempt++)
        {
            await Task.Delay(Pause);
            try
            {
                using var res = await http.GetAsync(url);
                if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.Gone)
                    throw new SourceRefusedException($"{url} answered {(int)res.StatusCode}");
                if ((res.StatusCode == HttpStatusCode.TooManyRequests || (int)res.StatusCode >= 500) && attempt < 3)
                {
                    var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * attempt);
                    log.LogInformation("{Url} answered {Status}; retrying in {Wait}", url, (int)res.StatusCode, wait);
                    await Task.Delay(wait < TimeSpan.FromMinutes(2) ? wait : TimeSpan.FromMinutes(2));
                    continue;
                }
                res.EnsureSuccessStatusCode();
                return await res.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException) when (attempt < 3)
            {
                log.LogInformation("{Url} timed out; retrying", url);
            }
        }
    }
}
