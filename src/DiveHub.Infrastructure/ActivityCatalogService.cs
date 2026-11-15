
using DiveHub.Application;
using DiveHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class ActivityCatalogService(
    DiveHubDbContext db) : IActivityCatalogService
{
    public async Task<IReadOnlyList<ActivityCatalogItem>> ListAsync(
        CancellationToken ct = default)
    {
        return await db.Activities
            .AsNoTracking()
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .Select(x => new ActivityCatalogItem(
                x.Id,
                x.Name,
                x.Description,
                x.Kind,
                x.Price,
                x.Currency,
                x.IsPublished))
            .ToListAsync(ct);
    }

    public async Task<ActivityCatalogItem?> GetAsync(
        int id,
        CancellationToken ct = default)
    {
        return await db.Activities
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ActivityCatalogItem(
                x.Id,
                x.Name,
                x.Description,
                x.Kind,
                x.Price,
                x.Currency,
                x.IsPublished))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ActivityCatalogItem> CreateAsync(
        SaveActivityCatalogRequest request,
        CancellationToken ct = default)
    {
        var activity = new Activity
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Kind = request.Kind,
            Price = request.Price,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            IsPublished = false
        };

        db.Activities.Add(activity);

        await db.SaveChangesAsync(ct);

        return ToItem(activity);
    }

    public async Task<ActivityCatalogItem?> UpdateAsync(
        int id,
        SaveActivityCatalogRequest request,
        CancellationToken ct = default)
    {
        var activity = await db.Activities
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (activity is null)
            return null;

        activity.Name = request.Name.Trim();
        activity.Description = request.Description.Trim();
        activity.Kind = request.Kind;
        activity.Price = request.Price;
        activity.Currency = request.Currency.Trim().ToUpperInvariant();

        await db.SaveChangesAsync(ct);

        return ToItem(activity);
    }

    public async Task<ActivityCatalogItem?> SetPublishedAsync(
        int id,
        bool isPublished,
        CancellationToken ct = default)
    {
        var activity = await db.Activities
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (activity is null)
            return null;

        activity.IsPublished = isPublished;

        await db.SaveChangesAsync(ct);

        return ToItem(activity);
    }

    private static ActivityCatalogItem ToItem(Activity activity)
    {
        return new ActivityCatalogItem(
            activity.Id,
            activity.Name,
            activity.Description,
            activity.Kind,
            activity.Price,
            activity.Currency,
            activity.IsPublished);
    }
}
