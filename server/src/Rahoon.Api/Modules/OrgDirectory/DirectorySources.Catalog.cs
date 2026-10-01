using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>Paging limits for paged sources (REGA): pages From..To (default: all pages it can see), with a pause between pages.</summary>
public sealed record SourceOptions(int? FromPage = null, int? ToPage = null, int PauseSeconds = 15);

/*
 * Sources (checked 2026-10-01):
 *  - banks: SAMA «البنوك المرخصة» list, served as JSON by the handler behind
 *    https://www.sama.gov.sa/ar-sa/Supervision/LicenseEntities/Pages/LicensedBanks.aspx (one call, whole list).
 *    SAMA labels LicensingNumber there as the commercial-registration number and publishes no bank licence number,
 *    so banks get RegistrationNumber (the unified number, else the CR number) and no LicenseNumber.
 *  - finance: SAMA «شركات التمويل المرخصة» list (FinanceLicencedEntities.aspx), same handler. LicensingNumber is the licence.
 *    Only the «شركات التمويل» category is imported; «شركات النشاطات المساندة للتمويل» (aggregators, lease registries) do
 *    not lend and are reported as excluded.
 *  - developers: REGA «الاستعلام عن المنشآت المؤهلة في البيع على الخارطة والمساهمات العقارية» (Wafi) results pages,
 *    20 per page. Only «مطور عقاري» records with an active qualification. REGA publishes no English name or website.
 *    REGA throttles repeated requests by answering «Not Exist»: the run stops there and says from which page to resume.
 */
public static partial class DirectorySources
{
    private const string SamaHandler = "https://www.sama.gov.sa/ar-sa/_LAYOUTS/15/SAMA.Portal/PortalHandler.ashx?op=LoadItems&viewName=Archive&listUrl=";
    private const string SamaBanksList = "/ar-sa/Supervision/LicenseEntities/Lists/LicensedBanks";
    private const string SamaFinanceList = "/ar-sa/Supervision/LicenseEntities/Lists/LicensedFinance";
    private const string SamaBanksPage = "https://www.sama.gov.sa/ar-sa/Supervision/LicenseEntities/Pages/LicensedBanks.aspx";
    private const string SamaFinancePage = "https://www.sama.gov.sa/ar-sa/Supervision/LicenseEntities/Pages/FinanceLicencedEntities.aspx";
    private const string RegaResults = "https://rega.gov.sa/en/rega-services/real-estate-enquiries/result-page/?tabActive=Wafi+Developers&currentDeveloperPage=";
    private const string RegaPage = "https://rega.gov.sa/en/rega-services/real-estate-enquiries/enquires-about-qualified-companies-in-off-plan-sales-and-leases-and-real-estate-contribution/";

    private static readonly Dictionary<string, Func<HttpClient, ILogger, DateOnly, SourceOptions, Task<SourceResult>>> Sources = new()
    {
        ["banks"] = SamaBanksAsync,
        ["finance"] = SamaFinanceAsync,
        ["developers"] = RegaDevelopersAsync,
    };

    // ── SAMA ──

