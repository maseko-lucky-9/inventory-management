using Inventory.Api.Features.Orders;
using Inventory.Api.Shared.Errors;

namespace Inventory.UnitTests;

public sealed class TransferServiceTests
{
    private static readonly TransferCommand Command = new("SKU-1", "WH-A", "WH-B", 7);

    [Fact]
    public async Task TransferSucceedsAndReturnsTheOrderWhenTheStoreReportsSuccess()
    {
        TransferOrder order = new(42, "SKU-1", "WH-A", "WH-B", 7, new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));
        TransferService service = new(new FakeTransferStore(new TransferOutcome.Completed(order)));

        TransferOrder result = await service.TransferAsync(Command, CancellationToken.None);

        Assert.Equal(order, result);
    }

    [Fact]
    public async Task TransferRefusalNamesProductSourceRequestedAndAvailable()
    {
        TransferService service = new(new FakeTransferStore(new TransferOutcome.Insufficient(3)));

        InsufficientStockException error = await Assert.ThrowsAsync<InsufficientStockException>(
            () => service.TransferAsync(Command, CancellationToken.None));

        Assert.Equal((400, "insufficient_stock"), (error.Status, error.Code));
        Assert.Equal("Insufficient stock of SKU-1 in WH-A: requested 7, available 3.", error.Message);
    }

    [Theory]
    [InlineData("product", "SKU-X", "unknown_product_code")]
    [InlineData("warehouse", "WH-X", "unknown_warehouse_code")]
    public async Task UnknownCodeBecomesAnUnknownCodeErrorNamingTheCode(string entity, string code, string expectedCode)
    {
        TransferService service = new(new FakeTransferStore(new TransferOutcome.UnknownCode(entity, code)));

        UnknownCodeException error = await Assert.ThrowsAsync<UnknownCodeException>(
            () => service.TransferAsync(Command, CancellationToken.None));

        Assert.Equal((400, expectedCode), (error.Status, error.Code));
        Assert.Contains(code, error.Message);
    }

    // Hand-written fake: the store's only job here is to report an outcome.
    private sealed class FakeTransferStore(TransferOutcome outcome) : ITransferStore
    {
        public Task<TransferOutcome> ExecuteAsync(TransferCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(outcome);
    }
}
