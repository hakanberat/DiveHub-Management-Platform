using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class ReservationDashboardEndpoints
{
    public static IEndpointRouteBuilder MapReservationDashboardEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/api/admin/reservations/pending-count",
            async Task<IResult> (
                DiveHubDbContext db,
                CancellationToken ct) =>
            {
                var count = await db.Reservations
                    .CountAsync(
                        x => x.Status == ReservationStatus.Pending,
                        ct);

                return Results.Ok(new { Count = count });
            })
            .RequireAuthorization("AdminOnly");

        return app;
    }
}