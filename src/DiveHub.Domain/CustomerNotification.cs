namespace DiveHub.Domain;

public enum CustomerNotificationKind
{
    ReservationConfirmed = 1,
    ReservationRejected = 2
}

public sealed class CustomerNotification
{
    public long Id { get; set; }

    public Guid CustomerUserId { get; set; }

    public long? ReservationId { get; set; }

    public CustomerNotificationKind Kind { get; set; }

    public string Title { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ReadAtUtc { get; set; }
}