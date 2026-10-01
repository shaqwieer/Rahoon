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
/// a private document — its owner, and team members holding documents.read for that request (all work, or the request is
/// assigned to them); a listing photo — its owner and members holding market.view for that request, plus anyone while the
/// photo is approved and shown in a published opportunity. Anything else answers 404 (never 403, so ids reveal nothing).
/// Permissions are re-read on every request, so a suspended member or a removed grant stops downloads at once.
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
                .Select(p => new { p.Id, p.ApplicantUserId, p.SaleRequestId, p.RemovedAt, p.ReviewStatus }).FirstOrDefaultAsync();
            var doc = photo is null
                ? await db.PrivateDocuments.AsNoTracking().Where(d => d.FileId == fileId).Select(d => new { d.ApplicantUserId, d.SaleRequestId }).FirstOrDefaultAsync()
                : null;
            if (photo is null && doc is null) throw new NotFoundException();

            var applicant = photo?.ApplicantUserId ?? doc!.ApplicantUserId;
            var requestId = photo?.SaleRequestId ?? doc!.SaleRequestId;
            var signedIn = rc.IsAuthenticated && rc.Stage == SessionStage.Active;
            var teamMay = false;
            if (signedIn && rc.IsOperator)
            {
                var assignee = await db.SaleRequests.Where(r => r.Id == requestId).Select(r => r.AssignedToUserId).FirstOrDefaultAsync();
                // A document needs documents.read; a photo needs market.view. Each with its own scope, never the other's.
                teamMay = rc.CanOn(P.MarketView, assignee) && rc.CanOn(photo is null ? P.DocumentsRead : P.MarketView, assignee);
            }
            allowed = signedIn && ((rc.IsIndividual && rc.UserId == applicant) || teamMay);
            if (!allowed && photo is { RemovedAt: null, ReviewStatus: FileReviewStatus.Accepted })
            {
                isPublic = await db.Opportunities.AnyAsync(o => o.Status == OpportunityStatus.Published && o.PhotoIds.Contains(photo.Id));
                allowed = isPublic;
            }
        }
        if (!allowed) throw new NotFoundException();

        var read = await files.ReadAsync(fileId) ?? throw new NotFoundException();
        http.Response.Headers.ContentDisposition = $"inline; filename*=UTF-8''{Uri.EscapeDataString(read.File.FileName)}";
        // Private bytes are never kept by shared caches or the browser after access is revoked.
        if (!isPublic) http.Response.Headers.CacheControl = "no-store, private";
        if (isPublic)
        {
            http.Response.Headers.CacheControl = "public, max-age=600";
            http.Response.Headers.Remove("X-Robots-Tag");
        }
        return Results.Bytes(read.Content, read.File.ContentType);
    }
}
