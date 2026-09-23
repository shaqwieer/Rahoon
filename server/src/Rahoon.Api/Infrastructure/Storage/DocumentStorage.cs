using System.Security.Cryptography;
using System.Text;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Modules.Documents;

namespace Rahoon.Api.Infrastructure.Storage;

public sealed record StoredFile(string StorageKey, string Sha256, long SizeBytes, string ContentType);

/// <summary>
/// Controlled document storage. Files are never served from a public path; every read
/// goes through an authorized API endpoint that logs the access.
/// </summary>
public interface IDocumentStorage
{
    Task<StoredFile> SaveAsync(Stream content, string fileName, Guid orgId, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);
}

public interface IFileScanner
{
    Task<ScanStatus> ScanAsync(string storageKey, CancellationToken ct = default);
}

public static class FileRules
{
    public const long MaxBytes = 20 * 1024 * 1024;

    /// <summary>Checks the magic bytes — the declared content type is never trusted.</summary>
    public static string DetectContentType(ReadOnlySpan<byte> head) => head switch
    {
        [0x25, 0x50, 0x44, 0x46, ..] => "application/pdf",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        _ => "",
    };
}

/// <summary>Local development adapter: files under a private directory, keyed by org and random id.</summary>
public sealed class LocalDocumentStorage(IConfiguration config, IHostEnvironment env) : IDocumentStorage
{
    private readonly string _root = Path.GetFullPath(config["Storage:Root"] is { Length: > 0 } r
        ? (Path.IsPathRooted(r) ? r : Path.Combine(env.ContentRootPath, r))
        : Path.Combine(env.ContentRootPath, ".data", "documents"));

    public async Task<StoredFile> SaveAsync(Stream content, string fileName, Guid orgId, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0) throw new DomainException("file_empty", "الملف فارغ.");
        if (buffer.Length > FileRules.MaxBytes) throw new DomainException("file_too_large", "حجم الملف يتجاوز 20 م.ب.");

        var bytes = buffer.ToArray();
        var type = FileRules.DetectContentType(bytes.AsSpan(0, Math.Min(8, bytes.Length)));
        if (type.Length == 0) throw new DomainException("file_type", "نوع الملف غير مدعوم. الأنواع المقبولة: PDF أو JPG أو PNG.");

        var key = $"{orgId:N}/{DateTime.UtcNow:yyyy/MM}/{Guid.CreateVersion7():N}";
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new StoredFile(key, sha, bytes.LongLength, type);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var path = ResolvePath(storageKey);
        if (!File.Exists(path)) throw new NotFoundException();
        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    private string ResolvePath(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar)));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) throw new ForbiddenException();
        return full;
    }
}

/// <summary>
/// Development scanner: signature check only (detects the EICAR test string). Not an
/// antivirus — production must plug in a real scanning service.
/// </summary>
public sealed class BasicSignatureScanner(IDocumentStorage storage) : IFileScanner
{
    private static readonly byte[] Eicar = Encoding.ASCII.GetBytes("EICAR-STANDARD-ANTIVIRUS-TEST-FILE");

    public async Task<ScanStatus> ScanAsync(string storageKey, CancellationToken ct = default)
    {
        await using var s = await storage.OpenReadAsync(storageKey, ct);
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray().AsSpan().IndexOf(Eicar) >= 0 ? ScanStatus.Infected : ScanStatus.Clean;
    }
}
