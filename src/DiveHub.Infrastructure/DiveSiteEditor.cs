
using DiveHub.Application;
using DiveHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class DiveSiteEditor(DiveHubDbContext db)
    : IDiveSiteEditor
{
    public async Task<DiveSite?> UpdateAsync(
        int id,
        EditDiveSiteRequest request,
        CancellationToken cancellationToken = default)
    {
        var site = await db.DiveSites
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (site is null)
            return null;

        site.Name = request.Name.Trim();
        site.Description = request.Description.Trim();
        site.MaximumDepthMeters = request.MaximumDepthMeters;

        await db.SaveChangesAsync(cancellationToken);

        return site;
    }

    public async Task<DiveSite?> SetPublishedAsync(
        int id,
        bool isPublished,
        CancellationToken cancellationToken = default)
    {
        var site = await db.DiveSites
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        if (site is null)
            return null;

        site.IsPublished = isPublished;

        await db.SaveChangesAsync(cancellationToken);

        return site;
    }
}
