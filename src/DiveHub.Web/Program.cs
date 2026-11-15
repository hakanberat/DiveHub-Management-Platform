
using System.Security.Claims;
using System.Threading.RateLimiting;
using DiveHub.Application;
using DiveHub.Web.Components;
using DiveHub.Web.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

const string WebScheme = "DiveHubWeb";

builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();

builder.Services.AddHttpClient<DiveHubApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(15);
});


builder.Services.AddHttpClient<DiveSitePhotoApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(60);
});



builder.Services.AddHttpClient<AdminDiveSiteApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(15);
});


builder.Services.AddHttpClient<AdminActivityCatalogApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<ActivityPhotoApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(60);
});


builder.Services.AddHttpClient<OperationsCatalogApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<InstructorPhotoApiClient>(client =>
{
    var url = builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5080/";

    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(60);
});




builder.Services
    .AddAuthentication(WebScheme)
    .AddCookie(WebScheme, options =>
    {
        options.Cookie.Name = "DiveHub.Web.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy =
            builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login?error=access";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpClient<DiveHub.Web.Services.ReservationAdminApiClient>(
    client =>
    {
        var apiBaseUrl =
            builder.Configuration["Api:BaseUrl"]
            ?? builder.Configuration["ApiBaseUrl"]
            ?? "http://localhost:5080/";

        client.BaseAddress = new Uri(
            apiBaseUrl,
            UriKind.Absolute);
    });
builder.Services.AddHttpClient<DiveHub.Web.Services.CustomerBookingApiClient>(
    client =>
    {
        var url = builder.Configuration["Api:BaseUrl"]
            ?? builder.Configuration["ApiBaseUrl"]
            ?? "http://localhost:5080/";

        client.BaseAddress = new Uri(url, UriKind.Absolute);
    });
builder.Services.AddHttpClient<DiveHub.Web.Services.ReservationAvailabilityApiClient>(
    client =>
    {
        var url =
            builder.Configuration["Api:BaseUrl"]
            ?? builder.Configuration["ApiBaseUrl"]
            ?? "http://localhost:5080/";

        client.BaseAddress = new Uri(url, UriKind.Absolute);
    });
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method))
    {
        var antiforgery = context.RequestServices
            .GetRequiredService<IAntiforgery>();

        var tokens = antiforgery.GetAndStoreTokens(context);

        context.Items["DiveHub.CsrfToken"] =
            tokens.RequestToken;
    }

    await next(context);
});
app.UseAntiforgery();

// ADMIN LOGIN
app.MapPost("/account/login", async (
    HttpContext context,
    IAntiforgery antiforgery,
    DiveHubApiClient api) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest("Invalid security token.");

    var form = await context.Request.ReadFormAsync();

    var email = form["Email"].ToString().Trim();
    var password = form["Password"].ToString();

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
        return Results.Redirect("/login?error=invalid");

    try
    {
        var token = await api.LoginAsync(email, password);

        if (token is null ||
            string.IsNullOrWhiteSpace(token.AccessToken))
            return Results.Redirect("/login?error=invalid");

        // Web paneline sadece Admin girebilir.
        if (!await api.IsAdminAsync(token.AccessToken))
            return Results.Redirect("/login?error=access");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(
            claims, WebScheme);

        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            AllowRefresh = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(
                Math.Clamp(token.ExpiresIn, 1, 28800))
        };

        properties.StoreTokens(
        [
            new AuthenticationToken
            {
                Name = "access_token",
                Value = token.AccessToken
            }
        ]);

        await context.SignInAsync(
            WebScheme, principal, properties);

        return Results.Redirect("/admin");
    }
    catch (HttpRequestException)
    {
        return Results.Redirect("/login?error=server");
    }
    catch (TaskCanceledException)
    {
        return Results.Redirect("/login?error=server");
    }
})
.RequireRateLimiting("login");

// LOGOUT
app.MapPost("/account/logout", async (
    HttpContext context,
    IAntiforgery antiforgery) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest("Invalid security token.");

    await context.SignOutAsync(WebScheme);

    return Results.Redirect("/");
})
.RequireAuthorization();

// UPDATE SITE SETTINGS
app.MapPost("/admin/site/save", async (
    HttpContext context,
    IAntiforgery antiforgery,
    DiveHubApiClient api) =>
{
    if (!await antiforgery.IsRequestValidAsync(context))
        return Results.BadRequest("Invalid security token.");

    var token = await context.GetTokenAsync(
        WebScheme, "access_token");

    if (string.IsNullOrWhiteSpace(token))
        return Results.Redirect("/login?error=expired");

    var form = await context.Request.ReadFormAsync();

    var request = new UpdateSiteRequest(
        Name: form["Name"].ToString(),
        Description: form["Description"].ToString(),
        Address: form["Address"].ToString(),
        Phone: form["Phone"].ToString(),
        Email: form["Email"].ToString(),
        WhatsApp: form["WhatsApp"].ToString()
    );

    if (string.IsNullOrWhiteSpace(request.Name) ||
        request.Name.Length > 160 ||
        request.Description.Length > 4000 ||
        request.Address.Length > 500 ||
        request.Phone.Length > 50 ||
        request.Email.Length > 254 ||
        request.WhatsApp.Length > 50)
        return Results.Redirect("/admin?status=invalid");

    try
    {
        var result = await api.UpdateSiteAsync(token, request);

        if (result == System.Net.HttpStatusCode.Unauthorized ||
            result == System.Net.HttpStatusCode.Forbidden)
            return Results.Redirect("/login?error=expired");

        return Results.Redirect(result == System.Net.HttpStatusCode.OK
            ? "/admin?status=saved"
            : "/admin?status=error");
    }
    catch (HttpRequestException)
    {
        return Results.Redirect("/admin?status=error");
    }
    catch (TaskCanceledException)
    {
        return Results.Redirect("/admin?status=error");
    }
})
.RequireAuthorization("AdminOnly");

app.MapStaticAssets();
DiveHub.Web.AdminDiveSiteWebEndpoints.MapAdminDiveSiteWebEndpoints(app);
DiveHub.Web.DiveSitePhotoWebEndpoints.MapDiveSitePhotoWebEndpoints(app);
DiveHub.Web.AdminActivityCatalogWebEndpoints.MapAdminActivityCatalogWebEndpoints(app);
DiveHub.Web.ActivityPhotoWebEndpoints.MapActivityPhotoWebEndpoints(app);
DiveHub.Web.OperationsCatalogWebEndpoints.MapOperationsCatalogWebEndpoints(app);
DiveHub.Web.InstructorPhotoWebEndpoints.MapInstructorPhotoWebEndpoints(app);
app.MapRazorComponents<App>();

DiveHub.Web.AdminReservationWebEndpoints.MapAdminReservationWebEndpoints(app);
DiveHub.Web.CustomerBookingWebEndpoints.MapCustomerBookingWebEndpoints(app);
app.Run();

