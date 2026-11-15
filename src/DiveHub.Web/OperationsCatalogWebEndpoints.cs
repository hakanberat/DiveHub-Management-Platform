
using System.Globalization;
using System.Net;
using DiveHub.Application;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class OperationsCatalogWebEndpoints
{
    public static IEndpointRouteBuilder MapOperationsCatalogWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Keep existing POST routes.
        var group = app.MapGroup("/admin/operations")
            .RequireAuthorization("AdminOnly");

        // BOATS

        group.MapPost("/boats/create", (
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (!TryBoat(form, out var data))
                    return Task.FromResult(HttpStatusCode.BadRequest);

                return api.CreateBoatAsync(token, data);
            }));

        group.MapPost("/boats/{id:int}/update", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 || !TryBoat(form, out var data))
                    return Task.FromResult(HttpStatusCode.BadRequest);

                return api.UpdateBoatAsync(token, id, data);
            }));

        group.MapPost("/boats/{id:int}/active", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !bool.TryParse(
                        form["IsActive"].ToString(),
                        out var active))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.SetBoatActiveAsync(
                    token, id, active);
            }));

        // INSTRUCTORS

        group.MapPost("/instructors/create", (
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (!TryInstructor(form, out var data))
                    return Task.FromResult(HttpStatusCode.BadRequest);

                return api.CreateInstructorAsync(token, data);
            }));

        group.MapPost("/instructors/{id:int}/update", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !TryInstructor(form, out var data))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.UpdateInstructorAsync(
                    token, id, data);
            }));

        group.MapPost("/instructors/{id:int}/active", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !bool.TryParse(
                        form["IsActive"].ToString(),
                        out var active))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.SetInstructorActiveAsync(
                    token, id, active);
            }));

        group.MapPost("/instructors/{id:int}/publication", (
            int id,
            HttpContext context,
            IAntiforgery antiforgery,
            OperationsCatalogApiClient api) =>
            ProcessAsync(context, antiforgery, (form, token) =>
            {
                if (id <= 0 ||
                    !bool.TryParse(
                        form["IsPublished"].ToString(),
                        out var published))
                {
                    return Task.FromResult(HttpStatusCode.BadRequest);
                }

                return api.SetInstructorPublishedAsync(
                    token, id, published);
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

        var returnPage = GetReturnPage(context);

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var form = await context.Request.ReadFormAsync();

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

    private static string GetReturnPage(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        return path.StartsWith(
            "/admin/operations/boats/",
            StringComparison.OrdinalIgnoreCase)
                ? "/admin/boats"
                : "/admin/instructors";
    }

    private static bool TryBoat(
        IFormCollection form,
        out SaveBoatRequest result)
    {
        result = new SaveBoatRequest("", 0);

        var name = form["Name"].ToString().Trim();

        var capacityText = form["Capacity"].ToString();

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 100 ||
            !int.TryParse(
                capacityText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var capacity) ||
            capacity is < 1 or > 250)
        {
            return false;
        }

        result = new SaveBoatRequest(name, capacity);
        return true;
    }

    private static bool TryInstructor(
        IFormCollection form,
        out SaveInstructorRequest result)
    {
        result = new SaveInstructorRequest(
            "", "", "", "");

        var name = form["FullName"].ToString().Trim();
        var biography = form["Biography"].ToString().Trim();
        var agency = form["CertificationAgency"].ToString().Trim();
        var number = form["CertificationNumber"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            name.Length > 120 ||
            biography.Length > 1500 ||
            agency.Length > 100 ||
            number.Length > 100)
        {
            return false;
        }

        result = new SaveInstructorRequest(
            name,
            biography,
            agency,
            number);

        return true;
    }
}
