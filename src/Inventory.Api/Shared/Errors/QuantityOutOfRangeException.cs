namespace Inventory.Api.Shared.Errors;

public sealed class QuantityOutOfRangeException()
    : DomainException("The resulting stock level is out of range.", StatusCodes.Status400BadRequest, "quantity_out_of_range");
