using FluentValidation;
using Inventory.Api.Shared.Validation;

namespace Inventory.Api.Features.Stock;

public sealed class ReceiveStockValidator : AbstractValidator<ReceiveStockRequest>
{
    public ReceiveStockValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.ProductCode).MustBeACode();
        RuleFor(request => request.WarehouseCode).MustBeACode();
        RuleFor(request => request.Quantity).GreaterThan(0);
    }
}
