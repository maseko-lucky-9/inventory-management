using Inventory.Api.Shared.Errors;

namespace Inventory.UnitTests;

public sealed class DbErrorTranslatorTests
{
    [Theory]
    [InlineData("23505", typeof(DuplicateCodeException))]
    [InlineData("22003", typeof(QuantityOutOfRangeException))]
    [InlineData("40P01", typeof(ConcurrencyConflictException))]
    [InlineData("40001", typeof(ConcurrencyConflictException))]
    [InlineData("55P03", typeof(ConcurrencyConflictException))]
    [InlineData("57014", typeof(DatabaseUnavailableException))]
    [InlineData("53300", typeof(DatabaseUnavailableException))]
    [InlineData("57P01", typeof(DatabaseUnavailableException))]
    [InlineData("08006", typeof(DatabaseUnavailableException))]
    [InlineData(null, typeof(DatabaseUnavailableException))]
    public void ExpectedDatabaseFailuresBecomeDomainErrors(string? sqlState, Type expected) =>
        Assert.IsType(expected, DbErrorTranslator.Translate(sqlState, "product", "SKU-1"));

    [Fact]
    public void CheckViolationIsADefectNotARefusal() =>
        Assert.Null(DbErrorTranslator.Translate("23514", "product", "SKU-1"));

    [Fact]
    public void DuplicateNamesTheEntityAndTheCode()
    {
        DomainException error = DbErrorTranslator.Translate("23505", "product", "SKU-1")!;

        Assert.Equal((409, "duplicate_product_code"), (error.Status, error.Code));
        Assert.Contains("SKU-1", error.Message);
    }
}
