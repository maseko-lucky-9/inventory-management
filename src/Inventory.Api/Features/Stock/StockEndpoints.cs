using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Stock;

public static class StockEndpoints
{
    public static IEndpointRouteBuilder MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/stock");
        group.MapPost("/", ReceiveAsync);
        group.MapGet("/", ListAsync);
        return app;
    }

    // 200, not 201: a receipt changes a level but creates no new addressable resource.
    private static async Task<Ok<StockReceipt>> ReceiveAsync(
        ReceiveStockRequest request, StockStore store, CancellationToken cancellationToken)
    {
        string productCode = request.ProductCode.Trim();
        string warehouseCode = request.WarehouseCode.Trim();
        int level = await store.ReceiveAsync(productCode, warehouseCode, request.Quantity, cancellationToken);
        return TypedResults.Ok(new StockReceipt(productCode, warehouseCode, level));
    }

    private static async Task<Ok<IReadOnlyList<StockLevel>>> ListAsync(
        string? productCode, string? warehouseCode, StockStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(productCode?.Trim(), warehouseCode?.Trim(), cancellationToken));
}
