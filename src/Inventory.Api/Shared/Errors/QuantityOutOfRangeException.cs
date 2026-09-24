namespace Inventory.Api.Shared.Errors;

public sealed class QuantityOutOfRangeException(Exception? cause = null)
    : DomainException("The resulting stock level is out of range.", StatusCodes.Status400BadRequest, "quantity_out_of_range", cause);
