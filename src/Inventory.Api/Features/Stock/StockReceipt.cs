namespace Inventory.Api.Features.Stock;

/// <summary>The level after a receipt, not the quantity received.</summary>
public sealed record StockReceipt(string ProductCode, string WarehouseCode, int Quantity);
