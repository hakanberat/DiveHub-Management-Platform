using System.Net;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DiveHub.Web;

public static class DiveSitePhotoWebEndpoints
{
    private const string ReturnUrl = "/admin/dive-sites";
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    public static IEndpointRouteBuilder MapDiveSitePhotoWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Public image delivery.
        app.MapGet(
            "/media/dive-sites/{siteId:int}/{photoId:long}",
            async (
                int siteId,
                long photoId,
                HttpContext context,
                DiveSitePhotoApiClient api) =>
            {
                await api.ProxyPhotoAsync(
                    context, siteId, photoId, null);
            });

        // Admin preview, including unpublished photos.
        app.MapGet(
            "/admin/media/dive-sites/{siteId:int}/{photoId:long}",
            async (
                int siteId,
                long photoId,
                HttpContext context,
                DiveSitePhotoApiClient api) =>
            {
                var token = await context.GetTokenAsync(
                    "DiveHubWeb", "access_token");

                if (string.IsNullOrWhiteSpace(token))
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                await api.ProxyPhotoAsync(
                    context, siteId, photoId, token);
            })
            .RequireAuthorization("AdminOnly");

        var admin = app.MapGroup(
            "/admin/dive-sites/{siteId:int}/photos")
            .RequireAuthorization("AdminOnly");

        admin.MapPost("", UploadAsync)
            .WithMetadata(
                new RequestSizeLimitAttribute(6_500_000),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = 6_500_000
                });

        admin.MapPost("/{photoId:long}/delete", DeleteAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        int siteId,
        HttpContext context,
        IAntiforgery antiforgery,
        DiveSitePhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType ||
            context.Request.ContentLength is > 6_500_000)
        {
            return Results.Redirect(ReturnUrl + "?status=invalid");
        }

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        var form = await context.Request.ReadFormAsync();

        var file = form.Files.GetFile("Photo");
        var altText = form["AltText"].ToString().Trim();

        if (siteId <= 0 ||
            file is null ||
            file.Length <= 0 ||
            file.Length > MaxPhotoBytes ||
            string.IsNullOrWhiteSpace(altText) ||
            altText.Length > 300)
        {
            return Results.Redirect(ReturnUrl + "?status=invalid");
        }

        try
        {
            var result = await api.UploadAsync(
                token, siteId, file, altText);

            return Results.Redirect(result switch
            {
                HttpStatusCode.Created =>
                    ReturnUrl + "?status=photo-saved",

                HttpStatusCode.BadRequest =>
                    ReturnUrl + "?status=invalid",

                HttpStatusCode.Conflict =>
                    ReturnUrl + "?status=limit",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => ReturnUrl + "?status=error"
            });
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(ReturnUrl + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(ReturnUrl + "?status=error");
        }
    }

    private static async Task<IResult> DeleteAsync(
        int siteId,
        long photoId,
        HttpContext context,
        IAntiforgery antiforgery,
        DiveSitePhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var result = await api.DeleteAsync(
                token, siteId, photoId);

            return Results.Redirect(result switch
            {
                HttpStatusCode.NoContent =>
                    ReturnUrl + "?status=photo-deleted",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => ReturnUrl + "?status=error"
            });
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(ReturnUrl + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(ReturnUrl + "?status=error");
        }
    }
}
