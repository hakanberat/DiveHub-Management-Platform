using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;

namespace DiveHub.Web.Services;

public sealed record ActivityBoatRequirementItem(
    int ActivityId,
    bool RequiresBoat);

public sealed class AdminActivityCatalogApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<ActivityCatalogItem>> ListAsync(
        string token)
    {
        using var request = Authorized(
            HttpMethod.Get,
            "api/admin/catalog/activities",
            token);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<ActivityCatalogItem>>() ?? [];
    }

    public async Task<IReadOnlyList<ActivityBoatRequirementItem>>
        GetBoatRequirementsAsync(string token)
    {
        using var request = Authorized(
            HttpMethod.Get,
            "api/admin/catalog/activities/boat-requirements",
            token);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<ActivityBoatRequirementItem>>() ?? [];
    }

    public Task<HttpStatusCode> SetBoatRequirementAsync(
        string token,
        int id,
        bool requiresBoat)
    {
        return SendAsync(
            HttpMethod.Patch,
            $"api/admin/catalog/activities/{id}/boat-requirement",
            token,
            new { RequiresBoat = requiresBoat });
    }

    public Task<HttpStatusCode> CreateAsync(
        string token,
        SaveActivityCatalogRequest data)
    {
        return SendAsync(
            HttpMethod.Post,
            "api/admin/catalog/activities",
            token,
            data);
    }

    public Task<HttpStatusCode> UpdateAsync(
        string token,
        int id,
        SaveActivityCatalogRequest data)
    {
        return SendAsync(
            HttpMethod.Put,
            $"api/admin/catalog/activities/{id}",
            token,
            data);
    }

    public Task<HttpStatusCode> SetPublishedAsync(
        string token,
        int id,
        bool isPublished)
    {
        return SendAsync(
            HttpMethod.Patch,
            $"api/admin/catalog/activities/{id}/publication",
            token,
            new SetActivityPublicationRequest(isPublished));
    }

    private async Task<HttpStatusCode> SendAsync<T>(
        HttpMethod method,
        string url,
        string token,
        T data)
    {
        using var request = Authorized(method, url, token);

        request.Content = JsonContent.Create(data);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    private static HttpRequestMessage Authorized(
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
