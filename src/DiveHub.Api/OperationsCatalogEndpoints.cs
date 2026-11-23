
using DiveHub.Application;

namespace DiveHub.Api;

public static class OperationsCatalogEndpoints
{
    public static IEndpointRouteBuilder MapOperationsCatalogEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/instructors", async (
            IOperationsCatalogService service,
            CancellationToken ct) =>
                Results.Ok(
                    await service.GetInstructorsAsync(true, ct)));

        var admin = app.MapGroup("/api/admin/operations")
            .RequireAuthorization("AdminOnly");

        // BOATS

        admin.MapGet("/boats", async (
            IOperationsCatalogService service,
            CancellationToken ct) =>
                Results.Ok(await service.GetBoatsAsync(ct)));

        admin.MapPost("/boats", async (
            SaveBoatRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (!ValidBoat(request))
                return Results.BadRequest();

            var boat = await service.CreateBoatAsync(request, ct);

            return Results.Created(
                $"/api/admin/operations/boats/{boat.Id}",
                boat);
        });

        admin.MapPut("/boats/{id:int}", async (
            int id,
            SaveBoatRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0 || !ValidBoat(request))
                return Results.BadRequest();

            var boat = await service.UpdateBoatAsync(
                id, request, ct);

            return boat is null
                ? Results.NotFound()
                : Results.Ok(boat);
        });

        admin.MapPatch("/boats/{id:int}/active", async (
            int id,
            SetActiveRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var boat = await service.SetBoatActiveAsync(
                id, request.IsActive, ct);

            return boat is null
                ? Results.NotFound()
                : Results.Ok(boat);
        });

        // INSTRUCTORS

        admin.MapGet("/instructors", async (
            IOperationsCatalogService service,
            CancellationToken ct) =>
                Results.Ok(
                    await service.GetInstructorsAsync(false, ct)));

        admin.MapPost("/instructors", async (
            SaveInstructorRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (!ValidInstructor(request))
                return Results.BadRequest();

            var instructor = await service.CreateInstructorAsync(
                request, ct);

            return Results.Created(
                $"/api/admin/operations/instructors/{instructor.Id}",
                instructor);
        });

        admin.MapPut("/instructors/{id:int}", async (
            int id,
            SaveInstructorRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0 || !ValidInstructor(request))
                return Results.BadRequest();

            var instructor = await service.UpdateInstructorAsync(
                id, request, ct);

            return instructor is null
                ? Results.NotFound()
                : Results.Ok(instructor);
        });

        admin.MapPatch("/instructors/{id:int}/active", async (
            int id,
            SetActiveRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var instructor = await service.SetInstructorActiveAsync(
                id, request.IsActive, ct);

            return instructor is null
                ? Results.NotFound()
                : Results.Ok(instructor);
        });

        admin.MapPatch("/instructors/{id:int}/publication", async (
            int id,
            SetPublishedRequest request,
            IOperationsCatalogService service,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var instructor =
                await service.SetInstructorPublishedAsync(
                    id, request.IsPublished, ct);

            return instructor is null
                ? Results.NotFound()
                : Results.Ok(instructor);
        });

        return app;
    }

    private static bool ValidBoat(SaveBoatRequest request)
    {
        return request is not null &&
            !string.IsNullOrWhiteSpace(request.Name) &&
            request.Name.Trim().Length <= 100 &&
            request.Capacity >= 1 &&
            request.Capacity <= 250;
    }

    private static bool ValidInstructor(
        SaveInstructorRequest request)
    {
        return request is not null &&
            !string.IsNullOrWhiteSpace(request.FullName) &&
            request.FullName.Trim().Length <= 120 &&
            request.Biography is not null &&
            request.Biography.Length <= 1500 &&
            request.CertificationAgency is not null &&
            request.CertificationAgency.Length <= 100 &&
            request.CertificationNumber is not null &&
            request.CertificationNumber.Length <= 100;
    }
}
