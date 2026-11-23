using System.Security.Claims;
using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace DiveHub.Api;

public static class ReservationEndpoints
{
    public sealed record CreateReservationRequest(
        int ActivityId,
        DateOnly ActivityDate,
        int ParticipantCount,
        string? CustomerNotes,
        string? CustomerFullName,
        string? ContactEmail,
        string? ContactPhone,
        string? ContactWhatsApp,
        bool ContactByEmail,
        bool ContactByPhone,
        bool ContactByWhatsApp);

    public sealed record ConfirmReservationRequest(
        int? BoatId,
        int InstructorId);

    public sealed record RejectReservationRequest(
        string? Reason);

    public sealed record ReservationItem(
        long Id,
        Guid CustomerUserId,
        string? CustomerEmail,
        int ActivityId,
        string ActivityName,
        DateOnly ActivityDate,
        int ParticipantCount,
        int? BoatId,
        string? BoatName,
        int? InstructorId,
        string? InstructorName,
        ReservationStatus Status,
        PaymentStatus PaymentStatus,
        decimal UnitPrice,
        decimal TotalPrice,
        string Currency,
        string CustomerNotes,
        DateTime CreatedAtUtc,
        string? CustomerFullName,
        string? ContactEmail,
        string? ContactPhone,
        string? ContactWhatsApp,
        bool ContactByEmail,
        bool ContactByPhone,
        bool ContactByWhatsApp,
        string? RejectionReason = null);

