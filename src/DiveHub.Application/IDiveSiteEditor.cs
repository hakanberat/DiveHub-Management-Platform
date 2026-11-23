
using DiveHub.Domain;

namespace DiveHub.Application;

public sealed record EditDiveSiteRequest(
    string Name,
    string Description,
    int? MaximumDepthMeters
);

public sealed record SetDiveSitePublicationRequest(
    bool IsPublished
);

public interface IDiveSiteEditor
{
    Task<DiveSite?> UpdateAsync(
        int id,
        EditDiveSiteRequest request,
        CancellationToken cancellationToken = default);

    Task<DiveSite?> SetPublishedAsync(
        int id,
        bool isPublished,
        CancellationToken cancellationToken = default);
}
