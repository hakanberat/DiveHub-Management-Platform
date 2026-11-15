using System.Security.Claims;
using DiveHub.Application;
using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class CustomerNotificationEndpoints
{
    public static IEndpointRouteBuilder MapCustomerNotificationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/customer/notifications")
            .RequireAuthorization("CustomerOnly");

        group.MapGet("", ListAsync);

        group.MapGet("/unread-count", CountAsync);

        group.MapPost("/{id:long}/read", MarkReadAsync);

        group.MapPost("/read-all", MarkAllReadAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var userId))
            return Results.Unauthorized();

        var items = await db.CustomerNotifications
            .AsNoTracking()
            .Where(x => x.CustomerUserId == userId)
            .OrderBy(x => x.ReadAtUtc != null)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(100)
            .Select(x => new CustomerNotificationItem(
                x.Id,
                x.ReservationId,
                x.Kind,
                x.Title,
                x.Message,
                x.CreatedAtUtc,
                x.ReadAtUtc))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> CountAsync(
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var userId))
            return Results.Unauthorized();

        var count = await db.CustomerNotifications
            .CountAsync(x =>
                x.CustomerUserId == userId &&
                x.ReadAtUtc == null, ct);

        return Results.Ok(
            new UnreadNotificationCount(count));
    }

    private static async Task<IResult> MarkReadAsync(
        long id,
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var userId))
            return Results.Unauthorized();

        var item = await db.CustomerNotifications
            .SingleOrDefaultAsync(x =>
                x.Id == id &&
                x.CustomerUserId == userId, ct);

        if (item is null)
            return Results.NotFound();

        if (item.ReadAtUtc is null)
        {
            item.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var userId))
            return Results.Unauthorized();

        var now = DateTime.UtcNow;

        await db.CustomerNotifications
            .Where(x =>
                x.CustomerUserId == userId &&
                x.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    x => x.ReadAtUtc,
                    (DateTime?)now), ct);

        return Results.NoContent();
    }

    private static bool TryGetUserId(
        ClaimsPrincipal principal,
        out Guid userId)
    {
        var value = principal.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}