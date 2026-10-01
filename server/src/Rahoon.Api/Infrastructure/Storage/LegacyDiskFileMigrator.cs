using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Persistence;

namespace Rahoon.Api.Infrastructure.Storage;

/// <summary>
/// One-time move of marketplace files from the old disk storage (Storage:Root) into the database (2026-10-01).
/// Runs between the RemoveLegacyMortgageModel migration (which adds files.* and the nullable file ids) and the
/// FilesInDatabase migration (which drops the disk-path columns and refuses to run while a file is not copied).
/// For each private document and listing photo: reads the disk file, checks its size and SHA-256 against the recorded
/// values, stores metadata and bytes in files.*, links the row, reads the stored bytes back and checks them again, and
/// only then deletes the disk copy. Afterwards it deletes the withdrawn mortgage-help model's leftover files
/// (per-organization folders) under the same root. Safe to rerun: rows already linked are skipped.
/// </summary>
public static partial class LegacyDiskFileMigrator
{
    private sealed record Row(Guid Id, Guid ApplicantUserId, Guid SaleRequestId, string FileName, string ContentType, long SizeBytes,
        string Sha256, string StorageKey, Guid UploadedByUserId, DateTimeOffset CreatedAt);

    public static async Task RunAsync(RahoonDbContext db, IConfiguration config, IHostEnvironment env, ILogger log, CancellationToken ct = default)
    {
        var hasDiskColumns = await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::int AS "Value" FROM information_schema.columns
            WHERE table_schema = 'market' AND table_name IN ('private_documents', 'listing_photos') AND column_name = 'storage_key'
            """).SingleAsync(ct) == 2;
        if (!hasDiskColumns) return;

        var root = Path.GetFullPath(config["Storage:Root"] is { Length: > 0 } r
            ? (Path.IsPathRooted(r) ? r : Path.Combine(env.ContentRootPath, r))
            : Path.Combine(env.ContentRootPath, ".data", "documents"));

        var failures = new List<string>();
        var moved = 0;
        long bytesMoved = 0;
        // Table names come from this fixed list, never from input.
#pragma warning disable EF1002
        foreach (var (table, visibility) in new[] { ("private_documents", FileVisibility.Private), ("listing_photos", FileVisibility.ListingPhoto) })
        {
            var rows = await db.Database.SqlQueryRaw<Row>($"""
                SELECT id, applicant_user_id, sale_request_id, file_name, content_type, size_bytes, sha256, storage_key, uploaded_by_user_id, created_at
                FROM market.{table} WHERE file_id IS NULL ORDER BY created_at
                """).ToListAsync(ct);
            foreach (var row in rows)
            {
                var path = ResolvePath(root, row.StorageKey);
                if (path is null || !File.Exists(path))
                {
                    failures.Add($"{table} {row.Id}: disk file «{row.StorageKey}» not found under {root}");
                    continue;
                }
                var bytes = await File.ReadAllBytesAsync(path, ct);
                if (bytes.LongLength != row.SizeBytes || Hash(bytes) != row.Sha256)
                {
                    failures.Add($"{table} {row.Id}: disk file «{row.StorageKey}» does not match the recorded size/checksum");
                    continue;
                }

                var fileId = Guid.CreateVersion7();
                var blobId = Guid.CreateVersion7();
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    await db.Database.ExecuteSqlAsync($"INSERT INTO files.file_blobs (id, content, created_at) VALUES ({blobId}, {bytes}, {row.CreatedAt})", ct);
                    await db.Database.ExecuteSqlAsync($"""
                        INSERT INTO files.stored_files (id, file_name, content_type, size_bytes, sha256, provider, storage_ref, visibility,
                                                        uploaded_by_user_id, owner_user_id, subject_type, subject_id, created_at)
                        VALUES ({fileId}, {row.FileName}, {row.ContentType}, {row.SizeBytes}, {row.Sha256}, {DatabaseContentStore.Name}, {blobId.ToString()},
                                {visibility.ToString()}, {row.UploadedByUserId}, {row.ApplicantUserId}, {"sale_request"}, {row.SaleRequestId}, {row.CreatedAt})
                        """, ct);
                    await db.Database.ExecuteSqlRawAsync($"UPDATE market.{table} SET file_id = {{0}} WHERE id = {{1}} AND file_id IS NULL", [fileId, row.Id], ct);
                    await tx.CommitAsync(ct);
                }

                // Read back what the database now holds before touching the disk copy.
                var stored = await db.Database.SqlQuery<byte[]>($"SELECT content AS \"Value\" FROM files.file_blobs WHERE id = {blobId}").SingleAsync(ct);
                if (stored.LongLength != row.SizeBytes || Hash(stored) != row.Sha256)
                {
                    failures.Add($"{table} {row.Id}: bytes read back from the database do not match; disk copy kept");
                    continue;
                }
                File.Delete(path);
                moved++;
                bytesMoved += stored.LongLength;
            }
        }
#pragma warning restore EF1002
        log.LogInformation("Moved {Count} marketplace files ({Bytes} bytes) from disk into the database, size and SHA-256 verified", moved, bytesMoved);

        if (failures.Count > 0)
            throw new InvalidOperationException("File migration incomplete — nothing was deleted for these files:\n" + string.Join('\n', failures));

        DeleteLegacyLeftovers(root, log);
    }

    /// <summary>
    /// Deletes what the withdrawn model left under the storage root: its per-organization folders (32 hex digits) and the
    /// now-empty marketplace areas. Anything else under the root is left alone.
    /// </summary>
    private static void DeleteLegacyLeftovers(string root, ILogger log)
    {
        if (!System.IO.Directory.Exists(root)) return;
        var deleted = 0;
        foreach (var dir in System.IO.Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(dir);
            if (OrgFolder().IsMatch(name))
            {
                deleted += System.IO.Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Count();
                System.IO.Directory.Delete(dir, recursive: true);
            }
            else if (name is "market-docs" or "market-photos")
            {
                RemoveEmptyDirectories(dir);
            }
        }
        if (deleted > 0) log.LogInformation("Deleted {Count} leftover files of the withdrawn mortgage-help model under {Root}", deleted, root);
    }

    private static void RemoveEmptyDirectories(string dir)
    {
        foreach (var sub in System.IO.Directory.EnumerateDirectories(dir)) RemoveEmptyDirectories(sub);
        if (!System.IO.Directory.EnumerateFileSystemEntries(dir).Any()) System.IO.Directory.Delete(dir);
    }

    private static string? ResolvePath(string root, string key)
    {
        var full = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex OrgFolder();
}
