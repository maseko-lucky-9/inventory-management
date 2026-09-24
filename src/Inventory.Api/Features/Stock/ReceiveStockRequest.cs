namespace Inventory.Api.Features.Stock;

/// <summary>Goods received into a warehouse; the quantity is added to the current level.</summary>
/// <remarks>Codes are nullable so a missing one reaches the validator as a field error, not a null dereference.</remarks>
public sealed record ReceiveStockRequest(string? ProductCode, string? WarehouseCode, int Quantity);
