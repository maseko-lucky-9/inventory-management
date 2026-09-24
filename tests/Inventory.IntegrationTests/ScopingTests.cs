using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Inventory.IntegrationTests;

/// <summary>ADR-006 and ADR-008 scoping (US4, FR-013, FR-014): a caller sees and acts only on the warehouses linked to them.</summary>
/// <remarks>Every caller here is a fresh user, so the demo users' links never change. Seed.WarehouseAsync links only the fixture user.</remarks>
[Collection("api")]
public sealed class ScopingTests(ApiFactory api)
{
    [Fact]
    public async Task TheWarehouseListShowsOnlyTheWarehousesLinkedToTheCaller()
    {
        (string user, string linked, string _) = await UserLinkedToOneOfTwoWarehousesAsync();
        using HttpClient client = api.CreateClientFor(user);

        HttpResponseMessage response = await client.GetAsync("/warehouses");

        Assert.Equal([linked], await CodesAsync(response));
    }

    [Fact]
    public async Task AStockQueryByProductReturnsOnlyTheRowsInLinkedWarehouses()
    {
        (string user, string linked, string unlinked) = await UserLinkedToOneOfTwoWarehousesAsync();
        string product = await ProductAsync();
        await Seed.StockAsync(api, product, linked, 3);
        await Seed.StockAsync(api, product, unlinked, 7);
        using HttpClient client = api.CreateClientFor(user);

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode={product}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] rows = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        Assert.Equal([(linked, 3)], rows.Select(row => (row.GetProperty("warehouseCode").GetString(), row.GetProperty("quantity").GetInt32())));
    }

    [Fact]
    public async Task AStockQueryOnAnUnlinkedWarehouseAnswersExactlyAsOnAnUnknownCode()
    {
        string product = await ProductAsync();
        string code = TestData.Unique("WH");
        using HttpClient client = api.CreateClientFor(TestData.Unique("user"));

        (HttpStatusCode status, Problem problem) = await UnknownAndUnlinkedAnswerAlikeAsync(
            () => client.GetAsync($"/stock?warehouseCode={code}"),
            async () =>
            {
                await Seed.WarehouseAsync(api, code);
                await Seed.StockAsync(api, product, code, 5);
            });

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal("warehouse_not_found", problem.Code);
    }

    [Fact]
    public async Task ReceivingIntoAnUnlinkedWarehouseAnswersExactlyAsAnUnknownCodeAndStoresNothing()
    {
        string product = await ProductAsync();
        string code = TestData.Unique("WH");
        using HttpClient client = api.CreateClientFor(TestData.Unique("user"));

        (HttpStatusCode status, Problem problem) = await UnknownAndUnlinkedAnswerAlikeAsync(
            () => client.PostAsJsonAsync("/stock", new { productCode = product, warehouseCode = code, quantity = 5 }),
            () => Seed.WarehouseAsync(api, code));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("unknown_warehouse_code", problem.Code);
        Assert.Contains(code, problem.Detail, StringComparison.Ordinal);
        Assert.Null(await Seed.QuantityAsync(api, product, code));
    }

    [Fact]
    public async Task ATransferOutOfAnUnlinkedWarehouseAnswersExactlyAsAnUnknownSourceAndMovesNothing()
    {
        (string user, string linked, string _) = await UserLinkedToOneOfTwoWarehousesAsync();
        string product = await ProductAsync();
        string source = TestData.Unique("WH");
        using HttpClient client = api.CreateClientFor(user);

        (HttpStatusCode status, Problem problem) = await UnknownAndUnlinkedAnswerAlikeAsync(
            () => client.PostAsJsonAsync("/orders", Transfer(product, source, linked, 4)),
            async () =>
            {
                await Seed.WarehouseAsync(api, source);
                await Seed.StockAsync(api, product, source, 10);
            });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal("unknown_warehouse_code", problem.Code);
        Assert.Contains(source, problem.Detail, StringComparison.Ordinal);
        Assert.Equal(10, await Seed.QuantityAsync(api, product, source));
        Assert.Null(await Seed.QuantityAsync(api, product, linked));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    // G36: a user linked to one warehouse must still be able to send stock somewhere.
    [Fact]
    public async Task ATransferFromALinkedWarehouseIntoAnUnlinkedOneCompletes()
    {
        (string user, string linked, string unlinked) = await UserLinkedToOneOfTwoWarehousesAsync();
        string product = await ProductAsync();
        await Seed.StockAsync(api, product, linked, 10);
        using HttpClient client = api.CreateClientFor(user);

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Transfer(product, linked, unlinked, 4));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(6, await Seed.QuantityAsync(api, product, linked));
        Assert.Equal(4, await Seed.QuantityAsync(api, product, unlinked));
    }

    [Fact]
    public async Task ATransferOrderRecordsTheCallerAsItsCreator()
    {
        (string user, string linked, string unlinked) = await UserLinkedToOneOfTwoWarehousesAsync();
        string product = await ProductAsync();
        await Seed.StockAsync(api, product, linked, 5);
        using HttpClient client = api.CreateClientFor(user);

        HttpResponseMessage response = await client.PostAsJsonAsync("/orders", Transfer(product, linked, unlinked, 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        long orderId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(await Seed.UserIdAsync(api, user), await Seed.OrderCreatorAsync(api, orderId));
    }

    // US4 scenario 6: products are global, so a user with no links still creates them.
    [Fact]
    public async Task AUserWithNoLinksSeesNoWarehousesGets404ForAStockQueryOnWhAAndCanStillCreateAProduct()
    {
        using HttpClient client = api.CreateClientFor(TestData.Unique("user"));

        HttpResponseMessage warehouses = await client.GetAsync("/warehouses");
        HttpResponseMessage stock = await client.GetAsync("/stock?warehouseCode=WH-A");
        HttpResponseMessage product = await client.PostAsJsonAsync("/products", new { code = TestData.Unique("SKU"), description = "Global" });

        Assert.Empty(await CodesAsync(warehouses));
        Assert.Equal(HttpStatusCode.NotFound, stock.StatusCode);
        Assert.Equal("warehouse_not_found", (await Problem.ReadAsync(stock)).Code);
        Assert.Equal(HttpStatusCode.Created, product.StatusCode);
    }

    [Fact]
    public async Task AWarehouseAUserCreatesIsTheOnlyOneInTheirList()
    {
        string code = TestData.Unique("WH");
        using HttpClient client = api.CreateClientFor(TestData.Unique("user"));

        HttpResponseMessage created = await client.PostAsJsonAsync("/warehouses", new { code, name = "Own depot" });
        HttpResponseMessage listed = await client.GetAsync("/warehouses");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal([code], await CodesAsync(listed));
    }

    // Creating a code that already exists must not become a way to link yourself to someone else's warehouse.
    [Fact]
    public async Task CreatingAnExistingWarehouseCodeAnswers409AndLinksNothing()
    {
        string code = TestData.Unique("WH");
        await Seed.WarehouseAsync(api, code);
        using HttpClient client = api.CreateClientFor(TestData.Unique("user"));

        HttpResponseMessage created = await client.PostAsJsonAsync("/warehouses", new { code, name = "Taken" });
        HttpResponseMessage listed = await client.GetAsync("/warehouses");

        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        Assert.Equal("duplicate_warehouse_code", (await Problem.ReadAsync(created)).Code);
        Assert.Empty(await CodesAsync(listed));
    }

    // FR-014, byte for byte: the same code is asked about before it exists and again after another user creates it.
    // Both answers must match exactly, except the per-request trace id.
    private static async Task<(HttpStatusCode Status, Problem Problem)> UnknownAndUnlinkedAnswerAlikeAsync(
        Func<Task<HttpResponseMessage>> request, Func<Task> createUnlinked)
    {
        HttpResponseMessage unknown = await request();
        await createUnlinked();
        HttpResponseMessage unlinked = await request();

        Assert.Equal(unknown.StatusCode, unlinked.StatusCode);
        Problem before = await Problem.ReadAsync(unknown);
        Problem after = await Problem.ReadAsync(unlinked);
        Assert.Equal(WithoutTraceId(before.Body), WithoutTraceId(after.Body));
        return (unlinked.StatusCode, after);
    }

    // A fresh user linked to the first of two new warehouses; the fixture user is linked to both.
    private async Task<(string User, string Linked, string Unlinked)> UserLinkedToOneOfTwoWarehousesAsync()
    {
        string user = TestData.Unique("user");
        string linked = TestData.Unique("WH");
        string unlinked = TestData.Unique("WH");
        await Seed.WarehouseAsync(api, linked);
        await Seed.WarehouseAsync(api, unlinked);
        await Seed.LinkAsync(api, user, linked);
        return (user, linked, unlinked);
    }

    private async Task<string> ProductAsync()
    {
        string product = TestData.Unique("SKU");
        await Seed.ProductAsync(api, product);
        return product;
    }

    private static async Task<string[]> CodesAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] warehouses = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        return [.. warehouses.Select(warehouse => warehouse.GetProperty("code").GetString() ?? "")];
    }

    private static string WithoutTraceId(JsonElement body)
    {
        JsonObject copy = JsonNode.Parse(body.GetRawText())?.AsObject() ?? [];
        copy.Remove("traceId");
        return copy.ToJsonString();
    }

    private static object Transfer(string product, string source, string destination, int quantity) => new
    {
        productCode = product,
        sourceWarehouseCode = source,
        destinationWarehouseCode = destination,
        quantity,
    };
}
