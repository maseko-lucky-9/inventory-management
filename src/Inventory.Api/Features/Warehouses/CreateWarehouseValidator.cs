using FluentValidation;
using Inventory.Api.Shared.Validation;

namespace Inventory.Api.Features.Warehouses;

public sealed class CreateWarehouseValidator : AbstractValidator<CreateWarehouseRequest>
{
    // The spec sets no cap; 200 keeps stored text bounded and a name to one line in the UI.
    private const int MaxNameLength = 200;

    public CreateWarehouseValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Code).MustBeACode();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(MaxNameLength);
    }
}
