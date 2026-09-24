using Inventory.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Stock;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/stock").WithTags("Stock").ProducesProblem(StatusCodes.Status401Unauthorized);
        group.MapPost("/", ReceiveAsync)
            .WithSummary("Receive stock into a linked warehouse; returns the new level.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .AddEndpointFilter<ValidationFilter<ReceiveStockRequest>>();
        group.MapGet("/", ListAsync)
            .WithSummary("List stock levels in linked warehouses by product, warehouse or both.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AddEndpointFilter<ValidationFilter<StockQuery>>();
        return app;
    }

    // 200, not 201: a receipt changes a level but creates no new addressable resource.
    // The validator has run, so both codes are present.
    private static async Task<Ok<StockReceipt>> ReceiveAsync(
        ReceiveStockRequest request, StockStore store, CancellationToken cancellationToken)
    {
        string productCode = request.ProductCode!.Trim();
        string warehouseCode = request.WarehouseCode!.Trim();
        int level = await store.ReceiveAsync(productCode, warehouseCode, request.Quantity, cancellationToken);
        return TypedResults.Ok(new StockReceipt(productCode, warehouseCode, level));
    }

    private static async Task<Ok<IReadOnlyList<StockLevel>>> ListAsync(
        [AsParameters] StockQuery query, StockStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(query.ProductCode?.Trim(), query.WarehouseCode?.Trim(), cancellationToken));
}
