using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Tests.Infrastructure;
using static Rahoon.Api.Tests.Infrastructure.MarketScenarios;

namespace Rahoon.Api.Tests;

/// <summary>
/// Files in the database: bytes in files.file_blobs, metadata in files.stored_files, served only through /api/files/{id}
/// with the right authorization; type, size and count validated from the content.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class FileStorageTests(ApiFixture api)
{
    private static byte[] Pdf(string marker) => System.Text.Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n1 0 obj << >> endobj\ntrailer << >>\n%%EOF\n");

    private static MultipartFormDataContent Form(byte[] bytes, string fileName, string contentType, string? kind = "developer_contract")
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        if (kind is not null) form.Add(new StringContent(kind), "kind");
        return form;
    }

    [Fact]
    public async Task Uploaded_document_is_stored_in_the_database_and_downloads_byte_for_byte()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var bytes = Pdf(Guid.NewGuid().ToString());
        var (s, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", Form(bytes, "عقد.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.OK, s);
        var fileId = Guid.Parse(TestClient.Str(doc, "fileId"));
        Assert.Equal($"/api/files/{fileId}", TestClient.Str(doc, "url"));
        Assert.Equal("عقد.pdf", TestClient.Str(doc, "fileName"));
        Assert.Equal(bytes.Length, doc!["sizeBytes"]!.GetValue<long>());

        var meta = await api.WithDbAsync(db => db.StoredFiles.AsNoTracking().SingleAsync(f => f.Id == fileId));
        Assert.Equal(DatabaseContentStore.Name, meta.Provider);
        Assert.Equal("application/pdf", meta.ContentType);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), meta.Sha256);
        Assert.Equal(FileVisibility.Private, meta.Visibility);
        Assert.Equal("sale_request", meta.SubjectType);
        var blob = await api.WithDbAsync(db => db.FileBlobs.AsNoTracking().SingleAsync(b => b.Id == Guid.Parse(meta.StorageRef)));
        Assert.Equal(bytes, blob.Content);

        var (sd, downloaded, type) = await owner.GetBytesAsync($"/api/files/{fileId}");
        Assert.Equal(HttpStatusCode.OK, sd);
        Assert.Equal("application/pdf", type);
        Assert.Equal(bytes, downloaded);
    }

    [Fact]
    public async Task Private_files_are_served_only_to_their_owner_and_the_team()
    {
        var (owner, reference) = await SubmittedAsync(api);
        var (_, doc) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", Form(Pdf("private"), "statement.pdf", "application/pdf", "payment_proof"));
        var url = TestClient.Str(doc, "url");
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client().GetBytesAsync(url)).Status);
        var stranger = await SellerAsync(api);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetBytesAsync(url)).Status);
        Assert.Equal(HttpStatusCode.OK, (await (await api.LoginAsync(Coordinator)).GetBytesAsync(url)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetBytesAsync($"/api/files/{Guid.NewGuid()}")).Status);
    }

    [Fact]
    public async Task Listing_photo_file_becomes_public_only_when_published()
    {
        var (owner, reference) = await SubmittedAsync(api);
        await UploadPhotoAsync(owner, reference);
        var (_, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        var url = TestClient.Str(file!["photos"]![0], "url");
        Assert.StartsWith("/api/files/", url);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Client().GetBytesAsync(url)).Status);
        await PublishedAsync(api, owner, reference);
        var (s, bytes, type) = await api.Client().GetBytesAsync(url);
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.StartsWith("image/", type);
        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData("script.pdf", "application/pdf", "MZ\u0090\u0000 not a pdf")]          // declared PDF, executable bytes
    [InlineData("image.png", "image/png", "GIF89a not allowed")]                     // GIF content under a PNG name
    [InlineData("empty.pdf", "application/pdf", "")]
    public async Task Content_is_validated_from_its_signature_not_its_name(string name, string declared, string content)
    {
        var (owner, reference) = await SubmittedAsync(api);
        var (s, body) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents",
            Form(System.Text.Encoding.Latin1.GetBytes(content), name, declared));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Contains(TestClient.Str(body, "code"), new[] { "file_type", "file_empty" });
    }

    [Fact]
    public async Task Size_limits_come_from_configuration()
    {
        await using var factory = api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
            Microsoft.Extensions.Configuration.MemoryConfigurationBuilderExtensions.AddInMemoryCollection(cfg,
                new Dictionary<string, string?> { ["Storage:Limits:MaxDocumentBytes"] = "100" })));
        var owner = new TestClient(factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = false }));
        await owner.PhoneLoginAsync(NewPhone(), "بائع حدود");
        var (_, created) = await owner.PostAsync("/api/market/sale-requests", DeveloperDraft());
        var reference = created!["file"]!["reference"]!.GetValue<string>();
        var big = Pdf(new string('x', 200));
        var (s, body) = await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", Form(big, "big.pdf", "application/pdf"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, s);
        Assert.Equal("file_too_large", TestClient.Str(body, "code"));
    }

    [Fact]
    public async Task Lists_never_load_file_bytes()
    {
        // The metadata entity has no payload; bytes live in another table that only the content store reads.
        Assert.Null(typeof(StoredFile).GetProperty("Content"));
        var (owner, reference) = await SubmittedAsync(api);
        await owner.PostMultipartAsync($"/api/market/sale-requests/{reference}/documents", Form(Pdf("list"), "a.pdf", "application/pdf"));
        var (s, file) = await owner.GetAsync($"/api/market/sale-requests/{reference}");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.DoesNotContain("JVBER", TestClient.Raw(file)); // base64 of "%PDF" never appears in JSON
    }
}
