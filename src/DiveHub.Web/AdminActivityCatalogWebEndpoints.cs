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

        if (!Enum.TryParse<ActivityKind>(
            Read(form, "Kind"), true, out var kind) ||
            !Enum.IsDefined(kind))
        {
            return false;
        }

        if (!decimal.TryParse(
            Read(form, "Price"),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var price))
        {
            return false;
        }

        if (!int.TryParse(
            Read(form, "MaxParticipants"),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var maxParticipants))
        {
            return false;
        }

        int? minimumAge = null;

        var minimumAgeText = Read(form, "MinimumAge");

        if (!string.IsNullOrWhiteSpace(minimumAgeText))
        {
            if (!int.TryParse(
                minimumAgeText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedAge))
            {
                return false;
            }

            minimumAge = parsedAge;
        }

        if (!bool.TryParse(
            Read(form, "RequiresBoat"),
            out var requiresBoat))
        {
            return false;
        }

        data = new SaveActivityCatalogRequest(
            Name: Read(form, "Name"),
            Description: Read(form, "Description"),
            Kind: kind,
            Price: price,
            Currency: Read(form, "Currency").ToUpperInvariant(),
            ShortDescription: Read(form, "ShortDescription"),
            Category: Read(form, "Category"),
            Country: Read(form, "Country"),
            City: Read(form, "City"),
            Location: Read(form, "Location"),
            MapLocation: Read(form, "MapLocation"),
            Duration: Read(form, "Duration"),
            MinimumAge: minimumAge,
            MinimumCertification: Read(form, "MinimumCertification"),
            MaxParticipants: maxParticipants,
            Included: Read(form, "Included"),
            NotIncluded: Read(form, "NotIncluded"),
            Requirements: Read(form, "Requirements"),
            RequiresBoat: requiresBoat);

        return ActivityCatalogRules.IsValid(data);
    }

    private static string Read(
        IFormCollection form,
        string field)
    {
        return form[field].ToString().Trim();
    }
}