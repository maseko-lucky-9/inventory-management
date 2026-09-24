namespace Inventory.Api.Features.Orders;

/// <summary>A validated transfer with trimmed codes: what the store executes.</summary>
public sealed record TransferCommand(
    string ProductCode,
    string SourceWarehouseCode,
    string DestinationWarehouseCode,
    int Quantity);
