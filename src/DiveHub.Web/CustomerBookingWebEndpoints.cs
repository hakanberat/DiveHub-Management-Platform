
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
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CustomerBookingApiClient api,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
            return Results.BadRequest("Invalid security token.");

        if (!context.Request.HasFormContentType)
            return Results.BadRequest("Invalid booking form.");

        var form = await context.Request.ReadFormAsync(ct);

        var activityText = form["ActivityId"].ToString();
        var dateText = form["ActivityDate"].ToString();
        var participantsText = form["ParticipantCount"].ToString();

        if (!int.TryParse(
                activityText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var activityId) ||
            activityId <= 0)
        {
            return Invalid(0, "activity");
        }

        if (!DateOnly.TryParseExact(
                dateText,
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
                participantsText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var participants) ||
            participants is < 1 or > 250)
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
            "DiveHubWeb",
            "access_token");

        if (string.IsNullOrWhiteSpace(token))
            return Results.Redirect("/login?error=expired");

        var request = new CreateCustomerBookingRequest(
            activityId,
            activityDate,
            participants,
            notes,
            name,
            email,
            phone,
            string.IsNullOrWhiteSpace(whatsapp)
                ? null
                : whatsapp,
            byEmail,
            byPhone,
            byWhatsApp);

        try
        {
            var result = await api.CreateAsync(
                token,
                request,
                ct);

            return result switch
            {
                HttpStatusCode.Created =>
                    Results.Redirect(
                        "/my-reservations?status=created"),

                HttpStatusCode.BadRequest =>
                    Invalid(activityId, "server"),

                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden =>
                    Results.Redirect("/login?error=expired"),

                _ => Results.Redirect(
                    $"/book?activityId={activityId}&status=error")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                $"/book?activityId={activityId}&status=error");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Redirect(
                $"/book?activityId={activityId}&status=error");
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
        if (message.Contains("full name",
            StringComparison.OrdinalIgnoreCase))
            return "name";

        if (message.Contains("email",
            StringComparison.OrdinalIgnoreCase))
            return "email";

        if (message.Contains("phone",
            StringComparison.OrdinalIgnoreCase))
            return "phone";

        if (message.Contains("WhatsApp",
            StringComparison.OrdinalIgnoreCase))
            return "whatsapp";

        if (message.Contains("contact method",
            StringComparison.OrdinalIgnoreCase))
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
