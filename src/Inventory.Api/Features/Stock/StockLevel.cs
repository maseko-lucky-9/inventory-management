namespace Inventory.Api.Features.Stock;

/// <summary>One product's level in one warehouse; a drained row reads 0.</summary>
public sealed record StockLevel(string ProductCode, string WarehouseCode, int Quantity, DateTime UpdatedAt);