    private static async Task<JsonElement[]> SamaListAsync(HttpClient http, ILogger log, string list)
    {
        var text = await GetStringAsync(http, SamaHandler + Uri.EscapeDataString(list), log);
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("SAMA answered something other than a list");
        return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToArray();
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()?.Trim() is { Length: > 0 } s && s != "-" ? s : null;

    /// <summary>The organization's own site (scheme and host only); links that point back at SAMA are not a website.</summary>
    private static string? Website(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (DirectoryNames.CleanUrl(c) is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var u)) continue;
            if (u.Host.EndsWith("sama.gov.sa", StringComparison.OrdinalIgnoreCase)) continue;
            return u.GetLeftPart(UriPartial.Authority);
        }
        return null;
    }

    private static async Task<SourceResult> SamaBanksAsync(HttpClient http, ILogger log, DateOnly today, SourceOptions _)
    {
        var items = await SamaListAsync(http, log, SamaBanksList);
        var candidates = new List<DirectoryCandidate>();
        var failures = new List<string>();
        var notYet = new List<string>();
        foreach (var e in items)
        {
            var id = e.TryGetProperty("ID", out var idv) ? idv.ToString() : null;
            var name = Str(e, "Title");
            if (id is null || name is null) { failures.Add($"item without id or Arabic name: {e}"); continue; }
            var activity = $"{Str(e, "ActivityType")} {Str(e, "ActivityType_x003a_TitleEn")}";
            if (activity.Contains("Not yet operational", StringComparison.OrdinalIgnoreCase) || activity.Contains("لم يبدأ", StringComparison.Ordinal))
            {
                notYet.Add(name);
                continue;
            }
            candidates.Add(new DirectoryCandidate("sama-banks", id, name, Str(e, "TitleEn"), [OrgTypes.Bank],
                Website(Str(e, "UrlLinkAr"), Str(e, "UrlLinkEn")), LicenseNumber: null,
                RegistrationNumber: Str(e, "UnifiedNumber") ?? Str(e, "LicensingNumber"),
                SourceName: "البنك المركزي السعودي (ساما) — البنوك المرخصة", SourceUrl: SamaBanksPage, VerifiedOn: today));
        }
        var notes = new List<string> { $"{items.Length} listed by SAMA" };
        if (notYet.Count > 0) notes.Add($"not imported — listed as not yet operational: {string.Join("، ", notYet)}");
        return new SourceResult("sama-banks", candidates, failures, notes);
    }

    private static async Task<SourceResult> SamaFinanceAsync(HttpClient http, ILogger log, DateOnly today, SourceOptions _)
    {
        var items = await SamaListAsync(http, log, SamaFinanceList);
        var candidates = new List<DirectoryCandidate>();
        var failures = new List<string>();
        var support = 0;
        foreach (var e in items)
        {
            var id = e.TryGetProperty("ID", out var idv) ? idv.ToString() : null;
            var name = Str(e, "Title");
            if (id is null || name is null) { failures.Add($"item without id or Arabic name: {e}"); continue; }
            // Only finance companies; the «finance support» category (aggregators, lease registries) does not lend.
            if (!string.Equals(Str(e, "Categories_x003a_TitleEn"), "Finance Companies", StringComparison.OrdinalIgnoreCase)
                && Str(e, "Categories") != "شركات التمويل")
            {
                support++;
                continue;
            }
            candidates.Add(new DirectoryCandidate("sama-finance", id, name, Str(e, "TitleEn"), [OrgTypes.FinanceCompany],
                Website(Str(e, "UrlLinkAr"), Str(e, "UrlLinkEn")), LicenseNumber: Str(e, "LicensingNumber"),
                RegistrationNumber: Str(e, "UnifiedNumber"),
                SourceName: "البنك المركزي السعودي (ساما) — شركات التمويل المرخصة", SourceUrl: SamaFinancePage, VerifiedOn: today));
        }
        return new SourceResult("sama-finance", candidates, failures,
            [$"{items.Length} listed by SAMA", $"not imported — finance support companies (not lenders): {support}"]);
    }

    // ── REGA (Wafi) ──

    [GeneratedRegex("""<div class="card-body">(.*?)(?=<div class="card-body">|<nav aria-label="pagination"|$)""", RegexOptions.Singleline)]
    private static partial Regex RegaCard();
    [GeneratedRegex("""<div class="title">(.*?)</div>""", RegexOptions.Singleline)]
    private static partial Regex RegaTitle();
    [GeneratedRegex("""class="badge-status[^"]*">\s*<span class="dot"></span>\s*<span>(.*?)</span>""", RegexOptions.Singleline)]
    private static partial Regex RegaStatus();
    [GeneratedRegex("""currentDeveloperPage=(\d+)""")]
    private static partial Regex RegaPageLink();
    [GeneratedRegex(@"\s+")]
    private static partial Regex Ws();

    private static string? RegaField(string card, string label)
    {
        var m = Regex.Match(card, $"""<span class="fw-bold">\s*{Regex.Escape(label)}:\s*</span>(.*?)</div>""", RegexOptions.Singleline);
        return m.Success ? WebUtility.HtmlDecode(Ws().Replace(m.Groups[1].Value, " ")).Trim() is { Length: > 0 } v ? v : null : null;
    }

    private static async Task<SourceResult> RegaDevelopersAsync(HttpClient http, ILogger log, DateOnly today, SourceOptions options)
    {
        var candidates = new List<DirectoryCandidate>();
        var failures = new List<string>();
        var notes = new List<string>();
        int excludedType = 0, excludedStatus = 0;
        var page = Math.Max(1, options.FromPage ?? 1);
        int? lastPage = options.ToPage;
        var fetched = 0;
        while (lastPage is null || page <= lastPage)
        {
            if (fetched > 0) await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.PauseSeconds)));
            string html;
            int start;
            var backoff = 0;
            while (true)
            {
                try
                {
                    html = await GetStringAsync(http, RegaResults + page, log);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SourceRefusedException)
                {
                    failures.Add($"page {page}: {ex.Message} — rerun with --pages {page}-{lastPage?.ToString() ?? ""} to continue");
                    html = "";
                }
                fetched++;
                start = html.IndexOf("wafi-developers-page", StringComparison.Ordinal);
                // REGA answers rate-limited requests with HTTP 200 and «Not Exist»: never read that as an empty list. Wait and
                // ask again a few times (1, 2, 3 minutes); then stop and say where to resume.
                var refused = start >= 0 && html.AsSpan(start, Math.Min(800, html.Length - start)).Contains("Not Exist", StringComparison.Ordinal);
                if (!refused || ++backoff > 3) break;
                log.LogInformation("REGA answered «Not Exist» for page {Page}; waiting {Minutes} min", page, backoff);
                await Task.Delay(TimeSpan.FromMinutes(backoff));
            }
            if (html.Length == 0) break;
            if (start < 0) { failures.Add($"page {page}: the results section is missing (page layout changed?) — stopped"); break; }
            if (backoff > 3)
            {
                failures.Add($"page {page}: REGA kept answering «Not Exist» (rate limited) — stopped; rerun later with --pages {page}-{lastPage?.ToString() ?? ""}");
                break;
            }
            if (lastPage is null)
            {
                var max = RegaPageLink().Matches(html).Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(page).Max();
                lastPage = options.ToPage ?? max;
            }
            var section = html[start..];
            var cards = RegaCard().Matches(section);
            var onPage = 0;
            foreach (Match c in cards)
            {
                var card = c.Groups[1].Value;
                var title = RegaTitle().Match(card);
                if (!title.Success) continue;
                onPage++;
                var name = WebUtility.HtmlDecode(title.Groups[1].Value).Trim();
                var type = RegaField(card, "License type");
                var status = RegaStatus().Match(card) is { Success: true } st ? st.Groups[1].Value.Trim() : null;
                var license = RegaField(card, "License number")?.TrimStart('0');
                if (type != "مطور عقاري") { excludedType++; continue; }
                if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)) { excludedStatus++; continue; }
                if (string.IsNullOrEmpty(license)) { failures.Add($"page {page}: «{name}» has no licence number"); continue; }
                candidates.Add(new DirectoryCandidate("rega-wafi", license, name, null, [OrgTypes.Developer], null,
                    LicenseNumber: license, RegistrationNumber: null,
                    SourceName: "الهيئة العامة للعقار — المنشآت المؤهلة (وافي)", SourceUrl: RegaPage, VerifiedOn: today));
            }
            if (onPage == 0) { failures.Add($"page {page}: no records found — stopped"); break; }
            page++;
        }
        notes.Add($"pages fetched: {fetched}{(lastPage is { } lp ? $" (last page {lp})" : "")}");
        notes.Add($"not imported — other qualification types (accountants, consultants): {excludedType}; qualification not active: {excludedStatus}");
        notes.Add("REGA publishes no English name or website for developers");
        return new SourceResult("rega-wafi", candidates, failures, notes);
    }
}
