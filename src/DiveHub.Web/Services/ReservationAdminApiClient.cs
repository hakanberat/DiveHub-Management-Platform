using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Domain;

namespace DiveHub.Web.Services;

public sealed record AdminReservationItem(
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

public sealed record PendingReservationCountDto(int Count);

public sealed class ReservationAdminApiClient(HttpClient http)
{
    public async Task<int> GetPendingCountAsync(
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/admin/reservations/pending-count",
            accessToken);

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<PendingReservationCountDto>(
                cancellationToken: ct);

        return result?.Count ?? 0;
    }

    public async Task<IReadOnlyList<AdminReservationItem>> GetAllAsync(
        string accessToken,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/admin/reservations",
            accessToken);

        using var response = await http.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<AdminReservationItem>>(
                cancellationToken: ct) ?? [];
    }

    public async Task<HttpStatusCode> ConfirmAsync(
        string accessToken,
        long reservationId,
        int? boatId,
        int instructorId,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/admin/reservations/{reservationId}/confirm",
            accessToken);

        request.Content = JsonContent.Create(new
        {
            BoatId = boatId,
            InstructorId = instructorId
        });

        using var response = await http.SendAsync(request, ct);

        return response.StatusCode;
    }

    public async Task<HttpStatusCode> RejectAsync(
        string accessToken,
        long reservationId,
        string reason,
        CancellationToken ct = default)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/admin/reservations/{reservationId}/reject",
            accessToken);

        request.Content = JsonContent.Create(new
        {
            Reason = reason
        });

        using var response = await http.SendAsync(request, ct);

        return response.StatusCode;
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        string accessToken)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        return request;
    }
}