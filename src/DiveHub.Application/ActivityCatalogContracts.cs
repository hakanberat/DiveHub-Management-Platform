using DiveHub.Domain;

namespace DiveHub.Application;

public sealed record ActivityCatalogItem(
    int Id,
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency,
    bool IsPublished,
    string ShortDescription,
    string Category,
    string Country,
    string City,
    string Location,
    string MapLocation,
    string Duration,
    int? MinimumAge,
    string MinimumCertification,
    int? MaxParticipants,
    string Included,
    string NotIncluded,
    string Requirements,
    bool RequiresBoat
);

public sealed record SaveActivityCatalogRequest(
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency,
    string ShortDescription = "",
    string Category = "",
    string Country = "",
    string City = "",
    string Location = "",
    string MapLocation = "",
    string Duration = "",
    int? MinimumAge = null,
    string MinimumCertification = "",
    int? MaxParticipants = null,
    string Included = "",
    string NotIncluded = "",
    string Requirements = "",
    bool? RequiresBoat = null
);

public sealed record SetActivityPublicationRequest(
    bool IsPublished
);

public interface IActivityCatalogService
{
    Task<IReadOnlyList<ActivityCatalogItem>> ListAsync(
        CancellationToken ct = default);

    Task<ActivityCatalogItem?> GetAsync(
        int id,
        CancellationToken ct = default);

    Task<ActivityCatalogItem> CreateAsync(
        SaveActivityCatalogRequest request,
        CancellationToken ct = default);

    Task<ActivityCatalogItem?> UpdateAsync(
        int id,
        SaveActivityCatalogRequest request,
        CancellationToken ct = default);

    Task<ActivityCatalogItem?> SetPublishedAsync(
        int id,
        bool isPublished,
        CancellationToken ct = default);
}

public static class ActivityCatalogRules
{
    public const int MaximumAllowedParticipants = 250;

    public static bool HasValidCapacity(int? capacity)
    {
        return capacity is >= 1 and <= MaximumAllowedParticipants;
    }

    public static bool IsValidMapUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return Uri.TryCreate(
                   value.Trim(),
                   UriKind.Absolute,
                   out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme == Uri.UriSchemeHttp);
    }

    public static bool IsValid(SaveActivityCatalogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 120)
            return false;

        if (!Enum.IsDefined(request.Kind))
            return false;

        if (request.Price < 0 ||
            request.Price > 999999.99m ||
            decimal.Round(request.Price, 2) != request.Price)
            return false;

        var currency = request.Currency?.Trim().ToUpperInvariant();

        if (currency is null ||
            currency.Length != 3 ||
            currency.Any(c => c < 'A' || c > 'Z'))
            return false;

        if (!HasValidCapacity(request.MaxParticipants))
            return false;

        if (request.MinimumAge is < 0 or > 120)
            return false;

        if (!request.RequiresBoat.HasValue)
            return false;

        if (!IsValidMapUrl(request.MapLocation))
            return false;

        return ValidLength(request.Description, 4000)
            && ValidLength(request.ShortDescription, 300)
            && ValidLength(request.Category, 100)
            && ValidLength(request.Country, 100)
            && ValidLength(request.City, 100)
            && ValidLength(request.Location, 250)
            && ValidLength(request.MapLocation, 2000)
            && ValidLength(request.Duration, 100)
            && ValidLength(request.MinimumCertification, 200)
            && ValidLength(request.Included, 2000)
            && ValidLength(request.NotIncluded, 2000)
            && ValidLength(request.Requirements, 2000);
    }

    private static bool ValidLength(string? value, int max)
    {
        return value is not null && value.Trim().Length <= max;
    }
}