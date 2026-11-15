
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Domain;

namespace DiveHub.Web.Services;

public sealed record BookableActivityItem(
    int Id,
    string Name,
    string Description,
    ActivityKind Kind,
    decimal Price,
    string Currency);

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
    bool ContactByWhatsApp);

public sealed class CustomerBookingApiClient
{
    private readonly HttpClient _http;

    public CustomerBookingApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<BookableActivityItem>>
        GetActivitiesAsync(
            CancellationToken ct = default)
    {
        return await _http
            .GetFromJsonAsync<List<BookableActivityItem>>(
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

        using var response = await _http.SendAsync(
            request,
            ct);

        return response.StatusCode;
    }

    public async Task<IReadOnlyList<CustomerReservationItem>>
        GetMineAsync(
            string token,
            CancellationToken ct = default)
    {
        using var request = NewAuthenticatedRequest(
            HttpMethod.Get,
            "/api/reservations/mine",
            token);

        using var response = await _http.SendAsync(
            request,
            ct);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<CustomerReservationItem>>(
                cancellationToken: ct) ?? [];
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
