using System.Net;
using System.Text;
using System.Text.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class ValidationTests(ApiFactory api)
{
    [Theory]
    [InlineData("/products", """{"description":"No code"}""", "code")]
    [InlineData("/warehouses", """{"code":"WH 1","name":"Space inside the code"}""", "code")]
    [InlineData("/stock", """{"productCode":"SKU-1","warehouseCode":"WH-A","quantity":0}""", "quantity")]
    [InlineData("/orders", """{"productCode":".SKU","sourceWarehouseCode":"WH-A","destinationWarehouseCode":"WH-B","quantity":1}""", "productCode")]
    public async Task AnInvalidBodyIs400ValidationFailedKeyedByTheCamelCaseField(string path, string json, string field)
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertValidationFailedAsync(response, field);
    }

    [Fact]
    public async Task AStockQueryWithNoFilterIs400ValidationFailedNamingTheRule()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/stock");

        JsonElement messages = await AssertValidationFailedAsync(response, "productCode");
        Assert.Contains("at least one filter", messages[0].GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStockFilterOutsideTheCodePatternIs400ValidationFailedKeyedByThatFilter()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/stock?warehouseCode=WH%20A");

        await AssertValidationFailedAsync(response, "warehouseCode");
    }

    // Returns the messages under the one expected key.
    private static async Task<JsonElement> AssertValidationFailedAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal(("validation_failed", "One or more fields are invalid."), (problem.Code, problem.Detail));
        JsonProperty error = Assert.Single(problem.Body.GetProperty("errors").EnumerateObject());
        Assert.Equal(field, error.Name);
        return error.Value;
    }
}
