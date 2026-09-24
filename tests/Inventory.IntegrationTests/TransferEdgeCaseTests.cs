using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class TransferEdgeCaseTests(ApiFactory api)
{
    [Fact]
    public async Task SelfTransferIsRefusedWithValidationFailedAndChangesNothing()
    {
        string product = await ProductAsync();
        string warehouse = await WarehouseAsync();
        await Seed.StockAsync(api, product, warehouse, 10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, warehouse, warehouse, 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("validation_failed", problem.Code);
        JsonProperty error = Assert.Single(problem.Body.GetProperty("errors").EnumerateObject());
        Assert.Equal("destinationWarehouseCode", error.Name);
        Assert.Equal(10, await Seed.QuantityAsync(api, product, warehouse));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task UnknownProductIsRefusedNamingTheCodeAndTouchesNoOtherStock()
    {
        string product = TestData.Unique("SKU");
        string source = await WarehouseAsync();
        string destination = await WarehouseAsync();
        // A real product with stock at the source: a resolve that quietly picked another id would move it.
        string other = await ProductAsync();
        await Seed.StockAsync(api, other, source, 10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("unknown_product_code", problem.Code);
        Assert.Contains(product, problem.Detail);
        Assert.Equal(10, await Seed.QuantityAsync(api, other, source));
    }

    [Fact]
    public async Task UnknownSourceWarehouseIsRefusedNamingTheCode()
    {
        string product = await ProductAsync();
        string source = TestData.Unique("WH");
        string destination = await WarehouseAsync();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("unknown_warehouse_code", problem.Code);
        Assert.Contains(source, problem.Detail);
    }

    [Fact]
    public async Task UnknownDestinationWarehouseIsRefusedNamingTheCodeAndChangesNothing()
    {
        string product = await ProductAsync();
        string source = await WarehouseAsync();
        string destination = TestData.Unique("WH");
        await Seed.StockAsync(api, product, source, 10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("unknown_warehouse_code", problem.Code);
        Assert.Contains(destination, problem.Detail);
        Assert.Equal(10, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task SourceWithNoStockRowIsRefusedWithAvailableZero()
    {
        string product = await ProductAsync();
        string source = await WarehouseAsync();
        string destination = await WarehouseAsync();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("insufficient_stock", problem.Code);
        Assert.Contains("available 0", problem.Detail);
    }

    [Fact]
    public async Task AnOverflowingDestinationRollsBackTheSourceDecrement()
    {
        string product = await ProductAsync();
        string source = TestData.Unique("WH");
        string destination = TestData.Unique("WH");
        // Source created first, so it has the lower id and its row is decremented before the destination fails.
        long sourceId = await Seed.WarehouseAsync(api, source);
        long destinationId = await Seed.WarehouseAsync(api, destination);
        Assert.True(sourceId < destinationId);
        await Seed.StockAsync(api, product, source, 10);
        await Seed.StockAsync(api, product, destination, int.MaxValue - 1);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 5));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("quantity_out_of_range", (await Problem.ReadAsync(response)).Code);
        Assert.Equal(10, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(int.MaxValue - 1, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    private async Task<string> ProductAsync()
    {
        string product = TestData.Unique("SKU");
        await Seed.ProductAsync(api, product);
        return product;
    }

    private async Task<string> WarehouseAsync()
    {
        string warehouse = TestData.Unique("WH");
        await Seed.WarehouseAsync(api, warehouse);
        return warehouse;
    }

    private static object Body(string product, string source, string destination, int quantity) => new
    {
        productCode = product,
        sourceWarehouseCode = source,
        destinationWarehouseCode = destination,
        quantity,
    };
}
