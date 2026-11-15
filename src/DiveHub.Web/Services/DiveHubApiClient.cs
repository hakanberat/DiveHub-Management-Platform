
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;
using DiveHub.Domain;

namespace DiveHub.Web.Services;

public sealed class ApiLoginResponse
{
    public string AccessToken { get; set; } = "";
    public long ExpiresIn { get; set; }
}

public sealed class DiveHubApiClient(HttpClient client)
{
    public async Task<ApiLoginResponse?> LoginAsync(
        string email, string password)
    {
        using var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(email, password));

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<ApiLoginResponse>();
    }

    public async Task<bool> IsAdminAsync(string token)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Get, "api/admin/site", token);

        using var response = await client.SendAsync(request);

        return response.IsSuccessStatusCode;
    }

    public Task<SiteSettings?> GetPublicSiteAsync()
    {
        return client.GetFromJsonAsync<SiteSettings>(
            "api/public/site");
    }

    public async Task<SiteSettings?> GetAdminSiteAsync(
        string token)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Get, "api/admin/site", token);

        using var response = await client.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        return await response.Content
            .ReadFromJsonAsync<SiteSettings>();
    }

    public async Task<HttpStatusCode> UpdateSiteAsync(
        string token, UpdateSiteRequest data)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Put, "api/admin/site", token);

        request.Content = JsonContent.Create(data);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task<IReadOnlyList<DiveSite>> GetDiveSitesAsync()
    {
        var sites = await client.GetFromJsonAsync<List<DiveSite>>(
            "api/public/dive-sites");

        return sites ?? [];
    }

    public async Task<IReadOnlyList<Activity>> GetActivitiesAsync()
    {
        var activities = await client.GetFromJsonAsync<List<Activity>>(
            "api/public/activities");

        return activities ?? [];
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return request;
    }
}
