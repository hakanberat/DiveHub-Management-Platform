using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;
using DiveHub.Domain;

namespace DiveHub.Web.Services;

public sealed record BookableActivityItem(
    int Id,
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency,
    int MaxParticipants,
    bool RequiresBoat);

public sealed record CreateCustomerBookingRequest(
    int ActivityId,
    DateOnly ActivityDate,
    int ParticipantCount,
    string CustomerNotes,
    string CustomerFullName,
    string ContactEmail,
    string ContactPhone,
    string? ContactWhatsApp,
    bool ContactByEmail,
    bool ContactByPhone,
    bool ContactByWhatsApp);

public sealed record CustomerReservationItem(
    long Id,
    string ActivityName,
    DateOnly ActivityDate,
    int ParticipantCount,
    ReservationStatus Status,
    PaymentStatus PaymentStatus,
    decimal TotalPrice,
    string Currency,
    string CustomerNotes,
    string? BoatName,
    string? InstructorName,
    DateTime CreatedAtUtc,
    string? CustomerFullName,
    string? ContactEmail,
    string? ContactPhone,
    string? ContactWhatsApp,
    bool ContactByEmail,
    bool ContactByPhone,
    bool ContactByWhatsApp,
    string? RejectionReason = null);

public sealed class CustomerBookingApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<BookableActivityItem>>
        GetActivitiesAsync(CancellationToken ct = default)
    {
        return await http.GetFromJsonAsync<List<BookableActivityItem>>(
            "/api/public/bookable-activities",
            ct) ?? [];
    }

    public async Task<HttpStatusCode> CreateAsync(
        string token,
        CreateCustomerBookingRequest booking,
        CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Post,
            "/api/reservations",
            token);

        request.Content = JsonContent.Create(booking);

        using var response = await http.SendAsync(request, ct);

        return response.StatusCode;
    }

    public async Task<IReadOnlyList<CustomerReservationItem>>
        GetMineAsync(string token, CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Get,
            "/api/reservations/mine",
            token);

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<CustomerReservationItem>>(
                cancellationToken: ct) ?? [];
    }

    public async Task<IReadOnlyList<CustomerNotificationItem>>
        GetNotificationsAsync(
            string token,
            CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Get,
            "/api/customer/notifications",
            token);

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<CustomerNotificationItem>>(
                cancellationToken: ct) ?? [];
    }

    public async Task<int> GetUnreadNotificationCountAsync(
        string token,
        CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Get,
            "/api/customer/notifications/unread-count",
            token);

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<UnreadNotificationCount>(
                cancellationToken: ct);

        return result?.Count ?? 0;
    }

    public async Task<HttpStatusCode> MarkNotificationReadAsync(
        string token,
        long id,
        CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/customer/notifications/{id}/read",
            token);

        using var response = await http.SendAsync(request, ct);

        return response.StatusCode;
    }

    public async Task<HttpStatusCode> MarkAllNotificationsReadAsync(
        string token,
        CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Post,
            "/api/customer/notifications/read-all",
            token);

        using var response = await http.SendAsync(request, ct);

        return response.StatusCode;
    }

    private static HttpRequestMessage NewAuthenticatedRequest(
        HttpMethod method,
        string url,
        string token)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}