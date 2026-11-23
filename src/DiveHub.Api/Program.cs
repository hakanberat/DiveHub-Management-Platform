using DiveHub.Application;
using DiveHub.Domain;
using DiveHub.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var seedAdmin = args.Contains("--seed-admin");

var hostArgs = args
    .Where(x => x != "--seed-admin")
    .ToArray();

var builder = WebApplication.CreateBuilder(hostArgs);

var connectionString =
    builder.Configuration.GetConnectionString("DiveHub")
    ?? throw new InvalidOperationException(
        "DiveHub connection string is missing.");

builder.Services.AddDbContext<DiveHubDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IContentService, ContentService>();
builder.Services.AddScoped<IActivityCatalogService, ActivityCatalogService>();
builder.Services.AddScoped<IOperationsCatalogService, OperationsCatalogService>();
builder.Services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
builder.Services.AddScoped<IDiveSiteEditor, DiveSiteEditor>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(15);

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<DiveHubDbContext>()
    .AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.BearerScheme)
    .AddBearerToken(IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromHours(8);
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"))
    .AddPolicy("CustomerOnly", policy =>
        policy.RequireRole("Customer"));

builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("auth", context =>
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

var app = builder.Build();

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Development-only administrator initialization.
if (seedAdmin)
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Admin bootstrap is Development-only.");

    await using var scope = app.Services.CreateAsyncScope();

    var db = scope.ServiceProvider
        .GetRequiredService<DiveHubDbContext>();

    await db.Database.MigrateAsync();

    var roles = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (var name in new[] { "Admin", "Instructor", "Customer" })
    {
        if (!await roles.RoleExistsAsync(name))
        {
            var result = await roles.CreateAsync(
                new IdentityRole<Guid>(name));

            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Cannot create role: {name}");
        }
    }

    var email = builder.Configuration["BootstrapAdmin:Email"];
    var password = builder.Configuration["BootstrapAdmin:Password"];

    if (string.IsNullOrWhiteSpace(email) ||
        string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "BootstrapAdmin credentials are required.");
    }

    var users = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    if (await users.FindByEmailAsync(email) is not null)
    {
        throw new InvalidOperationException(
            "Account already exists. Bootstrap will not promote it.");
    }

    var admin = new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = email.Trim(),
        Email = email.Trim(),
        FullName = "Dive Hub Administrator",
        EmailConfirmed = true
    };

    var created = await users.CreateAsync(admin, password);

    if (!created.Succeeded)
    {
        throw new InvalidOperationException(
            string.Join("; ",
                created.Errors.Select(x => x.Description)));
    }

    var assigned = await users.AddToRoleAsync(admin, "Admin");

    if (!assigned.Succeeded)
    {
        throw new InvalidOperationException(
            "Admin role assignment failed.");
    }

    Console.WriteLine("Dive Hub administrator created.");
    return;
}

// Health checks.
app.MapGet("/health", () =>
    Results.Ok(new
    {
        status = "running",
        app = "Dive Hub"
    }));

app.MapGet("/health/db", async (DiveHubDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { database = "connected" })
        : Results.Problem(
            title: "Database unavailable",
            statusCode: 503));

// Customer registration.
app.MapPost("/api/auth/register",
    async (RegisterRequest request,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles) =>
    {
        if (string.IsNullOrWhiteSpace(request.FullName) ||
            request.FullName.Trim().Length is < 2 or > 200 ||
            string.IsNullOrWhiteSpace(request.Email) ||
            request.Email.Trim().Length > 254 ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(
                new { error = "Invalid registration data." });
        }

        if (!await roles.RoleExistsAsync("Customer"))
        {
            return Results.Problem(
                title: "Account setup unavailable",
                statusCode: 503);
        }

        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim()
        };

        var result = await users.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(x => x.Code)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(e => e.Description).ToArray()));
        }

        var roleResult = await users.AddToRoleAsync(
            user,
            "Customer");

        if (!roleResult.Succeeded)
        {
            return Results.Problem(
                title: "Account role assignment failed",
                statusCode: 500);
        }

        return Results.Created(
            "/api/auth/me",
            new
            {
                user.Id,
                user.Email
            });
    })
    .RequireRateLimiting("auth");

// Login: ASP.NET Core Identity bearer token.
app.MapPost("/api/auth/login",
    async (LoginRequest request,
        SignInManager<ApplicationUser> signIn) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Unauthorized();
        }

        signIn.AuthenticationScheme =
            IdentityConstants.BearerScheme;

        var result = await signIn.PasswordSignInAsync(
            request.Email.Trim(),
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        return result.Succeeded
            ? Results.Empty
            : Results.Unauthorized();
    })
    .RequireRateLimiting("auth");

