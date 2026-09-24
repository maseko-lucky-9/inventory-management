using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class ProductTests(ApiFactory api)
{
    [Fact]
    public async Task CreatingAProductReturns201WithLocationAndTheProduct()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");

        HttpResponseMessage response = await client.PostAsJsonAsync("/products", new { code, description = "Blue widget" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/products/" + code, response.Headers.Location?.OriginalString);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal("Blue widget", body.GetProperty("description").GetString());
        Assert.EndsWith("Z", body.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task CreatingTrimsTheCodeBeforeStoringIt()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");

        HttpResponseMessage response = await client.PostAsJsonAsync("/products", new { code = "  " + code + "  ", description = "Padded" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostingTheSameCodeTwiceReturns409DuplicateProductCodeNamingTheCode()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");
        await CreateAsync(client, code, "First");

        HttpResponseMessage response = await client.PostAsJsonAsync("/products", new { code, description = "Second" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("duplicate_product_code", problem.Code);
        Assert.Contains(code, problem.Detail);
    }

    [Fact]
    public async Task GettingAnUnknownCodeReturns404ProductNotFound()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/products/" + TestData.Unique("NOPE"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("product_not_found", problem.Code);
    }

    [Fact]
    public async Task GettingByCodeReturnsTheCreatedProduct()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");
        JsonElement created = await CreateAsync(client, code, "Fetched widget");

        HttpResponseMessage response = await client.GetAsync("/products/" + code);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal("Fetched widget", body.GetProperty("description").GetString());
        Assert.Equal(created.GetProperty("createdAt").GetString(), body.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task GettingByCodeTrimsTheCodeFromThePath()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");
        await CreateAsync(client, code, "Padded lookup");

        HttpResponseMessage response = await client.GetAsync("/products/%20" + code + "%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListIncludesACreatedProduct()
    {
        using HttpClient client = api.CreateClient();
        string code = TestData.Unique("SKU");
        await CreateAsync(client, code, "Listed widget");

        HttpResponseMessage response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement product = Assert.Single(body.EnumerateArray(), item => item.GetProperty("code").GetString() == code);
        Assert.Equal("Listed widget", product.GetProperty("description").GetString());
        Assert.EndsWith("Z", product.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task ListIsOrderedByCode()
    {
        using HttpClient client = api.CreateClient();
        string prefix = TestData.Unique("SKU");
        // Created in reverse order, so insertion order alone cannot pass.
        await CreateAsync(client, prefix + "-B", "Second by code");
        await CreateAsync(client, prefix + "-A", "First by code");

        JsonElement body = await client.GetFromJsonAsync<JsonElement>("/products");

        string[] codes = body.EnumerateArray()
            .Select(item => item.GetProperty("code").GetString() ?? "")
            .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();
        Assert.Equal([prefix + "-A", prefix + "-B"], codes);
    }

    [Theory]
    [InlineData("/products")]
    [InlineData("/products/SKU-ANY")]
    public async Task ReadsReturn503DatabaseUnavailableWhenTheDatabaseIsUnreachable(string path)
    {
        await using WebApplicationFactory<Program> offline = api.WithUnreachableDatabase();
        using HttpClient client = offline.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("database_unavailable", problem.Code);
    }

    private static async Task<JsonElement> CreateAsync(HttpClient client, string code, string description)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/products", new { code, description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
