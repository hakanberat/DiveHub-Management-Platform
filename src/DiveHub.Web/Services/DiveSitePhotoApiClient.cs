
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;

namespace DiveHub.Web.Services;

public sealed class DiveSitePhotoApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<DiveSitePhotoDto>> GetPublicAsync(
        int siteId)
    {
        using var response = await client.GetAsync(
            $"api/public/dive-sites/{siteId}/photos");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<DiveSitePhotoDto>>() ?? [];
    }

    public async Task<IReadOnlyList<DiveSitePhotoDto>> GetAdminAsync(
        string token,
        int siteId)
    {
        using var request = Authorized(
            HttpMethod.Get,
            $"api/admin/dive-sites/{siteId}/photos",
            token);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<DiveSitePhotoDto>>() ?? [];
    }

    public async Task<HttpStatusCode> UploadAsync(
        string token,
        int siteId,
        IFormFile file,
        string altText)
    {
        using var request = Authorized(
            HttpMethod.Post,
            $"api/admin/dive-sites/{siteId}/photos",
            token);

        using var multipart = new MultipartFormDataContent();

        var fileContent = new StreamContent(
            file.OpenReadStream());

        multipart.Add(
            fileContent,
            "Photo",
            file.FileName);

        multipart.Add(
            new StringContent(altText),
            "AltText");

        request.Content = multipart;

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteAsync(
        string token,
        int siteId,
        long photoId)
    {
        using var request = Authorized(
            HttpMethod.Delete,
            $"api/admin/dive-sites/{siteId}/photos/{photoId}",
            token);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task ProxyPhotoAsync(
        HttpContext context,
        int siteId,
        long photoId,
        string? token)
    {
        var isAdmin = !string.IsNullOrWhiteSpace(token);

        var url = isAdmin
            ? $"api/admin/dive-sites/{siteId}/photos/{photoId}/file"
            : $"api/public/dive-sites/{siteId}/photos/{photoId}/file";

        using var request = new HttpRequestMessage(
            HttpMethod.Get, url);

        if (isAdmin)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode =
                    (int)response.StatusCode;

                return;
            }

            context.Response.ContentType = "image/webp";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.CacheControl = isAdmin
                ? "private, no-store"
                : "public, max-age=300";

            await response.Content.CopyToAsync(
                context.Response.Body,
                context.RequestAborted);
        }
        catch (HttpRequestException)
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 502;
        }
        catch (TaskCanceledException)
            when (!context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = 504;
        }
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
