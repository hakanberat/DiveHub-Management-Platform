using System.Linq.Expressions;
using DiveHub.Application;
using DiveHub.Domain;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Infrastructure;

public sealed class ActivityCatalogService(
    DiveHubDbContext db) : IActivityCatalogService
{
    private static readonly Expression<Func<Activity, ActivityCatalogItem>>
        Projection = x => new ActivityCatalogItem(
            x.Id,
            x.Name,
            x.Description,
            x.Kind,
            x.Price,
            x.Currency,
            x.IsPublished,
            x.ShortDescription,
            x.Category,
            x.Country,
            x.City,
            x.Location,
            x.MapLocation,
            x.Duration,
            x.MinimumAge,
            x.MinimumCertification,
            x.MaxParticipants,
            x.Included,
            x.NotIncluded,
            x.Requirements,
            x.RequiresBoat);

    private static readonly Func<Activity, ActivityCatalogItem>
        ToItem = Projection.Compile();

    public async Task<IReadOnlyList<ActivityCatalogItem>> ListAsync(
        CancellationToken ct = default)
    {
        return await db.Activities
            .AsNoTracking()
            .OrderBy(x => x.Kind)
            .ThenBy(x => x.Name)
            .Select(Projection)
            .ToListAsync(ct);
    }

    public async Task<ActivityCatalogItem?> GetAsync(
        int id,
        CancellationToken ct = default)
    {
        return await db.Activities
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(Projection)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<ActivityCatalogItem> CreateAsync(
        SaveActivityCatalogRequest request,
        CancellationToken ct = default)
    {
        Validate(request);

        var activity = new Activity
        {
            IsPublished = false
        };

        Apply(activity, request);

        db.Activities.Add(activity);

        await db.SaveChangesAsync(ct);

        return ToItem(activity);
    }

    public async Task<ActivityCatalogItem?> UpdateAsync(
        int id,
        SaveActivityCatalogRequest request,
        CancellationToken ct = default)
    {
        Validate(request);

        var activity = await db.Activities
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (activity is null)
            return null;

        Apply(activity, request);

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

        if (isPublished &&
            !ActivityCatalogRules.HasValidCapacity(
                activity.MaxParticipants))
        {
            throw new ArgumentException(
                "Set a valid participant limit before publishing.");
        }

        activity.IsPublished = isPublished;

        await db.SaveChangesAsync(ct);

        return ToItem(activity);
    }

    private static void Validate(
        SaveActivityCatalogRequest request)
    {
        if (!ActivityCatalogRules.IsValid(request))
        {
            throw new ArgumentException(
                "Invalid activity information.");
        }
    }

    private static void Apply(
        Activity activity,
        SaveActivityCatalogRequest request)
    {
        activity.Name = request.Name.Trim();
        activity.Description = request.Description.Trim();
        activity.ShortDescription = request.ShortDescription.Trim();

        activity.Kind = request.Kind;
        activity.Category = request.Category.Trim();

        activity.Country = request.Country.Trim();
        activity.City = request.City.Trim();
        activity.Location = request.Location.Trim();
        activity.MapLocation = request.MapLocation.Trim();

        activity.Price = request.Price;
        activity.Currency = request.Currency.Trim().ToUpperInvariant();

        activity.Duration = request.Duration.Trim();
        activity.MinimumAge = request.MinimumAge;
        activity.MinimumCertification =
            request.MinimumCertification.Trim();

        activity.MaxParticipants = request.MaxParticipants;

        activity.Included = request.Included.Trim();
        activity.NotIncluded = request.NotIncluded.Trim();
        activity.Requirements = request.Requirements.Trim();

        activity.RequiresBoat = request.RequiresBoat!.Value;
    }
}