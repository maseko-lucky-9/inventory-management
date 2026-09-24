using FluentValidation;
using Inventory.Api.Shared.Validation;

namespace Inventory.Api.Features.Orders;

public sealed class TransferOrderValidator : AbstractValidator<CreateTransferOrderRequest>
{
    public TransferOrderValidator()
    {
        // One message per field: a missing code is not also reported as a pattern mismatch.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.ProductCode).MustBeACode();
        RuleFor(request => request.SourceWarehouseCode).MustBeACode();
        RuleFor(request => request.DestinationWarehouseCode).MustBeACode()
            .Must((request, destination) => !IsSameCode(destination, request.SourceWarehouseCode))
            .WithMessage("'{PropertyName}' must differ from the source warehouse code: a self-transfer is not allowed.");
        RuleFor(request => request.Quantity).GreaterThan(0);
    }

    // Ordinal, like the database's unique code: "wh-a" and "WH-A" are different warehouses.
    private static bool IsSameCode(string? destination, string? source) =>
        string.Equals(destination?.Trim(), source?.Trim(), StringComparison.Ordinal);
}
