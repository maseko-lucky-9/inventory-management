namespace Inventory.Api.Features.Products;

// Nullable so a missing field reaches the validator as a field error, not a null dereference.
public sealed record CreateProductRequest(string? Code, string? Description);
