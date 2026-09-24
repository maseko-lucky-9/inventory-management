using Inventory.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Warehouses;

public static class WarehousesEndpoints
{
    public static IEndpointRouteBuilder MapWarehousesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/warehouses").WithTags("Warehouses").ProducesProblem(StatusCodes.Status401Unauthorized);
        group.MapGet("/", ListAsync).WithSummary("List the warehouses linked to the caller.");
        group.MapPost("/", CreateAsync)
            .WithSummary("Create a warehouse and link it to the caller.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .AddEndpointFilter<ValidationFilter<CreateWarehouseRequest>>();
        return app;
    }

    private static async Task<Ok<IReadOnlyList<Warehouse>>> ListAsync(WarehouseStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(cancellationToken));

    // There is no GET /warehouses/{code}, so the 201 carries no Location header.
    // The validator has run, so both fields are present.
    private static async Task<Created<Warehouse>> CreateAsync(
        CreateWarehouseRequest request, WarehouseStore store, CancellationToken cancellationToken)
    {
        Warehouse warehouse = await store.CreateAsync(request.Code!.Trim(), request.Name!, cancellationToken);
        return TypedResults.Created((string?)null, warehouse);
    }
}
