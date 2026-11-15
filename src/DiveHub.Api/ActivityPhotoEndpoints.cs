
using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class ActivityPhotoEndpoints
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const int MaxPhotos = 20;

    public static IEndpointRouteBuilder MapActivityPhotoEndpoints(
        this IEndpointRouteBuilder app)
    {
        var publicPhotos = app.MapGroup(
            "/api/public/activities/{activityId:int}/photos");

        publicPhotos.MapGet("", async (
            int activityId,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            var published = await db.Activities.AnyAsync(
                x => x.Id == activityId && x.IsPublished, ct);

            if (!published)
                return Results.NotFound();

            return Results.Ok(
                await ListAsync(db, activityId, ct));
        });

        publicPhotos.MapGet("/{photoId:long}/file", async (
            int activityId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            CancellationToken ct) =>
        {
            var published = await db.Activities.AnyAsync(
                x => x.Id == activityId && x.IsPublished, ct);

            if (!published)
                return Results.NotFound();

            return await GetFileAsync(
                activityId, photoId, db, storage, ct);
        });

        var admin = app.MapGroup(
            "/api/admin/activities/{activityId:int}/photos")
            .RequireAuthorization("AdminOnly");

        admin.MapGet("", async (
            int activityId,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            var exists = await db.Activities.AnyAsync(
                x => x.Id == activityId, ct);

            if (!exists)
                return Results.NotFound();

            return Results.Ok(
                await ListAsync(db, activityId, ct));
        });

        admin.MapGet("/{photoId:long}/file", async (
            int activityId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            CancellationToken ct) =>
                await GetFileAsync(
                    activityId, photoId, db, storage, ct));

        admin.MapPost("", UploadAsync)
            .RequireRateLimiting("auth")
            .WithMetadata(
                new RequestSizeLimitAttribute(6_500_000),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = 6_500_000
                });

        admin.MapDelete("/{photoId:long}", async (
            int activityId,
            long photoId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var photo = await db.MediaAssets
                .SingleOrDefaultAsync(
                    x => x.Id == photoId &&
                         x.ActivityId == activityId, ct);

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
                    "ActivityPhotos").LogError(
                    ex,
                    "Orphaned photo file: {PhotoId}",
                    photoId);
            }

            return Results.NoContent();
        });

        return app;
    }

    private static async Task<IResult> UploadAsync(
        int activityId,
        HttpContext context,
        DiveHubDbContext db,
        IPhotoStorage storage,
        CancellationToken ct)
    {
        if (activityId <= 0 ||
            !context.Request.HasFormContentType ||
            context.Request.ContentLength is > 6_500_000)
        {
            return Results.BadRequest(
                new { error = "Invalid upload request." });
        }

        var exists = await db.Activities.AnyAsync(
            x => x.Id == activityId, ct);

        if (!exists)
            return Results.NotFound();

        var count = await db.MediaAssets.CountAsync(
            x => x.ActivityId == activityId, ct);

        if (count >= MaxPhotos)
        {
            return Results.Conflict(
                new { error = "Maximum 20 photos." });
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
                new { error = "Invalid photo information." });
        }

        StoredPhoto stored;

        try
        {
            await using var input = file.OpenReadStream();

            stored = await storage.SaveActivityAsync(
                activityId,
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
            ActivityId = activityId,
            DiveSiteId = null,
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
            $"/api/admin/activities/{activityId}/photos",
            ToDto(photo));
    }

    private static async Task<IResult> GetFileAsync(
        int activityId,
        long photoId,
        DiveHubDbContext db,
        IPhotoStorage storage,
        CancellationToken ct)
    {
        var photo = await db.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == photoId &&
                     x.ActivityId == activityId, ct);

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

    private static async Task<List<ActivityPhotoDto>> ListAsync(
        DiveHubDbContext db,
        int activityId,
        CancellationToken ct)
    {
        var photos = await db.MediaAssets
            .AsNoTracking()
            .Where(x => x.ActivityId == activityId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

        return photos.Select(ToDto).ToList();
    }

    private static ActivityPhotoDto ToDto(MediaAsset photo)
    {
        return new ActivityPhotoDto(
            photo.Id,
            photo.ActivityId!.Value,
            photo.AltText);
    }
}
