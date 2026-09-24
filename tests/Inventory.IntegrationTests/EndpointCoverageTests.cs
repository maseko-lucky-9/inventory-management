using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>Fills the gaps the per-slice suites leave: trimming before uniqueness, unknown body fields, and an outage on every endpoint.</summary>
[Collection("api")]
public sealed class EndpointCoverageTests(ApiFactory api)
{
    // Each pad goes on both ends of its post: padded then bare, bare then padded, padded two ways.
    [Theory]
    [InlineData("  ", "")]
    [InlineData("", " ")]
    [InlineData(" ", "   ")]
    public async Task ProductCodeIsStoredTrimmedSoAnyPaddingOfItIsADuplicate(string firstPad, string secondPad)
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");
        HttpResponseMessage first = await client.PostAsJsonAsync("/products", new { code = firstPad + code + firstPad, description = "First" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage response = await client.PostAsJsonAsync("/products", new { code = secondPad + code + secondPad, description = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("duplicate_product_code", problem.Code);
        Assert.Contains(code, problem.Detail, StringComparison.Ordinal);
        Assert.Equal([code], await StoredProductCodesAsync(code));
    }

    [Theory]
    [InlineData("/products", HttpStatusCode.Created)]
    [InlineData("/warehouses", HttpStatusCode.Created)]
    [InlineData("/stock", HttpStatusCode.OK)]
    [InlineData("/orders", HttpStatusCode.Created)]
    public async Task UnknownBodyFieldsAreIgnoredAndNeverEchoed(string path, HttpStatusCode expected)
    {
        Dictionary<string, object> body = await ValidBodyAsync(path);
        body["colour"] = "red";
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(path, body);

        Assert.Equal(expected, response.StatusCode);
        JsonElement echoed = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(echoed.TryGetProperty("colour", out _));
    }

    [Fact]
    public async Task HealthAnswers503WhenTheDatabaseIsUnreachable()
    {
        await using WebApplicationFactory<Program> offline = api.WithUnreachableDatabase();
        using HttpClient client = offline.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        // The health probe writes a plain-text status, not Problem Details, so only the status is asserted.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // Valid requests only, so each one gets past binding and validation and reaches its store.
    [Theory]
    [InlineData("GET", "/products", null)]
    [InlineData("GET", "/products/SKU-OFFLINE", null)]
    [InlineData("POST", "/products", """{"code":"SKU-OFFLINE","description":"Offline"}""")]
    [InlineData("GET", "/warehouses", null)]
    [InlineData("POST", "/warehouses", """{"code":"WH-OFFLINE","name":"Offline"}""")]
    [InlineData("POST", "/stock", """{"productCode":"SKU-OFFLINE","warehouseCode":"WH-OFFLINE","quantity":5}""")]
    [InlineData("GET", "/stock?productCode=SKU-OFFLINE", null)]
    [InlineData("POST", "/orders", """{"productCode":"SKU-OFFLINE","sourceWarehouseCode":"WH-A","destinationWarehouseCode":"WH-B","quantity":1}""")]
    [InlineData("POST", "/auth/login", """{"username":"alice","password":"offline"}""")]
    public async Task EveryEndpointAnswers503DatabaseUnavailableWhenTheDatabaseIsUnreachable(string method, string path, string? json)
    {
        await using WebApplicationFactory<Program> offline = api.WithUnreachableDatabase();
        using HttpClient client = offline.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json"),
        };

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("database_unavailable", problem.Code);
    }

    // btrim matches the code however it was padded, so an untrimmed stored copy would show up here.
    private async Task<string[]> StoredProductCodesAsync(string code)
    {
        await using NpgsqlConnection connection = new(api.ConnectionString);
        IEnumerable<string> codes = await connection.QueryAsync<string>(
            "SELECT code FROM products WHERE btrim(code) = @Code", new { Code = code });
        return [.. codes];
    }

    // Seeds a product with stock in a source warehouse, so the body for every POST succeeds.
    private async Task<Dictionary<string, object>> ValidBodyAsync(string path)
    {
        string product = TestData.Unique("SKU");
        string source = TestData.Unique("WH");
        string destination = TestData.Unique("WH");
        await Seed.ProductAsync(api, product);
        await Seed.WarehouseAsync(api, source);
        await Seed.WarehouseAsync(api, destination);
        await Seed.StockAsync(api, product, source, 10);
        return path switch
        {
            "/products" => new() { ["code"] = TestData.Unique("SKU"), ["description"] = "Probe" },
            "/warehouses" => new() { ["code"] = TestData.Unique("WH"), ["name"] = "Probe" },
            "/stock" => new() { ["productCode"] = product, ["warehouseCode"] = source, ["quantity"] = 5 },
            "/orders" => new() { ["productCode"] = product, ["sourceWarehouseCode"] = source, ["destinationWarehouseCode"] = destination, ["quantity"] = 3 },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "No valid body is defined for this path."),
        };
    }
}
