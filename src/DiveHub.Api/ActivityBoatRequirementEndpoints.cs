using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class ActivityBoatRequirementEndpoints
{
    public sealed record BoatRequirementItem(
        int ActivityId,
        bool RequiresBoat);

    public sealed record UpdateBoatRequirementRequest(
        bool RequiresBoat);

    public static IEndpointRouteBuilder MapActivityBoatRequirementEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/catalog/activities")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/boat-requirements", async (
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            var requirements = await db.Activities
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(x => new BoatRequirementItem(
                    x.Id,
                    x.RequiresBoat))
                .ToListAsync(ct);

            return Results.Ok(requirements);
        });

        group.MapPatch("/{id:int}/boat-requirement", async (
            int id,
            UpdateBoatRequirementRequest request,
            DiveHubDbContext db,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var activity = await db.Activities
                .SingleOrDefaultAsync(x => x.Id == id, ct);

            if (activity is null)
                return Results.NotFound();

            activity.RequiresBoat = request.RequiresBoat;
            await db.SaveChangesAsync(ct);

            return Results.Ok(new BoatRequirementItem(
                activity.Id,
                activity.RequiresBoat));
        });

        return app;
    }
}
