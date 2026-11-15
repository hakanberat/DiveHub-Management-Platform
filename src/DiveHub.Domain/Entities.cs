
namespace DiveHub.Domain;

public enum ActivityKind
{
    Dive = 1,
    Course = 2
}

public enum ReservationStatus
{
    Pending = 0,
    Confirmed = 1,
    Completed = 2,
    Rejected = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum PaymentStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Refunded = 3
}

public sealed class SiteSettings
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = "Dive Hub";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string WhatsApp { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public sealed class DiveSite
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int? MaximumDepthMeters { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class Activity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ActivityKind Kind { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";
    public bool IsPublished { get; set; }

    // Boat is mandatory when confirming this activity.
    public bool RequiresBoat { get; set; } = false;
}

public sealed class Boat
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StaffMember
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Biography { get; set; } = "";
    public string CertificationAgency { get; set; } = "";
    public string CertificationNumber { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; }
}

public sealed class Reservation
{
    public long Id { get; set; }
    public Guid CustomerUserId { get; set; }

    // Contact snapshot captured at reservation time.
    // Nullable to preserve existing reservations.
    public string? CustomerFullName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactWhatsApp { get; set; }

    public bool ContactByEmail { get; set; }
    public bool ContactByPhone { get; set; }
    public bool ContactByWhatsApp { get; set; }

    public int ActivityId { get; set; }

    // Daily reservation without time slots.
    public DateOnly ActivityDate { get; set; }

    public int ParticipantCount { get; set; }
    public int? BoatId { get; set; }
    public int? InstructorId { get; set; }

    public ReservationStatus Status { get; set; }
        = ReservationStatus.Pending;

    public PaymentStatus PaymentStatus { get; set; }
        = PaymentStatus.Unpaid;

    // Price snapshot at the time of booking.
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "EUR";

    public string CustomerNotes { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;
}

public sealed class DiveEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public bool IsPublished { get; set; }
}

public sealed class MediaAsset
{
    public long Id { get; set; }
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string AltText { get; set; } = "";
    public string ContentType { get; set; } = "";

    public int? DiveSiteId { get; set; }
    public int? ActivityId { get; set; }
    public int? StaffMemberId { get; set; }

    public DateTime UploadedAtUtc { get; set; }
        = DateTime.UtcNow;
}
