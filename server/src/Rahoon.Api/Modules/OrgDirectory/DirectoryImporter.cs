using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>One organization as found in a source. Only what the source states; nothing is inferred.</summary>
public sealed record DirectoryCandidate(
    string SourceKey, string ItemKey, string NameAr, string? NameEn, IReadOnlyList<string> Types, string? Website,
    string? LicenseNumber, string? RegistrationNumber, string SourceName, string SourceUrl, DateOnly VerifiedOn);

/// <summary>What one source produced: the candidates and the problems met (never silently dropped).</summary>
public sealed record SourceResult(string SourceKey, IReadOnlyList<DirectoryCandidate> Candidates, IReadOnlyList<string> Failures, IReadOnlyList<string> Notes);

public sealed class ImportReport
{
    public int Discovered { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    /// <summary>Matched a record an administrator edited: left as the administrator set it.</summary>
    public int SkippedAdminEdited { get; set; }
    public int Failed { get; set; }
    public List<string> Failures { get; } = [];
    public List<string> Notes { get; } = [];
    public Dictionary<string, int> DiscoveredBySource { get; } = new();

    public int Skipped => Unchanged + SkippedAdminEdited;

    public string Summary() =>
        $"discovered={Discovered} created={Created} updated={Updated} skipped={Skipped} (unchanged={Unchanged}, admin-edited={SkippedAdminEdited}) failed={Failed}";
}

/// <summary>
/// Imports organizations into the directory. Idempotent: a rerun with the same data changes nothing. Matching is by the
/// source's own key first, then by normalized Arabic name, then by normalized English name; types are merged, so one
/// organization found as a bank and as a developer is one record with both types. Records an administrator edited are
/// never changed (only the new source key is remembered). Nothing is ever deleted or deactivated by an import — a source
/// that is down or no longer lists an organization says nothing about the organization.
/// </summary>
public sealed class DirectoryImporter(RahoonDbContext db, IClock clock, ILogger<DirectoryImporter> log)
{
    public async Task<ImportReport> ImportAsync(IEnumerable<SourceResult> results, bool dryRun, CancellationToken ct = default)
    {
        var report = new ImportReport();
        var existing = await db.DirectoryOrganizations.ToListAsync(ct);
        var now = clock.UtcNow;
        // A source may list the same item twice (e.g. on two pages): the first occurrence wins, so reruns are stable.
        var seenKeys = new HashSet<string>();
        var repeated = 0;

        foreach (var result in results)
        {
            report.Failures.AddRange(result.Failures.Select(f => $"[{result.SourceKey}] {f}"));
            report.Failed += result.Failures.Count;
            report.Notes.AddRange(result.Notes.Select(n => $"[{result.SourceKey}] {n}"));
            report.DiscoveredBySource[result.SourceKey] = result.Candidates.Count;

            foreach (var c in result.Candidates)
            {
                report.Discovered++;
                var problem = Check(c);
                if (problem is not null)
                {
                    report.Failed++;
                    report.Failures.Add($"[{c.SourceKey}] {c.ItemKey}: {problem}");
                    continue;
                }
                var importKey = $"{c.SourceKey}:{c.ItemKey}";
                if (!seenKeys.Add(importKey))
                {
                    repeated++;
                    report.Unchanged++;
                    continue;
                }
                var normAr = DirectoryNames.Normalize(c.NameAr);
                var normEn = string.IsNullOrWhiteSpace(c.NameEn) ? null : DirectoryNames.NormalizeEnglish(c.NameEn);
                var types = OrgTypes.All.Where(c.Types.Contains).ToList();
                var website = DirectoryNames.CleanUrl(c.Website);

                var match = existing.FirstOrDefault(d => d.ImportKeys.Contains(importKey))
                            ?? existing.FirstOrDefault(d => d.NormalizedNameAr == normAr)
                            ?? (normEn is { Length: > 0 } ? existing.FirstOrDefault(d => d.NormalizedNameEn == normEn) : null);

                if (match is null)
                {
                    var d = new DirectoryOrganization
                    {
                        NameAr = DirectoryNames.Clean(c.NameAr)!, NormalizedNameAr = normAr, NameEn = DirectoryNames.Clean(c.NameEn), NormalizedNameEn = normEn,
                        Types = types, Website = website, LicenseNumber = DirectoryNames.Clean(c.LicenseNumber, 60),
                        RegistrationNumber = DirectoryNames.Clean(c.RegistrationNumber, 60), SourceName = c.SourceName, SourceUrl = c.SourceUrl,
                        VerifiedOn = c.VerifiedOn, Active = true, Origin = DirectoryOrigins.Import, ImportKeys = [importKey], LastImportedAt = now,
                    };
                    existing.Add(d);
                    db.DirectoryOrganizations.Add(d);
                    report.Created++;
                    continue;
                }

                if (match.AdminEditedAt is not null)
                {
                    // Administrator edits win: remember that this source lists the record, change nothing else.
                    if (!match.ImportKeys.Contains(importKey)) match.ImportKeys = [.. match.ImportKeys, importKey];
                    report.SkippedAdminEdited++;
                    continue;
                }

                var changed = false;
                void Set<T>(T current, T value, Action<T> assign)
                {
                    if (EqualityComparer<T>.Default.Equals(current, value)) return;
                    assign(value);
                    changed = true;
                }

                // The source item that created the record owns its fields; any other item (another source, or a second listing of
                // the same organization in this source) only fills gaps — so reruns never flip a field between two values.
                var owner = match.Origin == DirectoryOrigins.Import && (match.ImportKeys.Count == 0 || match.ImportKeys[0] == importKey);
                var mergedTypes = OrgTypes.All.Where(t => match.Types.Contains(t) || types.Contains(t)).ToList();
                if (!mergedTypes.SequenceEqual(match.Types)) { match.Types = mergedTypes; changed = true; }
                if (!match.ImportKeys.Contains(importKey)) { match.ImportKeys = [.. match.ImportKeys, importKey]; changed = true; }

                if (owner)
                {
                    // The source that owns these fields may correct them.
                    var nameAr = DirectoryNames.Clean(c.NameAr)!;
                    if (nameAr != match.NameAr && !existing.Any(o => o != match && o.NormalizedNameAr == normAr))
                    {
                        match.NameAr = nameAr;
                        match.NormalizedNameAr = normAr;
                        changed = true;
                    }
                    if (DirectoryNames.Clean(c.NameEn) is { } en) Set(match.NameEn, en, v => { match.NameEn = v; match.NormalizedNameEn = normEn; });
                    if (website is not null) Set(match.Website, website, v => match.Website = v);
                    if (DirectoryNames.Clean(c.LicenseNumber, 60) is { } lic) Set(match.LicenseNumber, lic, v => match.LicenseNumber = v);
                    if (DirectoryNames.Clean(c.RegistrationNumber, 60) is { } reg) Set(match.RegistrationNumber, reg, v => match.RegistrationNumber = v);
                    Set(match.SourceName, c.SourceName, v => match.SourceName = v);
                    Set(match.SourceUrl, c.SourceUrl, v => match.SourceUrl = v);
                }
                else
                {
                    // Another source listing the same organization only fills gaps.
                    if (match.NameEn is null && DirectoryNames.Clean(c.NameEn) is { } en) { match.NameEn = en; match.NormalizedNameEn = normEn; changed = true; }
                    if (match.Website is null && website is not null) { match.Website = website; changed = true; }
                }

                if (changed)
                {
                    if (match.VerifiedOn is null || c.VerifiedOn > match.VerifiedOn) match.VerifiedOn = c.VerifiedOn;
                    match.LastImportedAt = now;
                    report.Updated++;
                }
                else
                {
                    report.Unchanged++;
                }
            }
        }

        if (repeated > 0) report.Notes.Add($"{repeated} items listed more than once by their source were counted once");
        if (dryRun)
        {
            db.ChangeTracker.Clear();
            report.Notes.Add("dry run: nothing was written");
        }
        else
        {
            await db.SaveChangesAsync(ct);
        }
        log.LogInformation("Directory import: {Summary}", report.Summary());
        return report;
    }

