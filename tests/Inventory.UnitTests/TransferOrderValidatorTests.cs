using FluentValidation.Results;
using Inventory.Api.Features.Orders;

namespace Inventory.UnitTests;

public sealed class TransferOrderValidatorTests
{
    private readonly TransferOrderValidator validator = new();

    [Fact]
    public void AValidRequestHasNoErrors()
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest("SKU-1", "WH-A", "WH-B", 7));

        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("WH-A", "WH-A")]
    [InlineData(" WH-A", "WH-A ")]
    public void SelfTransferIsRejectedByTheValidator(string source, string destination)
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest("SKU-1", source, destination, 7));

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateTransferOrderRequest.DestinationWarehouseCode), failure.PropertyName);
        Assert.Contains("self-transfer", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void QuantityMustBeGreaterThanZero(int quantity)
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest("SKU-1", "WH-A", "WH-B", quantity));

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateTransferOrderRequest.Quantity), failure.PropertyName);
    }

    [Fact]
    public void MissingCodesAreValidationErrorsNotExceptions()
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest(null, null, null, 7));

        Assert.Equal(
            ["DestinationWarehouseCode", "ProductCode", "SourceWarehouseCode"],
            result.Errors.Select(failure => failure.PropertyName).Order());
    }

    [Fact]
    public void CodesAreCheckedAfterTrimming()
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest(" SKU-1 ", "\tWH-A", "WH-B  ", 7));

        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("SKU 1")]
    [InlineData("-SKU")]
    [InlineData("S23456789012345678901234567890123456789012345678901")]
    public void ACodeOutsideThePatternIsRejected(string productCode)
    {
        ValidationResult result = validator.Validate(new CreateTransferOrderRequest(productCode, "WH-A", "WH-B", 7));

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateTransferOrderRequest.ProductCode), failure.PropertyName);
    }
}
