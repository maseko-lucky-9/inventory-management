using System.Net;
using System.Net.Http.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class TransferLockOrderTests(ApiFactory api)
{
    [Fact]
    public async Task OpposingConcurrentTransfersAllCompleteWithNoDeadlockAndConserveStock()
    {
        string product = await ProductAsync();
        string lower = TestData.Unique("WH");
        string higher = TestData.Unique("WH");
        await Seed.WarehouseAsync(api, lower);
        await Seed.WarehouseAsync(api, higher);
        await Seed.StockAsync(api, product, lower, 1000);
        await Seed.StockAsync(api, product, higher, 1000);
        using HttpClient client = api.CreateClient();
        // Created in this order, so lower has the lower id. With source-first locking, each direction would hold one row and wait for the other.
        IEnumerable<Task<HttpResponseMessage>> transfers = Enumerable.Range(0, 50).Select(index => index % 2 == 0
            ? client.PostAsJsonAsync("/orders", Body(product, lower, higher, 1))
            : client.PostAsJsonAsync("/orders", Body(product, higher, lower, 1)));

        HttpResponseMessage[] responses = await Task.WhenAll(transfers);

        string[] failures = await Task.WhenAll(responses
            .Where(response => response.StatusCode != HttpStatusCode.Created)
            .Select(DescribeAsync));
        Assert.Empty(failures.CountBy(failure => failure).Select(group => group.Key + " x" + group.Value));
        int? lowerLevel = await Seed.QuantityAsync(api, product, lower);
        int? higherLevel = await Seed.QuantityAsync(api, product, higher);
        Assert.Equal(2000, lowerLevel + higherLevel);
        Assert.Equal(50, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task RefusedTransferUndoesTheDestinationCreditThatRanFirst()
    {
        string product = await ProductAsync();
        string source = TestData.Unique("WH");
        string destination = TestData.Unique("WH");
        // Destination created first, so it has the lower id and is credited before the source guard refuses.
        long destinationId = await Seed.WarehouseAsync(api, destination);
        long sourceId = await Seed.WarehouseAsync(api, source);
        Assert.True(destinationId < sourceId);
        await Seed.StockAsync(api, product, source, 5);
        await Seed.StockAsync(api, product, destination, 2);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 7));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("insufficient_stock", problem.Code);
        Assert.Contains("available 5", problem.Detail);
        Assert.Equal(2, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(5, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    // Status plus code, so a red run names the refusal (e.g. "503 concurrency_conflict x12").
    private static async Task<string> DescribeAsync(HttpResponseMessage response) =>
        (int)response.StatusCode + " " + (await Problem.ReadAsync(response)).Code;

    private async Task<string> ProductAsync()
    {
        string product = TestData.Unique("SKU");
        await Seed.ProductAsync(api, product);
        return product;
    }

    private static object Body(string product, string source, string destination, int quantity) => new
    {
        productCode = product,
        sourceWarehouseCode = source,
        destinationWarehouseCode = destination,
        quantity,
    };
}
