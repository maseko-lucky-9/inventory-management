namespace Inventory.Api.Features.Warehouses;

// Nullable so a missing field reaches the validator as a field error, not a null dereference.
public sealed record CreateWarehouseRequest(string? Code, string? Name);
