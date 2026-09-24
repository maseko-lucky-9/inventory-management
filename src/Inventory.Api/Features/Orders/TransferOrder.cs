namespace Inventory.Api.Features.Orders;

public sealed record TransferOrder(
    long Id,
    string ProductCode,
    string SourceWarehouseCode,
    string DestinationWarehouseCode,
    int Quantity,
    DateTime CreatedAt);
