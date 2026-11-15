
using DiveHub.Domain;

namespace DiveHub.Application;

public sealed record UpdateSiteRequest(
    string Name,
    string Description,
    string Address,
    string Phone,
    string Email,
    string WhatsApp
);

public sealed record CreateDiveSiteRequest(
    string Name,
    string Description,
    int? MaximumDepthMeters
);

public sealed record CreateActivityRequest(
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency
);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password
);

public sealed record LoginRequest(
    string Email,
    string Password
);

public interface IContentService
{
    Task<SiteSettings> GetSiteAsync();

    Task<SiteSettings> UpdateSiteAsync(
        UpdateSiteRequest request);

    Task<IReadOnlyList<DiveSite>> GetDiveSitesAsync(
        bool includeUnpublished = false);

    Task<DiveSite> CreateDiveSiteAsync(
        CreateDiveSiteRequest request);

    Task<IReadOnlyList<Activity>> GetActivitiesAsync(
        bool includeUnpublished = false);

    Task<Activity> CreateActivityAsync(
        CreateActivityRequest request);
}
