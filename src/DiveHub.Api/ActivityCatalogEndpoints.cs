
using DiveHub.Application;
using DiveHub.Domain;

namespace DiveHub.Api;

public static class ActivityCatalogEndpoints
{
    public static IEndpointRouteBuilder MapActivityCatalogEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/catalog/activities")
            .RequireAuthorization("AdminOnly");

        group.MapGet("", async (
            IActivityCatalogService service,
            CancellationToken ct) =>
        {
            return Results.Ok(await service.ListAsync(ct));
        });

        group.MapGet("/{id:int}", async (
            int id,
            IActivityCatalogService service,
            CancellationToken ct) =>
        {
            var item = await service.GetAsync(id, ct);

            return item is null
                ? Results.NotFound()
                : Results.Ok(item);
        });

        group.MapPost("", async (
            SaveActivityCatalogRequest request,
            IActivityCatalogService service,
            CancellationToken ct) =>
        {
            if (!IsValid(request))
            {
                return Results.BadRequest(
                    new { error = "Invalid activity information." });
            }

            var item = await service.CreateAsync(request, ct);

            return Results.Created(
                $"/api/admin/catalog/activities/{item.Id}",
                item);
        });

        group.MapPut("/{id:int}", async (
            int id,
            SaveActivityCatalogRequest request,
            IActivityCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0 || !IsValid(request))
            {
                return Results.BadRequest(
                    new { error = "Invalid activity information." });
            }

            var item = await service.UpdateAsync(
                id, request, ct);

            return item is null
                ? Results.NotFound()
                : Results.Ok(item);
        });

        group.MapPatch("/{id:int}/publication", async (
            int id,
            SetActivityPublicationRequest request,
            IActivityCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var item = await service.SetPublishedAsync(
                id, request.IsPublished, ct);

            return item is null
                ? Results.NotFound()
                : Results.Ok(item);
        });

        return app;
    }

    private static bool IsValid(
        SaveActivityCatalogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Trim().Length > 120)
            return false;

        if (request.Description is null ||
            request.Description.Length > 1000)
            return false;

        if (!Enum.IsDefined(request.Kind))
            return false;

        if (request.Price < 0 ||
            request.Price > 999999.99m)
            return false;

        var currency = request.Currency?.Trim().ToUpperInvariant();

        if (currency is null ||
            currency.Length != 3 ||
            currency.Any(x => x < 'A' || x > 'Z'))
            return false;

        return true;
    }
}
