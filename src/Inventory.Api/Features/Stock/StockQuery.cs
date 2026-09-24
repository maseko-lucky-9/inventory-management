using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Features.Stock;

/// <summary>The GET /stock filters, bound with [AsParameters] so the validation filter receives them as one object.</summary>
/// <remarks>Named explicitly so the OpenAPI document spells them as the contract does.</remarks>
public sealed record StockQuery(
    [FromQuery(Name = "productCode")] string? ProductCode,
    [FromQuery(Name = "warehouseCode")] string? WarehouseCode);
