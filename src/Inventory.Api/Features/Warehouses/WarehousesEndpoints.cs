using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Warehouses;

public static class WarehousesEndpoints
{
    public static IEndpointRouteBuilder MapWarehousesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/warehouses");
        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        return app;
    }

    private static async Task<Ok<IReadOnlyList<Warehouse>>> ListAsync(WarehouseStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(cancellationToken));

    // There is no GET /warehouses/{code}, so the 201 carries no Location header.
    private static async Task<Created<Warehouse>> CreateAsync(
        CreateWarehouseRequest request, WarehouseStore store, CancellationToken cancellationToken)
    {
        Warehouse warehouse = await store.CreateAsync(request.Code.Trim(), request.Name, cancellationToken);
        return TypedResults.Created((string?)null, warehouse);
    }
}
