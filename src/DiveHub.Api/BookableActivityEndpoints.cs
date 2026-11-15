using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class BookableActivityEndpoints
{
    public sealed record BookableActivityDto(
        int Id,
        string Name,
        string Description,
        ActivityKind Kind,
        decimal Price,
        string Currency,
        int MaxParticipants,
        bool RequiresBoat);

    public static IEndpointRouteBuilder MapBookableActivityEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/public/bookable-activities",
            async (
                DiveHubDbContext db,
                CancellationToken ct) =>
            {
                var activities = await db.Activities
                    .AsNoTracking()
                    .Where(x =>
                        x.IsPublished &&
                        x.MaxParticipants >= 1 &&
                        x.MaxParticipants <=
                            ActivityCatalogRules.MaximumAllowedParticipants)
                    .OrderBy(x => x.Kind)
                    .ThenBy(x => x.Name)
                    .Select(x => new BookableActivityDto(
                        x.Id,
                        x.Name,
                        x.Description,
                        x.Kind,
                        x.Price,
                        x.Currency,
                        x.MaxParticipants!.Value,
                        x.RequiresBoat))
                    .ToListAsync(ct);

                return Results.Ok(activities);
            })
            .AllowAnonymous();

        return app;
    }
}