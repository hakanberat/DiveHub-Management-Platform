
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DiveHub.Application;

namespace DiveHub.Web.Services;

public sealed class InstructorPhotoApiClient(HttpClient client)
{
    public async Task<InstructorPhotoDto?> GetPublicAsync(
        int instructorId)
    {
        using var response = await client.GetAsync(
            $"api/public/instructors/{instructorId}/photo");

        if (response.StatusCode is
            HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<InstructorPhotoDto>();
    }

    public async Task<InstructorPhotoDto?> GetAdminAsync(
        string token,
        int instructorId)
    {
        using var request = Authorized(
            HttpMethod.Get,
            $"api/admin/instructors/{instructorId}/photo",
            token);

        using var response = await client.SendAsync(request);

        if (response.StatusCode is
            HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<InstructorPhotoDto>();
    }

    public async Task<HttpStatusCode> UploadAsync(
        string token,
        int instructorId,
        IFormFile file,
        string altText)
    {
        using var request = Authorized(
            HttpMethod.Post,
            $"api/admin/instructors/{instructorId}/photo",
            token);

        using var form = new MultipartFormDataContent();

        form.Add(
            new StreamContent(file.OpenReadStream()),
            "Photo",
            file.FileName);

        form.Add(
            new StringContent(altText),
            "AltText");

        request.Content = form;

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task<HttpStatusCode> DeleteAsync(
        string token,
        int instructorId)
    {
        using var request = Authorized(
            HttpMethod.Delete,
            $"api/admin/instructors/{instructorId}/photo",
            token);

        using var response = await client.SendAsync(request);

        return response.StatusCode;
    }

    public async Task ProxyAsync(
        HttpContext context,
        int instructorId,
        string? token)
    {
        bool isAdmin = !string.IsNullOrWhiteSpace(token);

        string url = isAdmin
            ? $"api/admin/instructors/{instructorId}/photo/file"
            : $"api/public/instructors/{instructorId}/photo/file";

        using var request = new HttpRequestMessage(
            HttpMethod.Get, url);

        if (isAdmin)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", token);
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
            context.Response.Headers.CacheControl = "no-store";

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
            new AuthenticationHeaderValue(
                "Bearer", token);

        return request;
    }
}
