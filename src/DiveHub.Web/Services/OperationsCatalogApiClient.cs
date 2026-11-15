
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;

namespace DiveHub.Web.Services;

public sealed class OperationsCatalogApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<BoatItem>> GetBoatsAsync(
        string token)
    {
        return await GetListAsync<BoatItem>(
            "api/admin/operations/boats", token);
    }

    public async Task<IReadOnlyList<InstructorItem>> GetInstructorsAsync(
        string token)
    {
        return await GetListAsync<InstructorItem>(
            "api/admin/operations/instructors", token);
    }

    public async Task<IReadOnlyList<InstructorItem>>
        GetPublicInstructorsAsync()
    {
        return await client.GetFromJsonAsync<List<InstructorItem>>(
            "api/public/instructors") ?? [];
    }

    public Task<HttpStatusCode> CreateBoatAsync(
        string token, SaveBoatRequest data) =>
        SendAsync(HttpMethod.Post,
            "api/admin/operations/boats", token, data);

    public Task<HttpStatusCode> UpdateBoatAsync(
        string token, int id, SaveBoatRequest data) =>
        SendAsync(HttpMethod.Put,
            $"api/admin/operations/boats/{id}", token, data);

    public Task<HttpStatusCode> SetBoatActiveAsync(
        string token, int id, bool active) =>
        SendAsync(HttpMethod.Patch,
            $"api/admin/operations/boats/{id}/active",
            token, new SetActiveRequest(active));

    public Task<HttpStatusCode> CreateInstructorAsync(
        string token, SaveInstructorRequest data) =>
        SendAsync(HttpMethod.Post,
            "api/admin/operations/instructors", token, data);

    public Task<HttpStatusCode> UpdateInstructorAsync(
        string token, int id, SaveInstructorRequest data) =>
        SendAsync(HttpMethod.Put,
            $"api/admin/operations/instructors/{id}", token, data);

    public Task<HttpStatusCode> SetInstructorActiveAsync(
        string token, int id, bool active) =>
        SendAsync(HttpMethod.Patch,
            $"api/admin/operations/instructors/{id}/active",
            token, new SetActiveRequest(active));

    public Task<HttpStatusCode> SetInstructorPublishedAsync(
        string token, int id, bool published) =>
        SendAsync(HttpMethod.Patch,
            $"api/admin/operations/instructors/{id}/publication",
            token, new SetPublishedRequest(published));

    private async Task<IReadOnlyList<T>> GetListAsync<T>(
        string url, string token)
    {
        using var request = Authorized(
            HttpMethod.Get, url, token);

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<T>>() ?? [];
    }

    private async Task<HttpStatusCode> SendAsync<T>(
        HttpMethod method, string url, string token, T data)
    {
        using var request = Authorized(method, url, token);
        request.Content = JsonContent.Create(data);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    private static HttpRequestMessage Authorized(
        HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
