
using System.ComponentModel.DataAnnotations;

namespace DiveHub.Application;

public static class BookingRules
{
    public const int MinimumAdvanceDays = 2;

    // Cyprus time by default.
    // Override through DiveCenter:TimeZoneId configuration.
    public static DateOnly GetEarliestDate(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId)
            ? "Asia/Nicosia"
            : timeZoneId.Trim();

        var zone = TimeZoneInfo.FindSystemTimeZoneById(id);

        var localNow = TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            zone);

        return DateOnly.FromDateTime(localNow.DateTime)
            .AddDays(MinimumAdvanceDays);
    }

    public static string? ValidateContact(
        string? fullName,
        string? email,
        string? phone,
        string? whatsapp,
        bool byEmail,
        bool byPhone,
        bool byWhatsApp)
    {
        fullName = fullName?.Trim();
        email = email?.Trim();
        phone = phone?.Trim();
        whatsapp = whatsapp?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Length > 160)
        {
            return "Enter your full name (maximum 160 characters).";
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 254 ||
            !new EmailAddressAttribute().IsValid(email))
        {
            return "Enter a valid contact email.";
        }

        if (string.IsNullOrWhiteSpace(phone) ||
            phone.Length > 50 ||
            !IsValidPhone(phone))
        {
            return "Enter a valid phone number with 7–15 digits.";
        }

        if (!string.IsNullOrWhiteSpace(whatsapp) &&
            (whatsapp.Length > 50 || !IsValidPhone(whatsapp)))
        {
            return "Enter a valid WhatsApp number.";
        }

        if (!byEmail && !byPhone && !byWhatsApp)
        {
            return "Select at least one preferred contact method.";
        }

        if (byWhatsApp && string.IsNullOrWhiteSpace(whatsapp))
        {
            return "WhatsApp number is required when selected.";
        }

        return null;
    }

    private static bool IsValidPhone(string number)
    {
        if (!number.All(c =>
            char.IsAsciiDigit(c) ||
            c is '+' or ' ' or '-' or '(' or ')'))
        {
            return false;
        }

        var digits = number.Count(char.IsAsciiDigit);

        return digits is >= 7 and <= 15;
    }
}
