using Inventory.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Orders;

public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/orders");
        group.MapPost("/", CreateAsync).AddEndpointFilter<ValidationFilter<CreateTransferOrderRequest>>();
        return app;
    }

    // The validator has run, so every code is present; trimmed here so the store matches stored codes.
    private static async Task<Created<TransferOrder>> CreateAsync(
        CreateTransferOrderRequest request, TransferService service, CancellationToken cancellationToken)
    {
        TransferCommand command = new(
            request.ProductCode!.Trim(),
            request.SourceWarehouseCode!.Trim(),
            request.DestinationWarehouseCode!.Trim(),
            request.Quantity);
        TransferOrder order = await service.TransferAsync(command, cancellationToken);
        // No Location: there is no GET /orders/{id} to point at.
        return TypedResults.Created((string?)null, order);
    }
}