// Current authenticated user, including actual Identity roles.
app.MapGet("/api/auth/me",
    async (System.Security.Claims.ClaimsPrincipal principal,
        UserManager<ApplicationUser> users) =>
    {
        var user = await users.GetUserAsync(principal);

        if (user is null)
            return Results.Unauthorized();

        var roles = await users.GetRolesAsync(user);

        return Results.Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            Roles = roles
        });
    })
    .RequireAuthorization();

// Public content.
var publicApi = app.MapGroup("/api/public");

publicApi.MapGet("/site",
    async (IContentService service) =>
        Results.Ok(await service.GetSiteAsync()));

publicApi.MapGet("/dive-sites",
    async (IContentService service) =>
        Results.Ok(await service.GetDiveSitesAsync()));

publicApi.MapGet("/activities",
    async (IContentService service) =>
        Results.Ok(await service.GetActivitiesAsync()));

// Admin content management.
var adminApi = app.MapGroup("/api/admin")
    .RequireAuthorization("AdminOnly");

adminApi.MapGet("/site",
    async (IContentService service) =>
        Results.Ok(await service.GetSiteAsync()));

adminApi.MapPut("/site",
    async (UpdateSiteRequest request,
        IContentService service) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 160 ||
            request.Description is null ||
            request.Description.Length > 4000 ||
            request.Address is null ||
            request.Address.Length > 500 ||
            request.Phone is null ||
            request.Phone.Length > 50 ||
            request.Email is null ||
            request.Email.Length > 254 ||
            request.WhatsApp is null ||
            request.WhatsApp.Length > 50)
        {
            return Results.BadRequest(
                new { error = "Invalid site information." });
        }

        return Results.Ok(
            await service.UpdateSiteAsync(request));
    });

adminApi.MapGet("/dive-sites",
    async (IContentService service) =>
        Results.Ok(await service.GetDiveSitesAsync(true)));

adminApi.MapPost("/dive-sites",
    async (CreateDiveSiteRequest request,
        IContentService service) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 160 ||
            request.Description is null ||
            request.Description.Length > 4000 ||
            request.MaximumDepthMeters is < 0 or > 400)
        {
            return Results.BadRequest(
                new { error = "Invalid dive site." });
        }

        var created = await service.CreateDiveSiteAsync(request);

        return Results.Created(
            $"/api/admin/dive-sites/{created.Id}",
            created);
    });

adminApi.MapGet("/activities",
    async (IContentService service) =>
        Results.Ok(await service.GetActivitiesAsync(true)));

adminApi.MapPost("/activities",
    async (CreateActivityRequest request,
        IContentService service) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 160 ||
            request.Description is null ||
            request.Description.Length > 4000 ||
            !Enum.IsDefined(request.Kind) ||
            request.Price < 0 ||
            request.Price > 999999999 ||
            request.Currency is null ||
            !new[] { "TRY", "EUR", "USD", "GBP" }
                .Contains(request.Currency.Trim().ToUpperInvariant()))
        {
            return Results.BadRequest(
                new { error = "Invalid activity." });
        }

        var created = await service.CreateActivityAsync(request);

        return Results.Created(
            $"/api/admin/activities/{created.Id}",
            created);
    });

// Existing application endpoints.
DiveHub.Api.AdminDiveSiteEndpoints
    .MapAdminDiveSiteEndpoints(app);

DiveHub.Api.DiveSitePhotoEndpoints
    .MapDiveSitePhotoEndpoints(app);

DiveHub.Api.ActivityCatalogEndpoints
    .MapActivityCatalogEndpoints(app);

DiveHub.Api.ActivityPhotoEndpoints
    .MapActivityPhotoEndpoints(app);

DiveHub.Api.OperationsCatalogEndpoints
    .MapOperationsCatalogEndpoints(app);

DiveHub.Api.InstructorPhotoEndpoints
    .MapInstructorPhotoEndpoints(app);

DiveHub.Api.ReservationEndpoints
    .MapReservationEndpoints(app);

DiveHub.Api.BookableActivityEndpoints
    .MapBookableActivityEndpoints(app);

DiveHub.Api.ReservationDashboardEndpoints
    .MapReservationDashboardEndpoints(app);

DiveHub.Api.ReservationResourceEndpoints
    .MapReservationResourceEndpoints(app);

DiveHub.Api.ActivityBoatRequirementEndpoints
    .MapActivityBoatRequirementEndpoints(app);

app.Run();