
using DiveHub.Application;
using DiveHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class ContentService(DiveHubDbContext db)
    : IContentService
{
    public async Task<SiteSettings> GetSiteAsync()
    {
        return await db.SiteSettings
            .AsNoTracking()
            .SingleAsync(x => x.Id == 1);
    }

    public async Task<SiteSettings> UpdateSiteAsync(
        UpdateSiteRequest request)
    {
        var site = await db.SiteSettings.SingleAsync(x => x.Id == 1);

        site.Name = request.Name.Trim();
        site.Description = request.Description.Trim();
        site.Address = request.Address.Trim();
        site.Phone = request.Phone.Trim();
        site.Email = request.Email.Trim();
        site.WhatsApp = request.WhatsApp.Trim();

        await db.SaveChangesAsync();
        return site;
    }

    public async Task<IReadOnlyList<DiveSite>> GetDiveSitesAsync(
        bool includeUnpublished = false)
    {
        var query = db.DiveSites.AsNoTracking();

        if (!includeUnpublished)
            query = query.Where(x => x.IsPublished);

        return await query.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<DiveSite> CreateDiveSiteAsync(
        CreateDiveSiteRequest request)
    {
        var site = new DiveSite
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            MaximumDepthMeters = request.MaximumDepthMeters,
            IsPublished = false
        };

        db.DiveSites.Add(site);
        await db.SaveChangesAsync();

        return site;
    }

    public async Task<IReadOnlyList<Activity>> GetActivitiesAsync(
        bool includeUnpublished = false)
    {
        var query = db.Activities.AsNoTracking();

        if (!includeUnpublished)
            query = query.Where(x => x.IsPublished);

        return await query.OrderBy(x => x.Name).ToListAsync();
    }

    public async Task<Activity> CreateActivityAsync(
        CreateActivityRequest request)
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
        await db.SaveChangesAsync();

        return activity;
    }
}
