using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class WarehouseTests(ApiFactory api)
{
    [Fact]
    public async Task CreateAnswers201WithTheWarehouseAndNoLocation()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("WH");

        HttpResponseMessage response = await client.PostAsJsonAsync("/warehouses", new { code, name = "North depot" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal("North depot", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateStoresTheCodeTrimmed()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("WH");

        HttpResponseMessage response = await client.PostAsJsonAsync("/warehouses", new { code = "  " + code + " ", name = "Trimmed" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreatingTheSameCodeTwiceAnswers409DuplicateWarehouseCodeNamingTheCode()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("WH");
        await client.PostAsJsonAsync("/warehouses", new { code, name = "First" });

        HttpResponseMessage response = await client.PostAsJsonAsync("/warehouses", new { code, name = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("duplicate_warehouse_code", problem.Code);
        Assert.Contains(code, problem.Detail, StringComparison.Ordinal);
    }

    // The list is scoped (ScopingTests): the fixture user sees what it created and what it is linked to, never demo WH-A.
    [Fact]
    public async Task ListContainsTheCreatedWarehouseAndALinkedSeededOne()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("WH");
        string seeded = TestData.Unique("WH");
        await Seed.WarehouseAsync(api, seeded);
        await client.PostAsJsonAsync("/warehouses", new { code, name = "Listed" });

        HttpResponseMessage response = await client.GetAsync("/warehouses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] warehouses = await ReadListAsync(response);
        Assert.Contains(warehouses, warehouse => Is(warehouse, code, "Listed"));
        Assert.Contains(warehouses, warehouse => Is(warehouse, seeded, seeded));
    }

    [Fact]
    public async Task ListIsOrderedByCode()
    {
        using HttpClient client = api.CreateClient();
        string prefix = TestData.Unique("WH");
        // Created in reverse, so insertion order alone would put B first.
        await client.PostAsJsonAsync("/warehouses", new { code = prefix + "-B", name = "B" });
        await client.PostAsJsonAsync("/warehouses", new { code = prefix + "-A", name = "A" });

        HttpResponseMessage response = await client.GetAsync("/warehouses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string[] codes = [.. (await ReadListAsync(response)).Select(warehouse => warehouse.GetProperty("code").GetString() ?? "")];
        Assert.True(Array.IndexOf(codes, prefix + "-A") >= 0, "the first warehouse is listed");
        Assert.True(Array.IndexOf(codes, prefix + "-A") < Array.IndexOf(codes, prefix + "-B"), "A comes before B");
    }

    [Fact]
    public async Task TwoParallelIdenticalCreatesYieldExactlyOne201AndOne409()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("WH");

        HttpResponseMessage[] responses = await Task.WhenAll(
            client.PostAsJsonAsync("/warehouses", new { code, name = "Racer 1" }),
            client.PostAsJsonAsync("/warehouses", new { code, name = "Racer 2" }));

        HttpStatusCode[] statuses = [.. responses.Select(response => response.StatusCode).Order()];
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], statuses);
    }

    [Fact]
    public async Task ListAnswers503DatabaseUnavailableWhenTheDatabaseIsUnreachable()
    {
        await using WebApplicationFactory<Program> offline = api.WithUnreachableDatabase();
        using HttpClient client = offline.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/warehouses");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("database_unavailable", problem.Code);
    }

    private static async Task<JsonElement[]> ReadListAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];

    private static bool Is(JsonElement warehouse, string code, string name) =>
        warehouse.GetProperty("code").GetString() == code && warehouse.GetProperty("name").GetString() == name;
}
