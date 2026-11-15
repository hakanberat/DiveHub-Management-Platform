
using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class DiveSitePhotoEndpoints
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxPhotosPerSite = 20;

    public static IEndpointRouteBuilder MapDiveSitePhotoEndpoints(
        this IEndpointRouteBuilder app)
    {
        var publicPhotos = app.MapGroup(
            "/api/public/dive-sites/{siteId:int}/photos");

        publicPhotos.MapGet("", async (
            int siteId,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            var published = await db.DiveSites
                .AnyAsync(x => x.Id == siteId &&
                               x.IsPublished, ct);

            if (!published)
                return Results.NotFound();

            return Results.Ok(await ListAsync(
                db, siteId, ct));
        });

        publicPhotos.MapGet("/{photoId:long}/file", async (
            int siteId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            CancellationToken ct) =>
        {
            var published = await db.DiveSites.AnyAsync(
                x => x.Id == siteId && x.IsPublished, ct);

            if (!published)
                return Results.NotFound();

            return await GetFileAsync(
                siteId, photoId, db, storage, ct);
        });

        var adminPhotos = app.MapGroup(
            "/api/admin/dive-sites/{siteId:int}/photos")
            .RequireAuthorization("AdminOnly");

        adminPhotos.MapGet("", async (
            int siteId,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.DiveSites
                .AnyAsync(x => x.Id == siteId, ct))
            {
                return Results.NotFound();
            }

            return Results.Ok(await ListAsync(
                db, siteId, ct));
        });

        adminPhotos.MapGet("/{photoId:long}/file", async (
            int siteId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            CancellationToken ct) =>
                await GetFileAsync(
                    siteId, photoId, db, storage, ct));

        adminPhotos.MapPost("", UploadAsync)
            .RequireRateLimiting("auth")
            .WithMetadata(
                new RequestSizeLimitAttribute(6_500_000),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = 6_500_000
                });

        adminPhotos.MapDelete("/{photoId:long}", async (
            int siteId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var photo = await db.MediaAssets.SingleOrDefaultAsync(
                x => x.Id == photoId &&
                     x.DiveSiteId == siteId, ct);

            if (photo is null)
                return Results.NotFound();

            db.MediaAssets.Remove(photo);
            await db.SaveChangesAsync(ct);

            try
            {
                await storage.DeleteAsync(
                    photo.RelativePath, ct);
            }
            catch (IOException ex)
            {
                loggerFactory.CreateLogger(
                    "DiveSitePhotos").LogError(
                    ex,
                    "Orphaned media file after deleting photo {PhotoId}",
                    photoId);
            }

            return Results.NoContent();
        });

        return app;
    }

    private static async Task<IResult> UploadAsync(
        int siteId,
        HttpContext context,
        DiveHubDbContext db,
        IPhotoStorage storage,
        CancellationToken ct)
    {
        if (siteId <= 0 ||
            !context.Request.HasFormContentType ||
            context.Request.ContentLength is > 6_500_000)
        {
            return Results.BadRequest(
                new { error = "Invalid upload request." });
        }

        var siteExists = await db.DiveSites
            .AnyAsync(x => x.Id == siteId, ct);

        if (!siteExists)
            return Results.NotFound();

        var photoCount = await db.MediaAssets
            .CountAsync(x => x.DiveSiteId == siteId, ct);

        if (photoCount >= MaxPhotosPerSite)
        {
            return Results.Conflict(
                new { error = "Maximum 20 photos per dive site." });
        }

        var form = await context.Request.ReadFormAsync(ct);

        var file = form.Files.GetFile("Photo");
        var altText = form["AltText"].ToString().Trim();

        if (file is null ||
            file.Length <= 0 ||
            file.Length > MaxPhotoBytes ||
            string.IsNullOrWhiteSpace(altText) ||
            altText.Length > 300)
        {
            return Results.BadRequest(
                new { error = "Invalid file or description." });
        }

        StoredPhoto stored;

        try
        {
            await using var input = file.OpenReadStream();

            stored = await storage.SaveAsync(
                siteId,
                file.FileName,
                input,
                file.Length,
                ct);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest(
                new { error = "Invalid or unsupported image." });
        }

        var photo = new MediaAsset
        {
            DiveSiteId = siteId,
            FileName = stored.FileName,
            RelativePath = stored.RelativePath,
            AltText = altText,
            ContentType = "image/webp",
            UploadedAtUtc = DateTime.UtcNow
        };

        db.MediaAssets.Add(photo);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(
                stored.RelativePath, CancellationToken.None);

            throw;
        }

        return Results.Created(
            $"/api/admin/dive-sites/{siteId}/photos",
            ToDto(photo));
    }

    private static async Task<IResult> GetFileAsync(
        int siteId,
        long photoId,
        DiveHubDbContext db,
        IPhotoStorage storage,
        CancellationToken ct)
    {
        var photo = await db.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == photoId &&
                     x.DiveSiteId == siteId, ct);

        if (photo is null)
            return Results.NotFound();

        var stream = await storage.OpenReadAsync(
            photo.RelativePath, ct);

        if (stream is null)
            return Results.NotFound();

        return Results.File(
            stream,
            contentType: "image/webp",
            enableRangeProcessing: true);
    }

    private static async Task<List<DiveSitePhotoDto>> ListAsync(
        DiveHubDbContext db,
        int siteId,
        CancellationToken ct)
    {
        var photos = await db.MediaAssets
            .AsNoTracking()
            .Where(x => x.DiveSiteId == siteId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        return photos.Select(ToDto).ToList();
    }

    private static DiveSitePhotoDto ToDto(MediaAsset photo)
    {
        return new DiveSitePhotoDto(
            photo.Id,
            photo.DiveSiteId!.Value,
            photo.AltText);
    }
}
