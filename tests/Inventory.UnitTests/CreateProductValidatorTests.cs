using FluentValidation.Results;
using Inventory.Api.Features.Products;

namespace Inventory.UnitTests;

public sealed class CreateProductValidatorTests
{
    private readonly CreateProductValidator validator = new();

    [Fact]
    public void AValidProductHasNoErrors() =>
        Assert.Empty(validator.Validate(new CreateProductRequest("SKU-1", "Blue widget")).Errors);

    [Fact]
    public void APaddedCodeIsAcceptedBecauseItIsCheckedAfterTrimming() =>
        Assert.Empty(validator.Validate(new CreateProductRequest("  SKU-1\t", "Blue widget")).Errors);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingOrBlankCodeIsOneNotEmptyErrorOnCode(string? code)
    {
        ValidationFailure failure = AssertSingleError(new CreateProductRequest(code, "Blue widget"), nameof(CreateProductRequest.Code));

        // Reported as missing, not as a pattern mismatch.
        Assert.Contains("must not be empty", failure.ErrorMessage, StringComparison.Ordinal);
    }

    // These two theories pin the shared code rule (CodeRuleExtensions) every validator uses:
    // 1-50 characters from letters, digits, '.', '_' and '-', starting with a letter or digit.
    [Theory]
    [InlineData("SKU 1")]
    [InlineData(".SKU")]
    [InlineData("_SKU")]
    [InlineData("-SKU")]
    [InlineData("S23456789012345678901234567890123456789012345678901")]
    public void ACodeOutsideThePatternIsRejectedOnCode(string code) =>
        AssertSingleError(new CreateProductRequest(code, "Blue widget"), nameof(CreateProductRequest.Code));

    [Theory]
    [InlineData("A")]
    [InlineData("9")]
    [InlineData("SKU_1.v2")]
    [InlineData("a.b-c_d")]
    [InlineData("SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS")]
    public void ACodeInsideThePatternIsAccepted(string code) =>
        Assert.Empty(validator.Validate(new CreateProductRequest(code, "Blue widget")).Errors);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AMissingOrBlankDescriptionIsOneErrorOnDescription(string? description) =>
        AssertSingleError(new CreateProductRequest("SKU-1", description), nameof(CreateProductRequest.Description));

    [Fact]
    public void ADescriptionOf200CharactersIsAccepted() =>
        Assert.Empty(validator.Validate(new CreateProductRequest("SKU-1", new string('d', 200))).Errors);

    [Fact]
    public void ADescriptionOver200CharactersIsRejectedOnDescription() =>
        AssertSingleError(new CreateProductRequest("SKU-1", new string('d', 201)), nameof(CreateProductRequest.Description));

    private ValidationFailure AssertSingleError(CreateProductRequest request, string property)
    {
        ValidationResult result = validator.Validate(request);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
        return failure;
    }
}
