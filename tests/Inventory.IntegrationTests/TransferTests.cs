using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class TransferTests(ApiFactory api)
{
    [Fact]
    public async Task TransferMovesStockAndWritesOneOrderAtomically()
    {
        (string product, string source, string destination) = await ArrangeAsync();
        await Seed.StockAsync(api, product, source, 10);
        await Seed.StockAsync(api, product, destination, 0);
        using HttpClient client = api.CreateClient();
        DateTime before = DateTime.UtcNow.AddMinutes(-1);

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 7));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement order = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(order.GetProperty("id").GetInt64() > 0);
        Assert.Equal(product, order.GetProperty("productCode").GetString());
        Assert.Equal(source, order.GetProperty("sourceWarehouseCode").GetString());
        Assert.Equal(destination, order.GetProperty("destinationWarehouseCode").GetString());
        Assert.Equal(7, order.GetProperty("quantity").GetInt32());
        DateTime createdAt = order.GetProperty("createdAt").GetDateTime();
        Assert.Equal(DateTimeKind.Utc, createdAt.Kind);
        Assert.InRange(createdAt, before, DateTime.UtcNow.AddMinutes(1));
        Assert.Equal(3, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(7, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(1, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task InsufficientStockIsRefusedNamingProductSourceRequestedAndAvailableAndChangesNothing()
    {
        (string product, string source, string destination) = await ArrangeAsync();
        await Seed.StockAsync(api, product, source, 3);
        await Seed.StockAsync(api, product, destination, 5);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 7));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("insufficient_stock", problem.Code);
        Assert.Contains(product, problem.Detail);
        Assert.Contains(source, problem.Detail);
        Assert.Contains("requested 7", problem.Detail);
        Assert.Contains("available 3", problem.Detail);
        Assert.Equal(3, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(5, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task TransferIntoAWarehouseWithNoStockRowCreatesTheRow()
    {
        (string product, string source, string destination) = await ArrangeAsync();
        await Seed.StockAsync(api, product, source, 10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(4, await Seed.QuantityAsync(api, product, destination));
    }

    [Fact]
    public async Task InvalidBodyIsRefusedWithValidationFailedKeyedByCamelCaseField()
    {
        (string product, string source, string destination) = await ArrangeAsync();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("validation_failed", problem.Code);
        JsonProperty error = Assert.Single(problem.Body.GetProperty("errors").EnumerateObject());
        Assert.Equal("quantity", error.Name);
    }

    private async Task<(string Product, string Source, string Destination)> ArrangeAsync()
    {
        string product = TestData.Unique("SKU");
        string source = TestData.Unique("WH");
        string destination = TestData.Unique("WH");
        await Seed.ProductAsync(api, product);
        await Seed.WarehouseAsync(api, source);
        await Seed.WarehouseAsync(api, destination);
        return (product, source, destination);
    }

    private static object Body(string product, string source, string destination, int quantity) => new
    {
        productCode = product,
        sourceWarehouseCode = source,
        destinationWarehouseCode = destination,
        quantity,
    };
}
