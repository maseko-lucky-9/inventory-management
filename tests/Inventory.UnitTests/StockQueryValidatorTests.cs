using FluentValidation.Results;
using Inventory.Api.Features.Stock;

namespace Inventory.UnitTests;

public sealed class StockQueryValidatorTests
{
    private readonly StockQueryValidator validator = new();

    [Theory]
    [InlineData("SKU-1", null)]
    [InlineData(null, "WH-A")]
    [InlineData("SKU-1", "WH-A")]
    [InlineData(" SKU-1 ", "\tWH-A")]
    public void AnyFilterGivenIsAValidQuery(string? productCode, string? warehouseCode) =>
        Assert.Empty(validator.Validate(new StockQuery(productCode, warehouseCode)).Errors);

    [Fact]
    public void AQueryWithNeitherFilterIsRejectedNamingTheRule()
    {
        ValidationResult result = validator.Validate(new StockQuery(null, null));

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(StockQuery.ProductCode), failure.PropertyName);
        Assert.Contains("at least one filter", failure.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SKU 1")]
    [InlineData(".SKU")]
    [InlineData("S23456789012345678901234567890123456789012345678901")]
    public void ABadProductFilterIsOneErrorOnProductCode(string productCode) =>
        AssertSingleError(new StockQuery(productCode, "WH-A"), nameof(StockQuery.ProductCode));

    [Theory]
    [InlineData(" ")]
    [InlineData("WH A")]
    [InlineData(".WH")]
    [InlineData("W23456789012345678901234567890123456789012345678901")]
    public void ABadWarehouseFilterIsOneErrorOnWarehouseCode(string warehouseCode) =>
        AssertSingleError(new StockQuery("SKU-1", warehouseCode), nameof(StockQuery.WarehouseCode));

    private void AssertSingleError(StockQuery query, string property)
    {
        ValidationResult result = validator.Validate(query);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
    }
}
