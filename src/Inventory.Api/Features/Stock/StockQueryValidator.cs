using FluentValidation;
using Inventory.Api.Shared.Validation;

namespace Inventory.Api.Features.Stock;

public sealed class StockQueryValidator : AbstractValidator<StockQuery>
{
    public StockQueryValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // Keyed on productCode so every errors key is a JSON name; the message names both filters.
        RuleFor(query => query.ProductCode)
            .Must((query, productCode) => productCode is not null || query.WarehouseCode is not null)
            .WithMessage("Give productCode, warehouseCode or both: a stock query needs at least one filter.");
        RuleFor(query => query.ProductCode).MustBeACode().When(query => query.ProductCode is not null);
        RuleFor(query => query.WarehouseCode).MustBeACode().When(query => query.WarehouseCode is not null);
    }
}
