
using DiveHub.Domain;

namespace DiveHub.Application;

public sealed record ActivityCatalogItem(
    int Id,
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency,
    bool IsPublished
);

public sealed record SaveActivityCatalogRequest(
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency
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
