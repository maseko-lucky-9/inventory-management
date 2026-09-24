namespace Inventory.Api.Features.Stock;

/// <summary>Goods received into a warehouse; the quantity is added to the current level.</summary>
public sealed record ReceiveStockRequest(string ProductCode, string WarehouseCode, int Quantity);