    private static string? Check(DirectoryCandidate c)
    {
        if (DirectoryNames.Clean(c.NameAr) is not { Length: >= 2 } || DirectoryNames.Normalize(c.NameAr).Length == 0) return "missing Arabic name";
        if (c.Types.Count == 0 || c.Types.Any(t => !OrgTypes.All.Contains(t))) return "unknown or missing type";
        if (string.IsNullOrWhiteSpace(c.SourceName) || DirectoryNames.CleanUrl(c.SourceUrl) is null) return "missing source name or URL";
        if (!string.IsNullOrWhiteSpace(c.Website) && DirectoryNames.CleanUrl(c.Website) is null) return $"invalid website «{c.Website}»";
        if (string.IsNullOrWhiteSpace(c.ItemKey)) return "missing item key";
        return null;
    }

    // ── Verified dataset files (CSV or JSON) through the same pipeline ──

    /// <summary>
    /// Reads a dataset file. Columns/properties: name_ar, name_en, types (developer|bank|finance_company, separated by «|» or «;»),
    /// website, license_number, registration_number, source_name, source_url, verified_on (YYYY-MM-DD), item_key (optional).
    /// Every row must cite its source; the file itself must come from an official publication (the importer cannot check that).
    /// </summary>
    public static SourceResult ReadDatasetFile(string path, DateOnly today)
    {
        var sourceKey = "file:" + Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        var rows = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? ReadJson(path) : ReadCsv(path);
        var candidates = new List<DirectoryCandidate>();
        var failures = new List<string>();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            string? Get(string k) => r.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
            var nameAr = Get("name_ar");
            if (nameAr is null) { failures.Add($"row {i + 1}: missing name_ar"); continue; }
            var types = (Get("types") ?? "").Split(['|', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            DateOnly verified = today;
            if (Get("verified_on") is { } vo && !DateOnly.TryParseExact(vo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out verified))
            {
                failures.Add($"row {i + 1}: verified_on «{vo}» is not YYYY-MM-DD");
                continue;
            }
            candidates.Add(new DirectoryCandidate(sourceKey, Get("item_key") ?? DirectoryNames.Normalize(nameAr), nameAr, Get("name_en"), types,
                Get("website"), Get("license_number"), Get("registration_number"), Get("source_name") ?? "", Get("source_url") ?? "", verified));
        }
        return new SourceResult(sourceKey, candidates, failures,
            ["dataset file: the importer checks the format and cited source of each row, not that the file is an official publication"]);
    }

    private static List<Dictionary<string, string?>> ReadJson(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var array = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
            : doc.RootElement.TryGetProperty("organizations", out var orgs) ? orgs : throw new InvalidDataException("Expected a JSON array or { organizations: [...] }.");
        return array.EnumerateArray().Select(e => e.EnumerateObject().ToDictionary(p => p.Name.ToLowerInvariant(), p => p.Value.ValueKind switch
        {
            JsonValueKind.Array => string.Join('|', p.Value.EnumerateArray().Select(x => x.ToString())),
            JsonValueKind.Null => null,
            _ => p.Value.ToString(),
        })).ToList();
    }

    private static List<Dictionary<string, string?>> ReadCsv(string path)
    {
        var lines = ParseCsv(File.ReadAllText(path));
        if (lines.Count == 0) return [];
        var header = lines[0].Select(h => h.Trim().TrimStart('﻿').ToLowerInvariant()).ToList();
        return lines.Skip(1).Where(l => l.Any(c => c.Length > 0))
            .Select(l => header.Select((h, i) => (h, v: i < l.Count ? l[i] : null)).ToDictionary(x => x.h, x => (string?)x.v)).ToList();
    }

    /// <summary>RFC 4180 CSV (quoted fields, doubled quotes, newlines inside quotes).</summary>
    internal static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                rows.Add(row); row = [];
            }
            else field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
