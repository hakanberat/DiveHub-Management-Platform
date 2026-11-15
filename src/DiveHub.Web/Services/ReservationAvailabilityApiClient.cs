
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DiveHub.Web.Services;

public sealed record ReservationResourceOccupancy(
    DateOnly ActivityDate,
    int? BoatId,
    int? InstructorId);

public sealed class ReservationAvailabilityApiClient
{
    private readonly HttpClient _http;

    public ReservationAvailabilityApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<ReservationResourceOccupancy>>
        GetOccupiedAsync(
            string accessToken,
            DateOnly from,
            DateOnly to,
            CancellationToken ct = default)
    {
        var start = from.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        var end = to.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture);

        var url =
            "/api/admin/reservations/occupied-resources" +
            $"?from={start}&to={end}";

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        using var response = await _http.SendAsync(
            request,
            ct);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<ReservationResourceOccupancy>>(
                cancellationToken: ct) ?? [];
    }
}
