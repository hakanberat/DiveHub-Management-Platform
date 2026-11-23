using System.Globalization;
using System.Net;
using DiveHub.Application;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class CustomerBookingWebEndpoints
{
    public static IEndpointRouteBuilder MapCustomerBookingWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/book/create", CreateAsync)
            .RequireAuthorization("CustomerOnly");

        var notifications = app.MapGroup("/customer/notifications")
            .RequireAuthorization("CustomerOnly");

        notifications.MapPost("/{id:long}/read", ReadAsync);
        notifications.MapPost("/read-all", ReadAllAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CustomerBookingApiClient api,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!context.Request.HasFormContentType ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid form or security token.");
        }

        var form = await context.Request.ReadFormAsync(ct);

        if (!int.TryParse(
                form["ActivityId"].ToString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var activityId) ||
            activityId <= 0)
        {
            return Invalid(0, "activity");
        }

        if (!DateOnly.TryParseExact(
                form["ActivityDate"].ToString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var activityDate))
        {
            return Invalid(activityId, "date");
        }

        var earliestDate = BookingRules.GetEarliestDate(
            configuration["DiveCenter:TimeZoneId"]);

        if (activityDate < earliestDate)
            return Invalid(activityId, "date");

        if (!int.TryParse(
                form["ParticipantCount"].ToString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var participants) ||
            participants < 1 ||
            participants >
                ActivityCatalogRules.MaximumAllowedParticipants)
        {
            return Invalid(activityId, "participants");
        }

        var notes = form["CustomerNotes"].ToString().Trim();
        var name = form["CustomerFullName"].ToString().Trim();
        var email = form["ContactEmail"].ToString().Trim();
        var phone = form["ContactPhone"].ToString().Trim();
        var whatsapp = form["ContactWhatsApp"].ToString().Trim();

        var byEmail = IsChecked(form, "ContactByEmail");
        var byPhone = IsChecked(form, "ContactByPhone");
        var byWhatsApp = IsChecked(form, "ContactByWhatsApp");

        if (notes.Length > 2000)
            return Invalid(activityId, "notes");

        var contactError = BookingRules.ValidateContact(
            name,
            email,
            phone,
            whatsapp,
            byEmail,
            byPhone,
            byWhatsApp);

        if (contactError is not null)
        {
            return Invalid(
                activityId,
                GetContactErrorField(contactError));
        }

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/customer/login?error=expired");

        var request = new CreateCustomerBookingRequest(
            activityId,
            activityDate,
            participants,
            notes,
            name,
            email,
            phone,
            string.IsNullOrWhiteSpace(whatsapp)
                ? null : whatsapp,
            byEmail,
            byPhone,
            byWhatsApp);

        try
        {
            var activities = await api.GetActivitiesAsync(ct);

            var activity = activities.FirstOrDefault(
                x => x.Id == activityId);

            if (activity is null)
                return Invalid(activityId, "activity");

            if (!ActivityCatalogRules.HasValidCapacity(
                    activity.MaxParticipants) ||
                participants > activity.MaxParticipants)
            {
                return Invalid(activityId, "participants");
            }

            var result = await api.CreateAsync(
                token, request, ct);

            return result switch
            {
                HttpStatusCode.Created =>
                    Results.Redirect("/my-reservations?status=created"),

                HttpStatusCode.BadRequest =>
                    Invalid(activityId, "server"),

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    Results.Redirect("/customer/login?error=expired"),

                _ => Results.Redirect(
                    $"/book?activityId={activityId}&status=error")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                $"/book?activityId={activityId}&status=error");
        }
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return Results.Redirect(
                $"/book?activityId={activityId}&status=error");
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Redirect(
                $"/book?activityId={activityId}&status=error");
        }
    }

    private static Task<IResult> ReadAsync(
        long id,
        HttpContext context,
        IAntiforgery antiforgery,
        CustomerBookingApiClient api,
        CancellationToken ct)
    {
        return MarkAsync(id, context, antiforgery, api, ct);
    }

    private static Task<IResult> ReadAllAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CustomerBookingApiClient api,
        CancellationToken ct)
    {
        return MarkAsync(null, context, antiforgery, api, ct);
    }

    private static async Task<IResult> MarkAsync(
        long? id,
        HttpContext context,
        IAntiforgery antiforgery,
        CustomerBookingApiClient api,
        CancellationToken ct)
    {
        if (!context.Request.HasFormContentType ||
            !await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid form or security token.");
        }

        if (id is <= 0)
            return Results.BadRequest();

        var token = await context.GetTokenAsync(
            "DiveHubWeb", "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/customer/login?error=expired");

        try
        {
            var status = id.HasValue
                ? await api.MarkNotificationReadAsync(token, id.Value, ct)
                : await api.MarkAllNotificationsReadAsync(token, ct);

            return status switch
            {
                HttpStatusCode.NoContent =>
                    Results.Redirect("/notifications"),

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    Results.Redirect("/customer/login?error=expired"),

                _ => Results.Redirect("/notifications?status=error")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect("/notifications?status=error");
        }
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return Results.Redirect("/notifications?status=error");
        }
    }

    private static IResult Invalid(int activityId, string field)
    {
        return Results.Redirect(
            $"/book?activityId={activityId}" +
            $"&status=invalid&field={field}");
    }

    private static string GetContactErrorField(string message)
    {
        if (message.Contains(
            "full name", StringComparison.OrdinalIgnoreCase))
            return "name";

        if (message.Contains(
            "WhatsApp", StringComparison.OrdinalIgnoreCase))
            return "whatsapp";

        if (message.Contains(
            "email", StringComparison.OrdinalIgnoreCase))
            return "email";

        if (message.Contains(
            "phone", StringComparison.OrdinalIgnoreCase))
            return "phone";

        if (message.Contains(
            "contact method", StringComparison.OrdinalIgnoreCase))
            return "preferences";

        return "contact";
    }

    private static bool IsChecked(
        IFormCollection form,
        string field)
    {
        return form[field].Any(value =>
            string.Equals(
                value,
                "true",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                value,
                "on",
                StringComparison.OrdinalIgnoreCase));
    }
}