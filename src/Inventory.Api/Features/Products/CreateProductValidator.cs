using FluentValidation;
using Inventory.Api.Shared.Validation;

namespace Inventory.Api.Features.Products;

public sealed class CreateProductValidator : AbstractValidator<CreateProductRequest>
{
    // The spec sets no cap; 200 keeps stored text bounded and a description to one line in the UI list.
    private const int MaxDescriptionLength = 200;

    public CreateProductValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(request => request.Code).MustBeACode();
        RuleFor(request => request.Description).NotEmpty().MaximumLength(MaxDescriptionLength);
    }
}
