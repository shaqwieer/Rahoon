using Microsoft.EntityFrameworkCore;
using Rahoon.Api.Infrastructure.Http;
using Rahoon.Api.Infrastructure.Persistence;
using Rahoon.Api.Infrastructure.Storage;
using Rahoon.Api.Infrastructure.Tenancy;
using Rahoon.Api.Modules.Identity;
using Rahoon.Api.Modules.Market;

namespace Rahoon.Api.Modules.Files;

/// <summary>
/// The stable address of every stored file: GET /api/files/{fileId}. Whatever provider holds the bytes, clients only ever
/// see this URL. Access is decided from what the file is attached to:
/// a private document — its owner and the Rahoon team (market.view); a listing photo — the same, plus anyone while the photo
/// is approved and shown in a published opportunity. Anything else answers 404 (never 403, so ids reveal nothing).
/// </summary>
public static class FileEndpoints
{
    public static string Url(Guid fileId) => $"/api/files/{fileId}";

    public static void Map(IEndpointRouteBuilder app) => app.MapGet("/api/files/{fileId:guid}", Get);

    private static async Task<IResult> Get(Guid fileId, RahoonDbContext db, RequestContext rc, FileStore files, HttpContext http)
    {
        bool allowed, isPublic = false;
        using (rc.BeginSystemScope())
        {
            var photo = await db.ListingPhotos.AsNoTracking().Where(p => p.FileId == fileId)
                .Select(p => new { p.Id, p.ApplicantUserId, p.RemovedAt, p.ReviewStatus }).FirstOrDefaultAsync();
            var doc = photo is null
                ? await db.PrivateDocuments.AsNoTracking().Where(d => d.FileId == fileId).Select(d => new { d.ApplicantUserId }).FirstOrDefaultAsync()
                : null;
            if (photo is null && doc is null) throw new NotFoundException();

            var applicant = photo?.ApplicantUserId ?? doc!.ApplicantUserId;
            var signedIn = rc.IsAuthenticated && rc.Stage == SessionStage.Active;
            allowed = signedIn && ((rc.IsIndividual && rc.UserId == applicant) || (rc.IsOperator && rc.Has(P.MarketView)));
            if (!allowed && photo is { RemovedAt: null, ReviewStatus: FileReviewStatus.Accepted })
            {
                isPublic = await db.Opportunities.AnyAsync(o => o.Status == OpportunityStatus.Published && o.PhotoIds.Contains(photo.Id));
                allowed = isPublic;
            }
        }
        if (!allowed) throw new NotFoundException();

        var read = await files.ReadAsync(fileId) ?? throw new NotFoundException();
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(read.File.FileName)}";
        if (isPublic)
        {
            http.Response.Headers.CacheControl = "public, max-age=600";
            http.Response.Headers.Remove("X-Robots-Tag");
        }
        return Results.Bytes(read.Content, read.File.ContentType);
    }
}
