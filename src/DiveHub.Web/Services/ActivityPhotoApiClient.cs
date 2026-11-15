
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;

namespace DiveHub.Web.Services;

public sealed class ActivityPhotoApiClient(HttpClient client)
{
    public async Task<IReadOnlyList<ActivityPhotoDto>> GetPublicAsync(
        int activityId)
    {
        using var response = await client.GetAsync(
            $"api/public/activities/{activityId}/photos");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<ActivityPhotoDto>>() ?? [];
    }

    public async Task<IReadOnlyList<ActivityPhotoDto>> GetAdminAsync(
        string token,
        int activityId)
    {
        using var request = Authorized(
            HttpMethod.Get,
            $"api/admin/activities/{activityId}/photos",
            token);

        using var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<List<ActivityPhotoDto>>() ?? [];
    }

    public async Task<HttpStatusCode> UploadAsync(
        string token,
        int activityId,
        IFormFile file,
        string altText)
    {
        using var request = Authorized(
            HttpMethod.Post,
            $"api/admin/activities/{activityId}/photos",
            token);

        using var multipart = new MultipartFormDataContent();

        multipart.Add(
            new StreamContent(file.OpenReadStream()),
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
        int activityId,
        long photoId)
    {
        using var request = Authorized(
            HttpMethod.Delete,
            $"api/admin/activities/{activityId}/photos/{photoId}",
            token);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task ProxyPhotoAsync(
        HttpContext context,
        int activityId,
        long photoId,
        string? token)
    {
        var isAdmin = !string.IsNullOrWhiteSpace(token);

        var url = isAdmin
            ? $"api/admin/activities/{activityId}/photos/{photoId}/file"
            : $"api/public/activities/{activityId}/photos/{photoId}/file";

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
            context.Response.Headers.CacheControl =
                "private, no-store";

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
