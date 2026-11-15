
using System.Globalization;
using System.Net;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class AdminReservationWebEndpoints
{
    public static IEndpointRouteBuilder MapAdminReservationWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/reservations")
            .RequireAuthorization("AdminOnly");

        group.MapPost("/{id:long}/confirm", ConfirmAsync);
        group.MapPost("/{id:long}/reject", RejectAsync);

        return app;
    }

    private static async Task<IResult> ConfirmAsync(
        long id,
        HttpContext context,
        IAntiforgery antiforgery,
        ReservationAdminApiClient api,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType)
            return Results.BadRequest("Invalid form.");

        var form = await context.Request.ReadFormAsync(ct);

        var instructorText = form["InstructorId"].ToString();
        var boatText = form["BoatId"].ToString();

        if (id <= 0 ||
            !int.TryParse(
                instructorText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var instructorId) ||
            instructorId <= 0)
        {
            return Redirect("invalid");
        }

        int? boatId = null;

        if (!string.IsNullOrWhiteSpace(boatText))
        {
            if (!int.TryParse(
                boatText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsedBoatId) ||
                parsedBoatId <= 0)
            {
                return Redirect("invalid");
            }

            boatId = parsedBoatId;
        }

        var token = await context.GetTokenAsync(
            "DiveHubWeb",
            "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await api.ConfirmAsync(
                token,
                id,
                boatId,
                instructorId,
                ct);

            return status switch
            {
                HttpStatusCode.OK => Redirect("confirmed"),

                HttpStatusCode.Conflict => Redirect("conflict"),

                HttpStatusCode.BadRequest => Redirect("invalid"),

                HttpStatusCode.NotFound => Redirect("notfound"),

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    Results.Redirect("/login?error=expired"),

                _ => Redirect("error")
            };
        }
        catch (HttpRequestException)
        {
            return Redirect("error");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Redirect("error");
        }
    }

    private static async Task<IResult> RejectAsync(
        long id,
        HttpContext context,
        IAntiforgery antiforgery,
        ReservationAdminApiClient api,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (id <= 0)
            return Redirect("invalid");

        var token = await context.GetTokenAsync(
            "DiveHubWeb",
            "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await api.RejectAsync(
                token,
                id,
                ct);

            return status switch
            {
                HttpStatusCode.NoContent => Redirect("rejected"),

                HttpStatusCode.Conflict => Redirect("conflict"),

                HttpStatusCode.NotFound => Redirect("notfound"),

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    Results.Redirect("/login?error=expired"),

                _ => Redirect("error")
            };
        }
        catch (HttpRequestException)
        {
            return Redirect("error");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Redirect("error");
        }
    }

    private static IResult Redirect(string status)
    {
        return Results.Redirect(
            "/admin/reservations?status=" + status);
    }
}
