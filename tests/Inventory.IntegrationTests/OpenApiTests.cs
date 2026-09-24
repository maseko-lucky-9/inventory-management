using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class OpenApiTests(ApiFactory api)
{
    // Each path's first segment names its feature; the Swagger UI groups operations by these tags.
    private static readonly Dictionary<string, string> FeatureTags = new(StringComparer.Ordinal)
    {
        ["auth"] = "Auth",
        ["products"] = "Products",
        ["warehouses"] = "Warehouses",
        ["stock"] = "Stock",
        ["orders"] = "Orders",
    };

    [Fact]
    public async Task TheOpenApiDocumentIsServedInDevelopmentAndListsOrders()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(document.GetProperty("paths").EnumerateObject(), path => path.Name.TrimEnd('/') == "/orders");
    }

    [Fact]
    public async Task TheOpenApiDocumentNamesTheStockFiltersAsTheContractSpellsThem()
    {
        using HttpClient client = api.CreateClient();

        JsonElement document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");

        JsonElement parameters = document.GetProperty("paths").GetProperty("/stock").GetProperty("get").GetProperty("parameters");
        Assert.Equal(["productCode", "warehouseCode"], parameters.EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString()));
    }

    // The setting's default, not an environment gate: only appsettings.Development.json turns OpenApi:Enabled on,
    // so Production gets false unless it is set (SwaggerUiTests covers a Production host that sets it).
    [Fact]
    public async Task AProductionHostDoesNotServeTheOpenApiDocumentByDefault()
    {
        await using WebApplicationFactory<Program> production = api.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Production));
        using HttpClient client = production.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheOpenApiDocumentDeclaresABearerJwtSecurityScheme()
    {
        JsonElement document = await DocumentAsync();

        JsonElement scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
        Assert.Equal("JWT", scheme.GetProperty("bearerFormat").GetString());
    }

    // The Swagger UI sends the token only to operations that ask for it, and login is the one that cannot have one yet.
    [Fact]
    public async Task EveryOperationButLoginRequiresTheBearerScheme()
    {
        (string Key, JsonElement Operation)[] operations = Operations(await DocumentAsync());

        Assert.Contains(operations, operation => operation.Key == "GET /products");
        Assert.Contains(operations, operation => operation.Key == "POST /auth/login");
        Assert.All(operations, operation => Assert.True(
            RequiresBearer(operation.Operation) == (operation.Key != "POST /auth/login"),
            operation.Key + " has the wrong security requirement."));
    }

    [Fact]
    public async Task EveryOperationIsTaggedWithItsFeatureAndHasASummary()
    {
        (string Key, JsonElement Operation)[] operations = Operations(await DocumentAsync());

        Assert.All(operations, operation =>
        {
            string segment = operation.Key.Split(' ')[1].Split('/')[1];
            Assert.Equal([FeatureTags[segment]], operation.Operation.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
            Assert.False(string.IsNullOrWhiteSpace(operation.Operation.GetProperty("summary").GetString()), operation.Key + " has no summary.");
        });
        Assert.Equal(FeatureTags.Values.Order(), operations.Select(operation => FeatureTags[operation.Key.Split(' ')[1].Split('/')[1]]).Distinct().Order());
    }

    // The error statuses of the README's API table, plus 401 on every route that needs a token; each is Problem Details.
    [Theory]
    [InlineData("POST /auth/login", new[] { 400, 401, 429 })]
    [InlineData("GET /products", new[] { 401 })]
    [InlineData("GET /products/{code}", new[] { 401, 404 })]
    [InlineData("POST /products", new[] { 400, 401, 409 })]
    [InlineData("GET /warehouses", new[] { 401 })]
    [InlineData("POST /warehouses", new[] { 400, 401, 409 })]
    [InlineData("POST /stock", new[] { 400, 401 })]
    [InlineData("GET /stock", new[] { 400, 401, 404 })]
    [InlineData("POST /orders", new[] { 400, 401, 503 })]
    public async Task EveryOperationDocumentsItsProblemStatuses(string key, int[] statuses)
    {
        JsonElement operation = Operations(await DocumentAsync()).Single(candidate => candidate.Key == key).Operation;

        JsonProperty[] errors = [.. operation.GetProperty("responses").EnumerateObject().Where(response => response.Name[0] != '2')];
        Assert.Equal(statuses.Select(status => status.ToString(CultureInfo.InvariantCulture)), errors.Select(error => error.Name).Order(StringComparer.Ordinal));
        Assert.All(errors, error => Assert.True(error.Value.GetProperty("content").TryGetProperty("application/problem+json", out _), key + " " + error.Name));
    }

    private async Task<JsonElement> DocumentAsync()
    {
        using HttpClient client = api.CreateClient();
        return await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
    }

    // "METHOD /path" with any trailing slash trimmed, so a group's root reads as "/products".
    private static (string Key, JsonElement Operation)[] Operations(JsonElement document) =>
    [
        .. document.GetProperty("paths").EnumerateObject().SelectMany(path => path.Value.EnumerateObject()
            .Select(operation => (operation.Name.ToUpperInvariant() + " " + path.Name.TrimEnd('/'), operation.Value))),
    ];

    private static bool RequiresBearer(JsonElement operation) =>
        operation.TryGetProperty("security", out JsonElement security)
        && security.EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _));
}
