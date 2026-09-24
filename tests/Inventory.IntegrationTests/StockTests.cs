using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class StockTests(ApiFactory api)
{
    [Fact]
    public async Task ReceivingFiveThenFiveLeavesALevelOfTen()
    {
        (string product, string warehouse) = await ProductInWarehouseAsync();
        using HttpClient client = api.CreateClient();
        await ReceiveAsync(client, product, warehouse, 5);

        HttpResponseMessage response = await ReceiveAsync(client, product, warehouse, 5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((product, warehouse, 10), Level(body));
        Assert.Equal(10, await Seed.QuantityAsync(api, product, warehouse));
    }

    [Fact]
    public async Task ReceivingTrimsBothCodesBeforeStoringTheLevel()
    {
        (string product, string warehouse) = await ProductInWarehouseAsync();
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await ReceiveAsync(client, "  " + product + "  ", "\t" + warehouse + " ", 5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((product, warehouse, 5), Level(await response.Content.ReadFromJsonAsync<JsonElement>()));
        Assert.Equal(5, await Seed.QuantityAsync(api, product, warehouse));
    }

    [Fact]
    public async Task QueryTrimsBothFiltersBeforeLookingUpTheLevel()
    {
        (string product, string warehouse) = await ProductInWarehouseAsync();
        await Seed.StockAsync(api, product, warehouse, 4);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode=%20{product}%20&warehouseCode=%09{warehouse}%20");

        Assert.Equal([(product, warehouse, 4)], await LevelsAsync(response));
    }

    [Fact]
    public async Task QueryByProductReturnsOnlyItsRowsInEveryWarehouseOrderedByWarehouseCode()
    {
        string product = await ProductAsync();
        string other = await ProductAsync();
        string prefix = TestData.Unique("WH");
        // Created in reverse code order, so the id order cannot pass for the code order.
        string second = await WarehouseAsync(prefix + "-2");
        string first = await WarehouseAsync(prefix + "-1");
        await Seed.StockAsync(api, product, second, 7);
        await Seed.StockAsync(api, product, first, 3);
        await Seed.StockAsync(api, other, first, 5);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode={product}");

        Assert.Equal([(product, first, 3), (product, second, 7)], await LevelsAsync(response));
    }

    [Fact]
    public async Task QueryByWarehouseReturnsOnlyThatWarehousesRowsOrderedByProductCode()
    {
        string prefix = TestData.Unique("SKU");
        string second = await ProductAsync(prefix + "-2");
        string first = await ProductAsync(prefix + "-1");
        string warehouse = await WarehouseAsync();
        string elsewhere = await WarehouseAsync();
        await Seed.StockAsync(api, second, warehouse, 4);
        await Seed.StockAsync(api, first, warehouse, 2);
        await Seed.StockAsync(api, first, elsewhere, 9);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?warehouseCode={warehouse}");

        Assert.Equal([(first, warehouse, 2), (second, warehouse, 4)], await LevelsAsync(response));
    }

    [Fact]
    public async Task BothFiltersReturnOnlyTheIntersectionRow()
    {
        string product = await ProductAsync();
        string other = await ProductAsync();
        string warehouse = await WarehouseAsync();
        string elsewhere = await WarehouseAsync();
        await Seed.StockAsync(api, product, warehouse, 6);
        await Seed.StockAsync(api, product, elsewhere, 8);
        await Seed.StockAsync(api, other, warehouse, 1);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode={product}&warehouseCode={warehouse}");

        Assert.Equal([(product, warehouse, 6)], await LevelsAsync(response));
    }

    [Fact]
    public async Task QueryRowsCarryTheirUpdateTimeInUtc()
    {
        (string product, string warehouse) = await ProductInWarehouseAsync();
        await Seed.StockAsync(api, product, warehouse, 1);
        using HttpClient client = api.CreateClient();

        JsonElement[] rows = await client.GetFromJsonAsync<JsonElement[]>($"/stock?productCode={product}") ?? [];

        string updatedAt = Assert.Single(rows).GetProperty("updatedAt").GetString() ?? "";
        Assert.EndsWith("Z", updatedAt);
        Assert.InRange(DateTime.Parse(updatedAt).ToUniversalTime(), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
    }

    [Fact]
    public async Task ReceivingAnUnknownProductIs400UnknownProductCode()
    {
        string warehouse = await WarehouseAsync();
        string product = TestData.Unique("SKU");
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await ReceiveAsync(client, product, warehouse, 5);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("unknown_product_code", problem.Code);
        Assert.Contains(product, problem.Detail);
    }

    [Fact]
    public async Task ReceivingIntoAnUnknownWarehouseIs400UnknownWarehouseCode()
    {
        string product = await ProductAsync();
        string warehouse = TestData.Unique("WH");
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await ReceiveAsync(client, product, warehouse, 5);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("unknown_warehouse_code", problem.Code);
        Assert.Contains(warehouse, problem.Detail);
    }

    [Fact]
    public async Task QueryingAnUnknownProductIs404ProductNotFound()
    {
        string product = TestData.Unique("SKU");
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode={product}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("product_not_found", (await Problem.ReadAsync(response)).Code);
    }

    [Fact]
    public async Task QueryingAnUnknownWarehouseIs404WarehouseNotFound()
    {
        string warehouse = TestData.Unique("WH");
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/stock?warehouseCode={warehouse}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("warehouse_not_found", (await Problem.ReadAsync(response)).Code);
    }

    [Fact]
    public async Task ReceiptThatOverflowsTheLevelIs400QuantityOutOfRangeAndLeavesTheLevelUnchanged()
    {
        (string product, string warehouse) = await ProductInWarehouseAsync();
        await Seed.StockAsync(api, product, warehouse, int.MaxValue - 1);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await ReceiveAsync(client, product, warehouse, 5);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("quantity_out_of_range", (await Problem.ReadAsync(response)).Code);
        Assert.Equal(int.MaxValue - 1, await Seed.QuantityAsync(api, product, warehouse));
    }

    private static Task<HttpResponseMessage> ReceiveAsync(HttpClient client, string productCode, string warehouseCode, int quantity) =>
        client.PostAsJsonAsync("/stock", new { productCode, warehouseCode, quantity });

    private static async Task<(string, string, int)[]> LevelsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] rows = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        return rows.Select(Level).ToArray();
    }

    private static (string, string, int) Level(JsonElement row) => (
        row.GetProperty("productCode").GetString() ?? "",
        row.GetProperty("warehouseCode").GetString() ?? "",
        row.GetProperty("quantity").GetInt32());

    private async Task<(string Product, string Warehouse)> ProductInWarehouseAsync() =>
        (await ProductAsync(), await WarehouseAsync());

    private async Task<string> ProductAsync(string? code = null)
    {
        string product = code ?? TestData.Unique("SKU");
        await Seed.ProductAsync(api, product);
        return product;
    }

    private async Task<string> WarehouseAsync(string? code = null)
    {
        string warehouse = code ?? TestData.Unique("WH");
        await Seed.WarehouseAsync(api, warehouse);
        return warehouse;
    }
}
