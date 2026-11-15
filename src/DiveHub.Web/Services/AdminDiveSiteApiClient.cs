
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;
using DiveHub.Domain;

namespace DiveHub.Web.Services;

public sealed class AdminDiveSiteApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<DiveSite>> ListAsync(
        string token)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "api/admin/dive-sites",
            token);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<DiveSite>>() ?? [];
    }

    public Task<HttpStatusCode> CreateAsync(
        string token,
        CreateDiveSiteRequest data)
    {
        return SendAsync(
            HttpMethod.Post,
            "api/admin/dive-sites",
            token,
            data);
    }

    public Task<HttpStatusCode> UpdateAsync(
        string token,
        int id,
        EditDiveSiteRequest data)
    {
        return SendAsync(
            HttpMethod.Put,
            $"api/admin/dive-sites/{id}",
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
            $"api/admin/dive-sites/{id}/publication",
            token,
            new SetDiveSitePublicationRequest(isPublished));
    }

    private async Task<HttpStatusCode> SendAsync<T>(
        HttpMethod method,
        string url,
        string token,
        T data)
    {
        using var request = CreateRequest(method, url, token);

        request.Content = JsonContent.Create(data);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    private static HttpRequestMessage CreateRequest(
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
