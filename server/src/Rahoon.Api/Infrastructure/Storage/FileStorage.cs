using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Time;

namespace Rahoon.Api.Infrastructure.Storage;

/*
 * File storage (2026-10-01). Business code holds only a stable file id (StoredFile.Id). The metadata row says which
 * provider holds the bytes and under which internal reference; the bytes themselves sit behind IFileContentStore.
 * Today's provider is "database" (a separate bytea table). A future object-storage provider is another
 * IFileContentStore: files of both providers can coexist because every read goes through the row's own Provider.
 * Migration procedure: docs/architecture.md «File storage».
 */

public enum FileVisibility
{
    /// <summary>Only the owner of the related request and the authorised Rahoon team.</summary>
    Private,
    /// <summary>A listing photo: public only while the marketplace publication rules allow it (checked per request).</summary>
    ListingPhoto,
}

/// <summary>Metadata and ownership of an uploaded file. Never carries the payload, so it is safe in any query.</summary>
public sealed class StoredFile : Entity
{
    public required string FileName { get; set; }
    /// <summary>Detected from the file signature, never from the extension or the browser.</summary>
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    /// <summary>Lowercase hex SHA-256 of the stored bytes.</summary>
    public required string Sha256 { get; set; }
    /// <summary>database (today) · a future object-storage provider name.</summary>
    public required string Provider { get; set; }
    /// <summary>Provider-internal reference (database: the FileBlob id; object storage: the object key).</summary>
    public required string StorageRef { get; set; }
    public FileVisibility Visibility { get; set; }
    public Guid? UploadedByUserId { get; set; }
    /// <summary>The person the file belongs to (the request's applicant).</summary>
    public Guid? OwnerUserId { get; set; }
    /// <summary>sale_request (today) — what the file is attached to.</summary>
    public string? SubjectType { get; set; }
    public Guid? SubjectId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>The bytes of a file kept by the database provider. Read only when the content is served.</summary>
public sealed class FileBlob
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required byte[] Content { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A place that holds file bytes. Provider-specific details stay behind this interface.</summary>
public interface IFileContentStore
{
    string Provider { get; }
    /// <summary>Stores the bytes (inside the caller's unit of work when the provider is transactional); returns the internal reference.</summary>
    Task<string> PutAsync(byte[] content, CancellationToken ct = default);
    Task<byte[]?> GetAsync(string storageRef, CancellationToken ct = default);
    Task DeleteAsync(string storageRef, CancellationToken ct = default);
}

/// <summary>Database provider: bytes in files.file_blobs (bytea), added to the same transaction as the business change.</summary>
public sealed class DatabaseContentStore(RahoonDbContext db, IClock clock) : IFileContentStore
{
    public const string Name = "database";
    public string Provider => Name;

    public Task<string> PutAsync(byte[] content, CancellationToken ct = default)
    {
        var blob = new FileBlob { Content = content, CreatedAt = clock.UtcNow };
        db.FileBlobs.Add(blob);
        return Task.FromResult(blob.Id.ToString());
    }

    public async Task<byte[]?> GetAsync(string storageRef, CancellationToken ct = default) =>
        Guid.TryParse(storageRef, out var id)
            ? await db.FileBlobs.Where(b => b.Id == id).Select(b => b.Content).FirstOrDefaultAsync(ct)
            : null;

    public async Task DeleteAsync(string storageRef, CancellationToken ct = default)
    {
        if (Guid.TryParse(storageRef, out var id)) await db.FileBlobs.Where(b => b.Id == id).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Upload limits (Storage:Limits). Types are content types detected from the file signature.</summary>
public sealed class StorageLimits
{
    public long MaxPhotoBytes { get; set; } = 10 * 1024 * 1024;
    public long MaxDocumentBytes { get; set; } = 20 * 1024 * 1024;
    public int MaxPhotosPerRequest { get; set; } = 30;
    public int MaxDocumentsPerRequest { get; set; } = 40;
    // No initial values: the configuration binder appends to an existing array instead of replacing it.
    public string[]? PhotoTypes { get; set; }
    public string[]? DocumentTypes { get; set; }

    public static StorageLimits From(IConfiguration config)
    {
        var l = config.GetSection("Storage:Limits").Get<StorageLimits>() ?? new StorageLimits();
        if (l.PhotoTypes is not { Length: > 0 }) l.PhotoTypes = ["image/jpeg", "image/png", "image/webp"];
        if (l.DocumentTypes is not { Length: > 0 }) l.DocumentTypes = ["application/pdf", "image/jpeg", "image/png"];
        return l;
    }
}

public sealed record FileUpload(byte[] Content, string FileName, FileVisibility Visibility, Guid? UploadedBy, Guid? OwnerUserId, string? SubjectType, Guid? SubjectId);

/// <summary>Business-facing file service: validates, stores through the configured provider, opens by stable id.</summary>
public sealed class FileStore(RahoonDbContext db, IEnumerable<IFileContentStore> stores, IConfiguration config, IClock clock)
{
    private IFileContentStore StoreFor(string provider) =>
        stores.FirstOrDefault(s => s.Provider == provider) ?? throw new InvalidOperationException($"No file content store for provider «{provider}».");

    /// <summary>The provider new uploads go to (Storage:Provider, default database).</summary>
    public string WriteProvider => config["Storage:Provider"] is { Length: > 0 } p ? p : DatabaseContentStore.Name;

    public static string DetectContentType(ReadOnlySpan<byte> head) => head switch
    {
        [0x25, 0x50, 0x44, 0x46, ..] => "application/pdf",
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => "",
    };

    private static readonly Dictionary<string, string> TypeNames = new()
    {
        ["application/pdf"] = "PDF", ["image/jpeg"] = "JPG", ["image/png"] = "PNG", ["image/webp"] = "WebP",
    };

    /// <summary>Checks size and signature against the allowed list; returns the detected content type.</summary>
    public static string Validate(byte[] content, long maxBytes, IReadOnlyCollection<string> allowed)
    {
        if (content.Length == 0) throw new DomainException("file_empty", "الملف فارغ.");
        if (content.LongLength > maxBytes) throw new DomainException("file_too_large", $"حجم الملف يتجاوز {maxBytes / (1024 * 1024)} م.ب.");
        var type = DetectContentType(content.AsSpan(0, Math.Min(12, content.Length)));
        if (type.Length == 0 || !allowed.Contains(type))
            throw new DomainException("file_type", $"نوع الملف غير مدعوم. الأنواع المقبولة: {string.Join(" أو ", allowed.Select(t => TypeNames.GetValueOrDefault(t, t)))}.");
        return type;
    }

    public static string SafeName(string? name)
    {
        var n = Path.GetFileName(name ?? "file");
        n = new string(n.Where(c => !char.IsControl(c) && c is not ('/' or '\\' or '"')).ToArray());
        return n.Length == 0 ? "file" : n.Length > 200 ? n[^200..] : n;
    }

    /// <summary>Adds the file (metadata + payload) to the current unit of work. The caller saves.</summary>
    public async Task<StoredFile> AddAsync(FileUpload upload, string contentType, CancellationToken ct = default)
    {
        var store = StoreFor(WriteProvider);
        var storageRef = await store.PutAsync(upload.Content, ct);
        var file = new StoredFile
        {
            FileName = SafeName(upload.FileName), ContentType = contentType, SizeBytes = upload.Content.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(upload.Content)), Provider = store.Provider, StorageRef = storageRef,
            Visibility = upload.Visibility, UploadedByUserId = upload.UploadedBy, OwnerUserId = upload.OwnerUserId,
            SubjectType = upload.SubjectType, SubjectId = upload.SubjectId, CreatedAt = clock.UtcNow,
        };
        db.StoredFiles.Add(file);
        return file;
    }

    /// <summary>Reads the bytes through the file's own provider and checks them against the recorded size and checksum.</summary>
    public async Task<(StoredFile File, byte[] Content)?> ReadAsync(Guid fileId, CancellationToken ct = default)
    {
        var file = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, ct);
        if (file is null) return null;
        var content = await StoreFor(file.Provider).GetAsync(file.StorageRef, ct);
        if (content is null || content.LongLength != file.SizeBytes || Convert.ToHexStringLower(SHA256.HashData(content)) != file.Sha256)
            throw new DomainException("file_unavailable", "تعذّر قراءة الملف كما خُزّن. أبلغ فريق رهون.", StatusCodes.Status500InternalServerError);
        return (file, content);
    }
}