    public static IEndpointRouteBuilder MapReservationEndpoints(
        this IEndpointRouteBuilder app)
    {
        var customer = app.MapGroup("/api/reservations")
            .RequireAuthorization("CustomerOnly");

        customer.MapPost("", CreateAsync);
        customer.MapGet("/mine", GetMineAsync);
        customer.MapGet("/{id:long}", GetMineByIdAsync);

        var admin = app.MapGroup("/api/admin/reservations")
            .RequireAuthorization("AdminOnly");

        admin.MapGet("", GetAdminListAsync);
        admin.MapPost("/{id:long}/confirm", ConfirmAsync);
        admin.MapPost("/{id:long}/reject", RejectAsync);

        app.MapCustomerNotificationEndpoints();

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateReservationRequest request,
        HttpContext context,
        DiveHubDbContext db,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var customerId))
            return Results.Unauthorized();

        if (request.ActivityId <= 0 ||
            request.ParticipantCount < 1 ||
            request.ParticipantCount >
                ActivityCatalogRules.MaximumAllowedParticipants)
        {
            return Error("Invalid activity or participant count.");
        }

        var earliestDate = BookingRules.GetEarliestDate(
            configuration["DiveCenter:TimeZoneId"]);

        if (request.ActivityDate < earliestDate)
        {
            return Error(
                $"Reservations must be made for {earliestDate:yyyy-MM-dd} or later.");
        }

        var contactError = BookingRules.ValidateContact(
            request.CustomerFullName,
            request.ContactEmail,
            request.ContactPhone,
            request.ContactWhatsApp,
            request.ContactByEmail,
            request.ContactByPhone,
            request.ContactByWhatsApp);

        if (contactError is not null)
            return Error(contactError);

        var notes = request.CustomerNotes?.Trim() ?? "";

        if (notes.Length > 2000)
            return Error("Customer notes are too long.");

        var activity = await db.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.ActivityId &&
                x.IsPublished, ct);

        if (activity is null)
            return Error("Activity is unavailable.");

        if (!ActivityCatalogRules.HasValidCapacity(
            activity.MaxParticipants))
        {
            return Error(
                "Activity participant capacity is not configured.");
        }

        var maxParticipants =
            activity.MaxParticipants.GetValueOrDefault();

        if (request.ParticipantCount > maxParticipants)
        {
            return Error(
                $"This activity allows a maximum of {maxParticipants} participants.");
        }

        if (activity.Price < 0)
            return Error("Invalid activity price.");

        decimal totalPrice;

        try
        {
            totalPrice = checked(
                activity.Price * request.ParticipantCount);
        }
        catch (OverflowException)
        {
            return Error("Invalid reservation total.");
        }

        if (totalPrice > 9999999999999999.99m)
            return Error("Reservation total exceeds the allowed limit.");

        var reservation = new Reservation
        {
            CustomerUserId = customerId,
            ActivityId = activity.Id,
            ActivityDate = request.ActivityDate,
            ParticipantCount = request.ParticipantCount,

            CustomerFullName = request.CustomerFullName!.Trim(),
            ContactEmail = request.ContactEmail!.Trim(),
            ContactPhone = request.ContactPhone!.Trim(),

            ContactWhatsApp =
                string.IsNullOrWhiteSpace(request.ContactWhatsApp)
                    ? null
                    : request.ContactWhatsApp.Trim(),

            ContactByEmail = request.ContactByEmail,
            ContactByPhone = request.ContactByPhone,
            ContactByWhatsApp = request.ContactByWhatsApp,

            CustomerNotes = notes,

            UnitPrice = activity.Price,
            TotalPrice = totalPrice,
            Currency = activity.Currency,

            Status = ReservationStatus.Pending,
            PaymentStatus = PaymentStatus.Unpaid,

            BoatId = null,
            InstructorId = null,

            CreatedAtUtc = DateTime.UtcNow
        };

        db.Reservations.Add(reservation);

        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/reservations/{reservation.Id}",
            new
            {
                reservation.Id,
                reservation.Status,
                reservation.PaymentStatus,
                reservation.TotalPrice,
                reservation.Currency
            });
    }

    private static async Task<IResult> GetMineAsync(
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var customerId))
            return Results.Unauthorized();

        var reservations = await db.Reservations
            .AsNoTracking()
            .Where(x => x.CustomerUserId == customerId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(100)
            .ToListAsync(ct);

        return Results.Ok(
            await BuildItemsAsync(db, reservations, ct));
    }

    private static async Task<IResult> GetMineByIdAsync(
        long id,
        HttpContext context,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        if (!TryGetUserId(context.User, out var customerId))
            return Results.Unauthorized();

        var reservation = await db.Reservations
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.CustomerUserId == customerId, ct);

        if (reservation is null)
            return Results.NotFound();

        var items = await BuildItemsAsync(
            db, [reservation], ct);

        return Results.Ok(items[0]);
    }

    private static async Task<IResult> GetAdminListAsync(
        DiveHubDbContext db,
        CancellationToken ct)
    {
        var reservations = await db.Reservations
            .AsNoTracking()
            .OrderBy(x =>
                x.Status == ReservationStatus.Pending ? 0 : 1)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(200)
            .ToListAsync(ct);

        return Results.Ok(
            await BuildItemsAsync(db, reservations, ct));
    }

    private static async Task<IResult> ConfirmAsync(
        long id,
        ConfirmReservationRequest request,
        DiveHubDbContext db,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (id <= 0 ||
            request.InstructorId <= 0 ||
            request.BoatId is <= 0)
        {
            return Error("Invalid instructor or boat assignment.");
        }

        var reservation = await db.Reservations
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (reservation is null)
            return Results.NotFound();

        if (reservation.Status != ReservationStatus.Pending)
        {
            return Conflict(
                "Only pending reservations can be confirmed.");
        }

        var activity = await db.Activities
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == reservation.ActivityId, ct);

        if (activity is null)
            return Results.NotFound();

        if (!ActivityCatalogRules.HasValidCapacity(
            activity.MaxParticipants))
        {
            return Error(
                "Activity participant capacity is not configured.");
        }

        var maxParticipants =
            activity.MaxParticipants.GetValueOrDefault();

        if (reservation.ParticipantCount < 1 ||
            reservation.ParticipantCount > maxParticipants)
        {
            return Error(
                $"Participant count exceeds the activity limit of {maxParticipants}.");
        }

        if (activity.RequiresBoat && request.BoatId is null)
            return Error("This activity requires an assigned boat.");

        var localToday = BookingRules.GetEarliestDate(
                configuration["DiveCenter:TimeZoneId"])
            .AddDays(-BookingRules.MinimumAdvanceDays);

        if (reservation.ActivityDate < localToday)
            return Error("Past reservations cannot be confirmed.");

        var instructor = await db.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.InstructorId &&
                x.IsActive, ct);

        if (instructor is null)
            return Error("Instructor is unavailable.");

        var instructorOccupied = await db.Reservations
            .AnyAsync(x =>
                x.Id != id &&
                x.ActivityDate == reservation.ActivityDate &&
                x.InstructorId == request.InstructorId &&
                (x.Status == ReservationStatus.Confirmed ||
                 x.Status == ReservationStatus.Completed), ct);

        if (instructorOccupied)
            return Conflict("Instructor already assigned on this date.");

        if (request.BoatId is int boatId)
        {
            var boat = await db.Boats
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == boatId && x.IsActive, ct);

            if (boat is null)
                return Error("Boat is unavailable.");

            if (reservation.ParticipantCount > boat.Capacity)
            {
                return Error(
                    $"Participant count exceeds boat capacity of {boat.Capacity}.");
            }

            var boatOccupied = await db.Reservations
                .AnyAsync(x =>
                    x.Id != id &&
                    x.ActivityDate == reservation.ActivityDate &&
                    x.BoatId == boatId &&
                    (x.Status == ReservationStatus.Confirmed ||
                     x.Status == ReservationStatus.Completed), ct);

            if (boatOccupied)
                return Conflict("Boat already assigned on this date.");
        }

        reservation.BoatId = request.BoatId;
        reservation.InstructorId = request.InstructorId;
        reservation.Status = ReservationStatus.Confirmed;

        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerUserId = reservation.CustomerUserId,
            ReservationId = reservation.Id,
            Kind = CustomerNotificationKind.ReservationConfirmed,
            Title = $"Booking #{reservation.Id} confirmed",
            Message =
                $"Your reservation for {activity.Name} on " +
                $"{reservation.ActivityDate:yyyy-MM-dd} has been confirmed."
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(
                "The reservation or assignment has changed. Refresh and retry.");
        }

        return Results.Ok(new
        {
            reservation.Id,
            reservation.Status,
            reservation.BoatId,
            reservation.InstructorId
        });
    }

    private static async Task<IResult> RejectAsync(
        long id,
        RejectReservationRequest request,
        DiveHubDbContext db,
        CancellationToken ct)
    {
        var reason = request.Reason?.Trim() ?? "";

        if (id <= 0 ||
            reason.Length < 1 ||
            reason.Length > 1000)
        {
            return Error(
                "A rejection reason is required (maximum 1000 characters).");
        }

        var reservation = await db.Reservations
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (reservation is null)
            return Results.NotFound();

        if (reservation.Status != ReservationStatus.Pending)
        {
            return Conflict(
                "Only pending reservations can be rejected.");
        }

        reservation.Status = ReservationStatus.Rejected;

        reservation.BoatId = null;
        reservation.InstructorId = null;

        db.CustomerNotifications.Add(new CustomerNotification
        {
            CustomerUserId = reservation.CustomerUserId,
            ReservationId = reservation.Id,
            Kind = CustomerNotificationKind.ReservationRejected,
            Title = $"Booking #{reservation.Id} rejected",
            Message = reason
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Conflict(
                "The reservation has changed. Refresh and retry.");
        }

        return Results.NoContent();
    }

    private static async Task<ReservationItem[]> BuildItemsAsync(
        DiveHubDbContext db,
        IReadOnlyList<Reservation> reservations,
        CancellationToken ct)
    {
        if (reservations.Count == 0)
            return [];

        var activityIds = reservations
            .Select(x => x.ActivityId)
            .Distinct()
            .ToArray();

        var boatIds = reservations
            .Where(x => x.BoatId.HasValue)
            .Select(x => x.BoatId!.Value)
            .Distinct()
            .ToArray();

        var instructorIds = reservations
            .Where(x => x.InstructorId.HasValue)
            .Select(x => x.InstructorId!.Value)
            .Distinct()
            .ToArray();

        var customerIds = reservations
            .Select(x => x.CustomerUserId)
            .Distinct()
            .ToArray();

        var reservationIds = reservations
            .Select(x => x.Id)
            .ToArray();

        var activities = await db.Activities
            .AsNoTracking()
            .Where(x => activityIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var boats = await db.Boats
            .AsNoTracking()
            .Where(x => boatIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var instructors = await db.StaffMembers
            .AsNoTracking()
            .Where(x => instructorIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var customers = await db.Users
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, ct);

        var reasons = await db.CustomerNotifications
            .AsNoTracking()
            .Where(x =>
                x.Kind == CustomerNotificationKind.ReservationRejected &&
                x.ReservationId.HasValue &&
                reservationIds.Contains(x.ReservationId.Value))
            .ToDictionaryAsync(
                x => x.ReservationId!.Value,
                x => x.Message,
                ct);

        return reservations.Select(x => new ReservationItem(
            x.Id,
            x.CustomerUserId,
            customers.GetValueOrDefault(x.CustomerUserId),
            x.ActivityId,
            activities.GetValueOrDefault(x.ActivityId)
                ?? "Unknown activity",
            x.ActivityDate,
            x.ParticipantCount,
            x.BoatId,
            x.BoatId is int boatId
                ? boats.GetValueOrDefault(boatId)
                : null,
            x.InstructorId,
            x.InstructorId is int instructorId
                ? instructors.GetValueOrDefault(instructorId)
                : null,
            x.Status,
            x.PaymentStatus,
            x.UnitPrice,
            x.TotalPrice,
            x.Currency,
            x.CustomerNotes,
            x.CreatedAtUtc,
            x.CustomerFullName,
            x.ContactEmail,
            x.ContactPhone,
            x.ContactWhatsApp,
            x.ContactByEmail,
            x.ContactByPhone,
            x.ContactByWhatsApp,
            reasons.GetValueOrDefault(x.Id)
        )).ToArray();
    }

    private static bool TryGetUserId(
        ClaimsPrincipal principal,
        out Guid userId)
    {
        var id = principal.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(id, out userId);
    }

    private static IResult Error(string message)
    {
        return Results.BadRequest(new { error = message });
    }

    private static IResult Conflict(string message)
    {
        return Results.Conflict(new { error = message });
    }
}