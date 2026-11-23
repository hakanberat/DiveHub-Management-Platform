
using DiveHub.Application;

namespace DiveHub.Api;

public static class AdminDiveSiteEndpoints
{
    public static IEndpointRouteBuilder MapAdminDiveSiteEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/dive-sites")
            .RequireAuthorization("AdminOnly");

        group.MapPut("/{id:int}", async (
            int id,
            EditDiveSiteRequest request,
            IDiveSiteEditor editor,
            CancellationToken ct) =>
        {
            if (id <= 0 ||
                string.IsNullOrWhiteSpace(request.Name) ||
                request.Name.Length > 160 ||
                request.Description is null ||
                request.Description.Length > 4000 ||
                request.MaximumDepthMeters is < 1 or > 400)
            {
                return Results.BadRequest(
                    new { error = "Invalid dive site information." });
            }

            var site = await editor.UpdateAsync(
                id, request, ct);

            return site is null
                ? Results.NotFound()
                : Results.Ok(site);
        });

        group.MapPatch("/{id:int}/publication", async (
            int id,
            SetDiveSitePublicationRequest request,
            IDiveSiteEditor editor,
            CancellationToken ct) =>
        {
            if (id <= 0)
                return Results.BadRequest();

            var site = await editor.SetPublishedAsync(
                id, request.IsPublished, ct);

            return site is null
                ? Results.NotFound()
                : Results.Ok(site);
        });

        return app;
    }
}
