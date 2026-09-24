using FluentValidation.Results;
using Inventory.Api.Features.Stock;

namespace Inventory.UnitTests;

public sealed class ReceiveStockValidatorTests
{
    private readonly ReceiveStockValidator validator = new();

    [Fact]
    public void AValidReceiptHasNoErrors() =>
        Assert.Empty(validator.Validate(new ReceiveStockRequest("SKU-1", "WH-A", 5)).Errors);

    [Fact]
    public void PaddedCodesAreAcceptedBecauseTheyAreCheckedAfterTrimming() =>
        Assert.Empty(validator.Validate(new ReceiveStockRequest(" SKU-1 ", "\tWH-A", 5)).Errors);

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("SKU 1")]
    [InlineData(".SKU")]
    [InlineData("S23456789012345678901234567890123456789012345678901")]
    public void ABadProductCodeIsOneErrorOnProductCode(string? productCode) =>
        AssertSingleError(new ReceiveStockRequest(productCode, "WH-A", 5), nameof(ReceiveStockRequest.ProductCode));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("WH A")]
    [InlineData(".WH")]
    [InlineData("W23456789012345678901234567890123456789012345678901")]
    public void ABadWarehouseCodeIsOneErrorOnWarehouseCode(string? warehouseCode) =>
        AssertSingleError(new ReceiveStockRequest("SKU-1", warehouseCode, 5), nameof(ReceiveStockRequest.WarehouseCode));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AQuantityOfZeroOrLessIsRejectedOnQuantity(int quantity) =>
        AssertSingleError(new ReceiveStockRequest("SKU-1", "WH-A", quantity), nameof(ReceiveStockRequest.Quantity));

    [Fact]
    public void AQuantityOfOneIsAccepted() =>
        Assert.Empty(validator.Validate(new ReceiveStockRequest("SKU-1", "WH-A", 1)).Errors);

    private void AssertSingleError(ReceiveStockRequest request, string property)
    {
        ValidationResult result = validator.Validate(request);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
    }
}
