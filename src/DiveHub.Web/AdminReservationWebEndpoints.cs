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
        if (!context.Request.HasFormContentType ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid form or security token.");
        }

        var form = await context.Request.ReadFormAsync(ct);

        if (id <= 0 ||
            !int.TryParse(
                form["InstructorId"].ToString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var instructorId) ||
            instructorId <= 0)
        {
            return Redirect("invalid");
        }

        int? boatId = null;

        var boatText = form["BoatId"].ToString();

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
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await api.ConfirmAsync(
                token, id, boatId, instructorId, ct);

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
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
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
        if (!context.Request.HasFormContentType ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid form or security token.");
        }

        var form = await context.Request.ReadFormAsync(ct);

        var reason = form["Reason"].ToString().Trim();

        if (id <= 0 ||
            reason.Length < 1 ||
            reason.Length > 1000)
        {
            return Redirect("invalid-reason");
        }

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        try
        {
            var status = await api.RejectAsync(
                token, id, reason, ct);

            return status switch
            {
                HttpStatusCode.NoContent => Redirect("rejected"),
                HttpStatusCode.Conflict => Redirect("conflict"),
                HttpStatusCode.BadRequest => Redirect("invalid-reason"),
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
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
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