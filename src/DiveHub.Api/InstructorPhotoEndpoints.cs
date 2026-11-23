
using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class InstructorPhotoEndpoints
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxRequestBytes = 6_500_000;

    // ==================================================
    // ENDPOINT REGISTRATION
    // ==================================================

    public static IEndpointRouteBuilder MapInstructorPhotoEndpoints(
        this IEndpointRouteBuilder app)
    {
        // PUBLIC: Only active and published instructors.

        app.MapGet(
            "/api/public/instructors/{instructorId:int}/photo",
            async Task<IResult> (
                int instructorId,
                DiveHubDbContext db,
                CancellationToken ct) =>
            {
                if (!await IsPublicAsync(
                    db, instructorId, ct))
                {
                    return Results.NotFound();
                }

                return await GetMetadataAsync(
                    db, instructorId, ct);
            });

        app.MapGet(
            "/api/public/instructors/{instructorId:int}/photo/file",
            async Task<IResult> (
                int instructorId,
                DiveHubDbContext db,
                IPhotoStorage storage,
                CancellationToken ct) =>
            {
                if (!await IsPublicAsync(
                    db, instructorId, ct))
                {
                    return Results.NotFound();
                }

                return await GetFileAsync(
                    db, storage, instructorId, ct);
            });

        // ADMIN: Includes unpublished and inactive profiles.

        var admin = app.MapGroup(
            "/api/admin/instructors/{instructorId:int}/photo")
            .RequireAuthorization("AdminOnly");

        admin.MapGet("", async Task<IResult> (
            int instructorId,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            if (!await db.StaffMembers.AnyAsync(
                x => x.Id == instructorId, ct))
            {
                return Results.NotFound();
            }

            return await GetMetadataAsync(
                db, instructorId, ct);
        });

        admin.MapGet("/file", async (
            int instructorId,
            DiveHubDbContext db,
            IPhotoStorage storage,
            CancellationToken ct) =>
        {
            return await GetFileAsync(
                db, storage, instructorId, ct);
        });

        admin.MapPost("", UploadAsync)
            .RequireRateLimiting("auth")
            .WithMetadata(
                new RequestSizeLimitAttribute(MaxRequestBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = MaxRequestBytes
                });

        admin.MapDelete("", DeleteAsync);

        return app;
    }

    // ==================================================
    // UPLOAD OR REPLACE INSTRUCTOR PHOTO
    // ==================================================

    private static async Task<IResult> UploadAsync(
        int instructorId,
        HttpContext context,
        DiveHubDbContext db,
        IPhotoStorage storage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(
            "InstructorPhotoEndpoints");

        // Validate request.

        if (instructorId <= 0 ||
            !context.Request.HasFormContentType ||
            context.Request.ContentLength is > MaxRequestBytes)
        {
            return Results.BadRequest(
                new { error = "Invalid upload request." });
        }

        // Check instructor existence.

        bool instructorExists =
            await db.StaffMembers.AnyAsync(
                x => x.Id == instructorId, ct);

        if (!instructorExists)
        {
            return Results.NotFound();
        }

        // Read form data.

        var form = await context.Request.ReadFormAsync(ct);

        var file = form.Files.GetFile("Photo");

        var altText = form["AltText"].ToString().Trim();

        // Validate photo and description.

        if (file is null ||
            file.Length <= 0 ||
            file.Length > MaxPhotoBytes ||
            string.IsNullOrWhiteSpace(altText) ||
            altText.Length > 300)
        {
            return Results.BadRequest(
                new { error = "Invalid image or description." });
        }

        // Save and process uploaded image.

        StoredPhoto stored;

        try
        {
            await using var input = file.OpenReadStream();

            stored = await storage.SaveInstructorAsync(
                instructorId,
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

        // Find existing instructor photo.

        var photo = await db.MediaAssets
            .SingleOrDefaultAsync(
                x => x.StaffMemberId == instructorId,
                ct);

        string? previousPath = photo?.RelativePath;

        // Create new photo record or update existing one.

        if (photo is null)
        {
            photo = new MediaAsset
            {
                StaffMemberId = instructorId,
                DiveSiteId = null,
                ActivityId = null,
                FileName = stored.FileName,
                RelativePath = stored.RelativePath,
                AltText = altText,
                ContentType = "image/webp",
                UploadedAtUtc = DateTime.UtcNow
            };

            db.MediaAssets.Add(photo);
        }
        else
        {
            photo.FileName = stored.FileName;
            photo.RelativePath = stored.RelativePath;
            photo.AltText = altText;
            photo.ContentType = "image/webp";
            photo.UploadedAtUtc = DateTime.UtcNow;
        }

        // Save database changes.

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await storage.DeleteAsync(
                stored.RelativePath,
                CancellationToken.None);

            logger.LogWarning(
                ex,
                "Photo database update conflict for instructor {Id}",
                instructorId);

            return Results.Conflict(
                new
                {
                    error = "Photo could not be saved. Retry."
                });
        }
        catch
        {
            await storage.DeleteAsync(
                stored.RelativePath,
                CancellationToken.None);

            throw;
        }

        // Delete previous image after database update.

        if (previousPath is not null &&
            previousPath != stored.RelativePath)
        {
            try
            {
                await storage.DeleteAsync(
                    previousPath,
                    CancellationToken.None);
            }
            catch (IOException ex)
            {
                logger.LogWarning(
                    ex,
                    "Old photo cleanup failed for instructor {Id}",
                    instructorId);
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning(
                    ex,
                    "Old photo cleanup access denied for instructor {Id}",
                    instructorId);
            }
        }

        return Results.Ok(
            new InstructorPhotoDto(
                photo.Id,
                instructorId,
                photo.AltText));
    }

    // ==================================================
    // DELETE INSTRUCTOR PHOTO
    // ==================================================

    private static async Task<IResult> DeleteAsync(
        int instructorId,
        DiveHubDbContext db,
        IPhotoStorage storage,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(
            "InstructorPhotoEndpoints");

        var photo = await db.MediaAssets
            .SingleOrDefaultAsync(
                x => x.StaffMemberId == instructorId,
                ct);

        if (photo is null)
        {
            return Results.NotFound();
        }

        var path = photo.RelativePath;

        // First remove the database record.

        db.MediaAssets.Remove(photo);

        await db.SaveChangesAsync(ct);

        // Then remove the physical file.

        try
        {
            await storage.DeleteAsync(
                path,
                CancellationToken.None);
        }
        catch (IOException ex)
        {
            logger.LogWarning(
                ex,
                "Photo file cleanup failed for instructor {Id}",
                instructorId);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(
                ex,
                "Photo file cleanup access denied for instructor {Id}",
                instructorId);
        }

        return Results.NoContent();
    }

    // ==================================================
    // GET PHOTO METADATA
    // ==================================================

    private static async Task<IResult> GetMetadataAsync(
        DiveHubDbContext db,
        int instructorId,
        CancellationToken ct)
    {
        var photo = await db.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.StaffMemberId == instructorId,
                ct);

        if (photo is null)
        {
            return Results.NoContent();
        }

        return Results.Ok(
            new InstructorPhotoDto(
                photo.Id,
                instructorId,
                photo.AltText));
    }

    // ==================================================
    // GET PHOTO FILE
    // ==================================================

    private static async Task<IResult> GetFileAsync(
        DiveHubDbContext db,
        IPhotoStorage storage,
        int instructorId,
        CancellationToken ct)
    {
        var photo = await db.MediaAssets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.StaffMemberId == instructorId,
                ct);

        if (photo is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(
            photo.RelativePath,
            ct);

        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.File(
            stream,
            contentType: "image/webp");
    }

    // ==================================================
    // CHECK PUBLIC VISIBILITY
    // ==================================================

    private static Task<bool> IsPublicAsync(
        DiveHubDbContext db,
        int instructorId,
        CancellationToken ct)
    {
        return db.StaffMembers.AnyAsync(
            x => x.Id == instructorId &&
                 x.IsActive &&
                 x.IsPublished,
            ct);
    }
}
