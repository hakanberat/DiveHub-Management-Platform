using System.Globalization;
using System.Net;
using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class AdminActivityCatalogWebEndpoints
{
    public static IEndpointRouteBuilder MapAdminActivityCatalogWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Keep existing POST addresses.
        // Only the destination after saving changes.

        var group = app.MapGroup("/admin/activity-catalog")
            .RequireAuthorization("AdminOnly");

        group.MapPost("/create", (
            HttpContext context,
            IAntiforgery antiforgery,
            AdminActivityCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (!TryReadActivity(form, out var data))
                    return Task.FromResult(HttpStatusCode.BadRequest);

                return api.CreateAsync(token, data);
            }));

        group.MapPost("/{id:int}/update", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            AdminActivityCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 || !TryReadActivity(form, out var data))
                    return Task.FromResult(HttpStatusCode.BadRequest);

                return api.UpdateAsync(token, id, data);
            }));

        group.MapPost("/{id:int}/publication", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            AdminActivityCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !bool.TryParse(
                        form["IsPublished"].ToString(),
                        out var published))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.SetPublishedAsync(
                    token, id, published);
            }));

        group.MapPost("/{id:int}/boat-requirement", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            AdminActivityCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !bool.TryParse(
                        form["RequiresBoat"].ToString(),
                        out var requiresBoat))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.SetBoatRequirementAsync(
                    token, id, requiresBoat);
            }));

        return app;
    }

    private static async Task<IResult> ProcessAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        Func<IFormCollection, string, Task<HttpStatusCode>> operation)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType)
            return Results.BadRequest("Invalid form.");

        var form = await context.Request.ReadFormAsync();

        // Only these two destinations are allowed.
        var returnPage = GetReturnPage(form);

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await operation(form, token);

            return status switch
            {
                HttpStatusCode.OK or HttpStatusCode.Created =>
                    Results.Redirect(returnPage + "?status=saved"),

                HttpStatusCode.BadRequest =>
                    Results.Redirect(returnPage + "?status=invalid"),

                HttpStatusCode.NotFound =>
                    Results.Redirect(returnPage + "?status=notfound"),

                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    Results.Redirect("/login?error=expired"),

                _ => Results.Redirect(returnPage + "?status=error")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(returnPage + "?status=error");
        }
        catch (TaskCanceledException)
        {
            return Results.Redirect(returnPage + "?status=error");
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

    private static bool TryReadActivity(
        IFormCollection form,
        out SaveActivityCatalogRequest data)
    {
        data = new SaveActivityCatalogRequest(
            "", "", ActivityKind.Dive, 0, "EUR");

        var name = form["Name"].ToString().Trim();
        var description = form["Description"].ToString().Trim();
        var kindText = form["Kind"].ToString();
        var currency = form["Currency"].ToString()
            .Trim().ToUpperInvariant();
        var priceText = form["Price"].ToString();

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 120 ||
            description.Length > 1000)
        {
            return false;
        }

        if (!Enum.TryParse<ActivityKind>(
            kindText, true, out var kind) ||
            !Enum.IsDefined(kind))
        {
            return false;
        }

        if (!decimal.TryParse(
            priceText,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var price))
        {
            return false;
        }

        if (price < 0 || price > 999999.99m)
            return false;

        if (currency.Length != 3 ||
            currency.Any(c => c < 'A' || c > 'Z'))
            return false;

        data = new SaveActivityCatalogRequest(
            name,
            description,
            kind,
            price,
            currency);

        return true;
    }
}
