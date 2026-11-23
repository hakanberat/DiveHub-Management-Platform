using DiveHub.Application;

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
            if (!ActivityCatalogRules.IsValid(request))
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
            if (id <= 0 ||
                !ActivityCatalogRules.IsValid(request))
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

            try
            {
                var item = await service.SetPublishedAsync(
                    id, request.IsPublished, ct);

                return item is null
                    ? Results.NotFound()
                    : Results.Ok(item);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(
                    new { error = exception.Message });
            }
        });

        return app;
    }
}