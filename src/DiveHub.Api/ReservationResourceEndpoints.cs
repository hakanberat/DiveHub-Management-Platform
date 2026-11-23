
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class ReservationResourceEndpoints
{
    public sealed record OccupiedResourceDto(
        DateOnly ActivityDate,
        int? BoatId,
        int? InstructorId);

    public static IEndpointRouteBuilder MapReservationResourceEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/admin/reservations/occupied-resources",
            async Task<IResult> (
                DateOnly from,
                DateOnly to,
                DiveHubDbContext db,
                CancellationToken ct) =>
            {
                if (from > to)
                    return Results.BadRequest(
                        "Invalid date range.");

                var occupied = await db.Reservations
                    .AsNoTracking()
                    .Where(r =>
                        r.ActivityDate >= from &&
                        r.ActivityDate <= to &&
                        (r.Status == ReservationStatus.Confirmed ||
                         r.Status == ReservationStatus.Completed) &&
                        (r.BoatId != null ||
                         r.InstructorId != null))
                    .Select(r => new OccupiedResourceDto(
                        r.ActivityDate,
                        r.BoatId,
                        r.InstructorId))
                    .ToListAsync(ct);

                return Results.Ok(occupied);
            })
            .RequireAuthorization("AdminOnly");

        return app;
    }
}
