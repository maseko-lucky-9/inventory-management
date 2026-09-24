namespace Inventory.Api.Features.Orders;

// Nullable so a missing field reaches the validator as a field error, not a binding failure.
public sealed record CreateTransferOrderRequest(
    string? ProductCode,
    string? SourceWarehouseCode,
    string? DestinationWarehouseCode,
    int Quantity);
