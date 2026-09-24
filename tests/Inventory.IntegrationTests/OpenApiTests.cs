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

    [Fact]
    public async Task TheOpenApiDocumentIsNotServedOutsideDevelopment()
    {
        await using WebApplicationFactory<Program> production = api.WithWebHostBuilder(
            builder => builder.UseEnvironment(Environments.Production));
        using HttpClient client = production.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
