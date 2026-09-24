using System.Text.RegularExpressions;
using FluentValidation;

namespace Inventory.Api.Features.Orders;

public sealed partial class TransferOrderValidator : AbstractValidator<CreateTransferOrderRequest>
{
    private const string CodeRule = "'{PropertyName}' must be 1-50 letters, digits, '.', '_' or '-', starting with a letter or digit.";

    public TransferOrderValidator()
    {
        // One message per field: a missing code is not also reported as a pattern mismatch.
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.ProductCode).NotEmpty().Must(BeAValidCode).WithMessage(CodeRule);
        RuleFor(request => request.SourceWarehouseCode).NotEmpty().Must(BeAValidCode).WithMessage(CodeRule);
        RuleFor(request => request.DestinationWarehouseCode).NotEmpty().Must(BeAValidCode).WithMessage(CodeRule)
            .Must((request, destination) => !IsSameCode(destination, request.SourceWarehouseCode))
            .WithMessage("'{PropertyName}' must differ from the source warehouse code: a self-transfer is not allowed.");
        RuleFor(request => request.Quantity).GreaterThan(0);
    }

    // The handler trims codes before the store sees them, so the rule applies to the trimmed value.
    private static bool BeAValidCode(string? code) => CodePattern().IsMatch(code!.Trim());

    // Ordinal, like the database's unique code: "wh-a" and "WH-A" are different warehouses.
    private static bool IsSameCode(string? destination, string? source) =>
        string.Equals(destination?.Trim(), source?.Trim(), StringComparison.Ordinal);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,49}$")]
    private static partial Regex CodePattern();
}
