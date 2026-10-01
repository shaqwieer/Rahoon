namespace Rahoon.Api.Modules.OrgDirectory;

/// <summary>
/// `dotnet run -- import-directory [--source banks|finance|developers|all] [--pages 1-20] [--pause 15] [--file path.csv|path.json] [--dry-run]`.
/// Fetches the official sources (or reads a verified dataset file), imports through <see cref="DirectoryImporter"/> and prints
/// discovered/created/updated/skipped/failed counts. Exit code 0 when nothing failed, 2 when some items failed (the rest is
/// still imported), 1 on bad arguments.
/// </summary>
public static class DirectoryCli
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        string? source = null, file = null;
        var dryRun = false;
        var options = new SourceOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--source" when i + 1 < args.Length: source = args[++i]; break;
                case "--file" when i + 1 < args.Length: file = args[++i]; break;
                case "--dry-run": dryRun = true; break;
                case "--pages" when i + 1 < args.Length:
                {
                    var range = args[++i].Split('-');
                    if (!int.TryParse(range[0], out var from) || from < 1) { Console.Error.WriteLine("import-directory: --pages expects FROM-TO, e.g. 21-40"); return 1; }
                    options = options with { FromPage = from, ToPage = range.Length > 1 && int.TryParse(range[1], out var to) ? to : null };
                    break;
                }
                case "--pause" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out var pause) || pause < 5) { Console.Error.WriteLine("import-directory: --pause expects seconds (5 or more)"); return 1; }
                    options = options with { PauseSeconds = pause };
                    break;
                default:
                    Console.Error.WriteLine($"import-directory: unknown argument «{args[i]}». Usage: import-directory [--source {string.Join('|', DirectorySources.Keys)}|all] [--pages FROM-TO] [--pause SECONDS] [--file path] [--dry-run]");
                    return 1;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var results = new List<SourceResult>();
        if (file is not null)
        {
            if (!File.Exists(file)) { Console.Error.WriteLine($"import-directory: file not found: {file}"); return 1; }
            results.Add(DirectoryImporter.ReadDatasetFile(file, today));
        }
        if (file is null || source is not null)
        {
            var keys = source is null or "all" ? DirectorySources.Keys : [source];
            if (keys.Any(k => !DirectorySources.Keys.Contains(k)))
            {
                Console.Error.WriteLine($"import-directory: unknown source «{source}». Sources: {string.Join(", ", DirectorySources.Keys)}, all.");
                return 1;
            }
            var http = services.GetRequiredService<IHttpClientFactory>().CreateClient(DirectorySources.HttpClientName);
            var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("Rahoon.DirectorySources");
            foreach (var key in keys) results.Add(await DirectorySources.FetchAsync(key, http, log, today, options));
        }

        var report = await services.GetRequiredService<DirectoryImporter>().ImportAsync(results, dryRun);
        Console.WriteLine($"import-directory{(dryRun ? " (dry run)" : "")}: {report.Summary()}");
        foreach (var (key, count) in report.DiscoveredBySource) Console.WriteLine($"  source {key}: {count} discovered");
        foreach (var n in report.Notes) Console.WriteLine($"  note: {n}");
        foreach (var f in report.Failures) Console.WriteLine($"  failed: {f}");
        return report.Failed > 0 ? 2 : 0;
    }
}
