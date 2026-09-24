using FluentValidation.Results;
using Inventory.Api.Features.Warehouses;

namespace Inventory.UnitTests;

public sealed class CreateWarehouseValidatorTests
{
    private readonly CreateWarehouseValidator validator = new();

    [Fact]
    public void AValidWarehouseHasNoErrors() =>
        Assert.Empty(validator.Validate(new CreateWarehouseRequest("WH-1", "North depot")).Errors);

    [Fact]
    public void APaddedCodeIsAcceptedBecauseItIsCheckedAfterTrimming() =>
        Assert.Empty(validator.Validate(new CreateWarehouseRequest(" WH-1  ", "North depot")).Errors);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AMissingOrBlankCodeIsOneNotEmptyErrorOnCode(string? code)
    {
        ValidationFailure failure = AssertSingleError(new CreateWarehouseRequest(code, "North depot"), nameof(CreateWarehouseRequest.Code));

        // Reported as missing, not as a pattern mismatch.
        Assert.Contains("must not be empty", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("WH 1")]
    [InlineData(".WH")]
    [InlineData("W23456789012345678901234567890123456789012345678901")]
    public void ACodeOutsideThePatternIsRejectedOnCode(string code) =>
        AssertSingleError(new CreateWarehouseRequest(code, "North depot"), nameof(CreateWarehouseRequest.Code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void AMissingOrBlankNameIsOneErrorOnName(string? name) =>
        AssertSingleError(new CreateWarehouseRequest("WH-1", name), nameof(CreateWarehouseRequest.Name));

    [Fact]
    public void ANameOf200CharactersIsAccepted() =>
        Assert.Empty(validator.Validate(new CreateWarehouseRequest("WH-1", new string('n', 200))).Errors);

    [Fact]
    public void ANameOver200CharactersIsRejectedOnName() =>
        AssertSingleError(new CreateWarehouseRequest("WH-1", new string('n', 201)), nameof(CreateWarehouseRequest.Name));

    private ValidationFailure AssertSingleError(CreateWarehouseRequest request, string property)
    {
        ValidationResult result = validator.Validate(request);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
        return failure;
    }
}
