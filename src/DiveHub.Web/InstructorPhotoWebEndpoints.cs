
using System.Net;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace DiveHub.Web;

public static class InstructorPhotoWebEndpoints
{
    private const string ReturnUrl = "/admin/instructors";
    private const long MaxPhotoBytes = 5 * 1024 * 1024;
    private const long MaxRequestBytes = 6_500_000;

    public static IEndpointRouteBuilder MapInstructorPhotoWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Public photo delivery.
        app.MapGet(
            "/media/instructors/{instructorId:int}",
            async (
                int instructorId,
                HttpContext context,
                InstructorPhotoApiClient api) =>
            {
                await api.ProxyAsync(
                    context, instructorId, null);
            });

        // Admin preview, including draft profiles.
        app.MapGet(
            "/admin/media/instructors/{instructorId:int}",
            async (
                int instructorId,
                HttpContext context,
                InstructorPhotoApiClient api) =>
            {
                var token = await context.GetTokenAsync(
                    "DiveHubWeb", "access_token");

                if (string.IsNullOrWhiteSpace(token))
                {
                    context.Response.StatusCode = 401;
                    return;
                }

                await api.ProxyAsync(
                    context, instructorId, token);
            })
            .RequireAuthorization("AdminOnly");

        var group = app.MapGroup(
            "/admin/operations/instructors/{instructorId:int}/photo")
            .RequireAuthorization("AdminOnly");

        group.MapPost("", UploadAsync)
            .WithMetadata(
                new RequestSizeLimitAttribute(MaxRequestBytes),
                new RequestFormLimitsAttribute
                {
                    MultipartBodyLengthLimit = MaxRequestBytes
                });

        group.MapPost("/delete", DeleteAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        int instructorId,
        HttpContext context,
        IAntiforgery antiforgery,
        InstructorPhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (instructorId <= 0 ||
            !context.Request.HasFormContentType ||
            context.Request.ContentLength is > MaxRequestBytes)
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

        if (file is null ||
            file.Length <= 0 ||
            file.Length > MaxPhotoBytes ||
            string.IsNullOrWhiteSpace(altText) ||
            altText.Length > 300)
        {
            return Results.Redirect(ReturnUrl + "?status=invalid");
        }

        try
        {
            var status = await api.UploadAsync(
                token, instructorId, file, altText);

            return Results.Redirect(status switch
            {
                HttpStatusCode.OK =>
                    ReturnUrl + "?status=saved#instructors",

                HttpStatusCode.BadRequest =>
                    ReturnUrl + "?status=invalid#instructors",

                HttpStatusCode.Conflict =>
                    ReturnUrl + "?status=error#instructors",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => ReturnUrl + "?status=error#instructors"
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
        int instructorId,
        HttpContext context,
        IAntiforgery antiforgery,
        InstructorPhotoApiClient api)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await api.DeleteAsync(
                token, instructorId);

            return Results.Redirect(status switch
            {
                HttpStatusCode.NoContent =>
                    ReturnUrl + "?status=saved#instructors",

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    "/login?error=expired",

                _ => ReturnUrl + "?status=error#instructors"
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
