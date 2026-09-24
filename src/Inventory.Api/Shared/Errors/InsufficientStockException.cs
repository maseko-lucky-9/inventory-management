namespace Inventory.Api.Shared.Errors;

public sealed class InsufficientStockException(string productCode, string sourceCode, int requested, int available)
    : DomainException(
        $"Insufficient stock of {productCode} in {sourceCode}: requested {requested}, available {available}.",
        StatusCodes.Status400BadRequest,
        "insufficient_stock");
