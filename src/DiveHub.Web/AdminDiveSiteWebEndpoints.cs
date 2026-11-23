
using System.Globalization;
using System.Net;
using DiveHub.Application;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class AdminDiveSiteWebEndpoints
{
    private const string PageUrl = "/admin/dive-sites";

    public static IEndpointRouteBuilder MapAdminDiveSiteWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PageUrl)
            .RequireAuthorization("AdminOnly");

        // CREATE
        group.MapPost("/create", async (
            HttpContext context,
            IAntiforgery antiforgery,
            AdminDiveSiteApiClient api) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid security token.");

            var form = await context.Request.ReadFormAsync();

            if (!TryReadForm(form, out var data))
                return Results.Redirect(PageUrl + "?status=invalid");

            var token = await GetTokenAsync(context);

            if (token is null)
                return Results.Redirect("/login?error=expired");

            var request = new CreateDiveSiteRequest(
                data.Name,
                data.Description,
                data.MaximumDepthMeters);

            return await SendOrErrorAsync(
                () => api.CreateAsync(token, request));
        });

        // UPDATE
        group.MapPost("/{id:int}/update", async (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            AdminDiveSiteApiClient api) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid security token.");

            var form = await context.Request.ReadFormAsync();

            if (id <= 0 || !TryReadForm(form, out var data))
                return Results.Redirect(PageUrl + "?status=invalid");

            var token = await GetTokenAsync(context);

            if (token is null)
                return Results.Redirect("/login?error=expired");

            return await SendOrErrorAsync(
                () => api.UpdateAsync(token, id, data));
        });

        // PUBLISH / UNPUBLISH
        group.MapPost("/{id:int}/publication", async (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            AdminDiveSiteApiClient api) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid security token.");

            var form = await context.Request.ReadFormAsync();

            if (id <= 0 ||
                !bool.TryParse(
                    form["IsPublished"].ToString(),
                    out var isPublished))
            {
                return Results.Redirect(PageUrl + "?status=invalid");
            }

            var token = await GetTokenAsync(context);

            if (token is null)
                return Results.Redirect("/login?error=expired");

            return await SendOrErrorAsync(
                () => api.SetPublishedAsync(token, id, isPublished));
        });

        return app;
    }

    private static async Task<string?> GetTokenAsync(
        HttpContext context)
    {
        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        return string.IsNullOrWhiteSpace(token)
            ? null
            : token;
    }

    private static bool TryReadForm(
        IFormCollection form,
        out EditDiveSiteRequest data)
    {
        var name = form["Name"].ToString().Trim();
        var description = form["Description"].ToString().Trim();
        var depthText = form["MaximumDepthMeters"].ToString();

        data = new EditDiveSiteRequest("", "", null);

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 160 ||
            description.Length > 4000)
        {
            return false;
        }

        int? depth = null;

        if (!string.IsNullOrWhiteSpace(depthText))
        {
            if (!int.TryParse(
                depthText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedDepth))
            {
                return false;
            }

            if (parsedDepth < 1 || parsedDepth > 400)
                return false;

            depth = parsedDepth;
        }

        data = new EditDiveSiteRequest(
            name,
            description,
            depth);

        return true;
    }

    private static async Task<IResult> SendOrErrorAsync(
        Func<Task<HttpStatusCode>> operation)
    {
        try
        {
            var status = await operation();

            return status switch
            {
                HttpStatusCode.OK or HttpStatusCode.Created =>
                    Results.Redirect(PageUrl + "?status=saved"),

                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    Results.Redirect("/login?error=expired"),

                HttpStatusCode.BadRequest =>
                    Results.Redirect(PageUrl + "?status=invalid"),

                HttpStatusCode.NotFound =>
                    Results.Redirect(PageUrl + "?status=notfound"),

                _ => Results.Redirect(PageUrl + "?status=error")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(PageUrl + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(PageUrl + "?status=error");
        }
    }
}
