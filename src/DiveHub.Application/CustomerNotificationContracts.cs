using DiveHub.Domain;

namespace DiveHub.Application;

public sealed record CustomerNotificationItem(
    long Id,
    long? ReservationId,
    CustomerNotificationKind Kind,
    string Title,
    string Message,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc
);

public sealed record UnreadNotificationCount(
    int Count
);