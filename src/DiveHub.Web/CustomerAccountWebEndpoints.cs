
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace DiveHub.Web;

public static class CustomerAccountWebEndpoints
{
    private const string WebScheme = "DiveHubWeb";

    public static IEndpointRouteBuilder MapCustomerAccountWebEndpoints(
        this IEndpointRouteBuilder app)
    {
        // Keep form POST routes separate from Razor page routes.
        var group = app.MapGroup("/auth/customer");

        group.MapPost("/register", RegisterAsync)
            .RequireRateLimiting("login");

        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("login");

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        DiveHubApiClient api,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid security token.");
        }

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest("Invalid registration form.");
        }

        var form = await context.Request.ReadFormAsync(ct);

        var fullName = form["FullName"].ToString().Trim();
        var email = form["Email"].ToString().Trim();
        var password = form["Password"].ToString();
        var confirmPassword = form["ConfirmPassword"].ToString();

        if (fullName.Length is < 2 or > 200 ||
            email.Length is < 3 or > 254 ||
            !new EmailAddressAttribute().IsValid(email) ||
            password.Length is < 12 or > 128 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            password != confirmPassword)
        {
            return Results.Redirect(
                "/customer/register?error=invalid");
        }

        try
        {
            var status = await api.RegisterCustomerAsync(
                fullName,
                email,
                password,
                ct);

            return status switch
            {
                HttpStatusCode.Created =>
                    Results.Redirect(
                        "/customer/login?registered=1"),

                HttpStatusCode.BadRequest =>
                    Results.Redirect(
                        "/customer/register?error=invalid"),

                HttpStatusCode.TooManyRequests =>
                    Results.Redirect(
                        "/customer/register?error=limit"),

                _ => Results.Redirect(
                    "/customer/register?error=server")
            };
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                "/customer/register?error=server");
        }
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return Results.Redirect(
                "/customer/register?error=server");
        }
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        DiveHubApiClient api,
        CancellationToken ct)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest("Invalid security token.");
        }

        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest("Invalid login form.");
        }

        var form = await context.Request.ReadFormAsync(ct);

        var email = form["Email"].ToString().Trim();
        var password = form["Password"].ToString();

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 254 ||
            string.IsNullOrWhiteSpace(password))
        {
            return Results.Redirect(
                "/customer/login?error=invalid");
        }

        try
        {
            var login = await api.LoginAsync(email, password);

            if (login is null ||
                string.IsNullOrWhiteSpace(login.AccessToken))
            {
                return Results.Redirect(
                    "/customer/login?error=invalid");
            }

            var profile = await api.GetProfileAsync(
                login.AccessToken,
                ct);

            if (profile is null ||
                profile.Id == Guid.Empty ||
                profile.Roles is null ||
                !profile.Roles.Contains(
                    "Customer",
                    StringComparer.Ordinal))
            {
                return Results.Redirect(
                    "/customer/login?error=access");
            }

            var claims = new[]
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    profile.Id.ToString()),

                new Claim(
                    ClaimTypes.Name,
                    profile.FullName ?? email),

                new Claim(
                    ClaimTypes.Email,
                    profile.Email ?? email),

                new Claim(
                    ClaimTypes.Role,
                    "Customer")
            };

            var identity = new ClaimsIdentity(
                claims,
                WebScheme);

            var principal = new ClaimsPrincipal(identity);

            var properties = new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Clamp(
                        login.ExpiresIn,
                        1L,
                        28800L))
            };

            properties.StoreTokens(
            [
                new AuthenticationToken
                {
                    Name = "access_token",
                    Value = login.AccessToken
                }
            ]);

            await context.SignInAsync(
                WebScheme,
                principal,
                properties);

            return Results.Redirect("/account");
        }
        catch (HttpRequestException)
        {
            return Results.Redirect(
                "/customer/login?error=server");
        }
        catch (TaskCanceledException)
            when (!ct.IsCancellationRequested)
        {
            return Results.Redirect(
                "/customer/login?error=server");
        }
        catch (System.Text.Json.JsonException)
        {
            return Results.Redirect(
                "/customer/login?error=server");
        }
    }
}
