
using System.Net;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DiveHub.Web;

public static class ActivityPhotoWebEndpoints
{
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxRequestBytes = 6_500_000;

    public static IEndpointRouteBuilder MapActivityPhotoWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Public image delivery.
        app.MapGet(
            "/media/activities/{activityId:int}/{photoId:long}",
            async (
                int activityId,
                long photoId,
                HttpContext context,
                ActivityPhotoApiClient api) =>
            {
                await api.ProxyPhotoAsync(
                    context, activityId, photoId, null);
            });

        // Admin preview for published and draft photos.
        app.MapGet(
            "/admin/media/activities/{activityId:int}/{photoId:long}",
            async (
                int activityId,
                long photoId,
                HttpContext context,
                ActivityPhotoApiClient api) =>
            {
                var token = await context.GetTokenAsync(
                    "DiveHubWeb", "access_token");

                if (string.IsNullOrWhiteSpace(token))
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                await api.ProxyPhotoAsync(
                    context, activityId, photoId, token);
            })
            .RequireAuthorization("AdminOnly");

        // Keep existing POST addresses.
        var admin = app.MapGroup(
            "/admin/activity-catalog/{activityId:int}/photos")
            .RequireAuthorization("AdminOnly");

        admin.MapPost("", UploadAsync)
            .WithMetadata(
                new RequestSizeLimitAttribute(MaxRequestBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = MaxRequestBytes
                });

        admin.MapPost("/{photoId:long}/delete", DeleteAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        int activityId,
        HttpContext context,
        IAntiforgery antiforgery,
        ActivityPhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType ||
            context.Request.ContentLength is > MaxRequestBytes)
        {
            return Results.BadRequest("Invalid upload request.");
        }

        var form = await context.Request.ReadFormAsync();

        var returnPage = GetReturnPage(form);

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        var file = form.Files.GetFile("Photo");
        var altText = form["AltText"].ToString().Trim();

        if (activityId <= 0 ||
            file is null ||
            file.Length <= 0 ||
            file.Length > MaxPhotoBytes ||
            string.IsNullOrWhiteSpace(altText) ||
            altText.Length > 300)
        {
            return Results.Redirect(
                returnPage + "?status=invalid");
        }

        try
        {
            var result = await api.UploadAsync(
                token, activityId, file, altText);

            return Results.Redirect(result switch
            {
                HttpStatusCode.Created =>
                    returnPage + "?status=saved",

                HttpStatusCode.BadRequest or
                HttpStatusCode.Conflict =>
                    returnPage + "?status=invalid",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => returnPage + "?status=error"
            });
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                returnPage + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(
                returnPage + "?status=error");
        }
    }

    private static async Task<IResult> DeleteAsync(
        int activityId,
        long photoId,
        HttpContext context,
        IAntiforgery antiforgery,
        ActivityPhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType)
            return Results.BadRequest("Invalid form.");

        var form = await context.Request.ReadFormAsync();

        var returnPage = GetReturnPage(form);

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var result = await api.DeleteAsync(
                token, activityId, photoId);

            return Results.Redirect(result switch
            {
                HttpStatusCode.NoContent =>
                    returnPage + "?status=saved",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => returnPage + "?status=error"
            });
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                returnPage + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(
                returnPage + "?status=error");
        }
    }

    private static string GetReturnPage(IFormCollection form)
    {
        return string.Equals(
            form["ReturnTo"].ToString(),
            "courses",
            StringComparison.OrdinalIgnoreCase)
                ? "/admin/courses"
                : "/admin/diving-activities";
    }
}
