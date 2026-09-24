using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class MalformedRequestTests(ApiFactory api)
{
    [Fact]
    public async Task MalformedJsonIs400MalformedRequest()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/products", Json("""{"code":"SKU-1","""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("malformed_request", (await Problem.ReadAsync(response)).Code);
    }

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("2147483648")]
    public async Task AQuantityThatIsNotAnInt32Is400MalformedRequest(string quantity)
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/stock", Json($$"""{"productCode":"SKU-1","warehouseCode":"WH-A","quantity":{{quantity}}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("malformed_request", (await Problem.ReadAsync(response)).Code);
    }

    [Fact]
    public async Task ATextPlainBodyIs415UnsupportedMediaType()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.PostAsync(
            "/products", new StringContent("""{"code":"SKU-1","description":"Plain"}""", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("unsupported_media_type", (await Problem.ReadAsync(response)).Code);
    }

    // In Development the framework throws on a bad body and the exception handler answers; elsewhere it sets a
    // bare status and the status-code pages answer. Both paths must produce the same code and detail.
    [Theory]
    [InlineData("Development", "application/json", """{"code":""", HttpStatusCode.BadRequest, "malformed_request", "The request body could not be read.")]
    [InlineData("Production", "application/json", """{"code":""", HttpStatusCode.BadRequest, "malformed_request", "The request body could not be read.")]
    [InlineData("Development", "text/plain", """{"code":"SKU-1","description":"Plain"}""", HttpStatusCode.UnsupportedMediaType, "unsupported_media_type", "The request body must be JSON.")]
    [InlineData("Production", "text/plain", """{"code":"SKU-1","description":"Plain"}""", HttpStatusCode.UnsupportedMediaType, "unsupported_media_type", "The request body must be JSON.")]
    public async Task ABadBodyAnswersTheSameProblemInEveryEnvironment(
        string environment, string contentType, string body, HttpStatusCode status, string code, string detail)
    {
        await using WebApplicationFactory<Program> host = api.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using HttpClient client = host.CreateClient();

        HttpResponseMessage response = await client.PostAsync("/products", new StringContent(body, Encoding.UTF8, contentType));

        Assert.Equal(status, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal((code, detail), (problem.Code, problem.Detail));
    }

    [Theory]
    [InlineData("POST", "/products", "application/json", """{"code":""", HttpStatusCode.BadRequest, "malformed_request")]
    [InlineData("POST", "/warehouses", "application/json", "", HttpStatusCode.BadRequest, "malformed_request")]
    [InlineData("POST", "/orders", "application/json", "[]", HttpStatusCode.BadRequest, "malformed_request")]
    [InlineData("POST", "/orders", "application/json", """{"productCode":"SKU-1","sourceWarehouseCode":"WH-A","destinationWarehouseCode":"WH-B","quantity":1.5}""", HttpStatusCode.BadRequest, "malformed_request")]
    [InlineData("POST", "/stock", "text/plain", """{"productCode":"SKU-1","warehouseCode":"WH-A","quantity":1}""", HttpStatusCode.UnsupportedMediaType, "unsupported_media_type")]
    [InlineData("POST", "/products", "application/json", """{"description":"No code"}""", HttpStatusCode.BadRequest, "validation_failed")]
    [InlineData("GET", "/stock", null, null, HttpStatusCode.BadRequest, "validation_failed")]
    [InlineData("GET", "/products/NO-SUCH-SKU", null, null, HttpStatusCode.NotFound, "product_not_found")]
    [InlineData("GET", "/stock?warehouseCode=NO-SUCH-WH", null, null, HttpStatusCode.NotFound, "warehouse_not_found")]
    [InlineData("DELETE", "/products", null, null, HttpStatusCode.MethodNotAllowed, "method_not_allowed")]
    [InlineData("GET", "/no-such-route", null, null, HttpStatusCode.NotFound, "not_found")]
    public async Task EveryRefusalIsProblemDetailsWithItsCodeAndATraceId(
        string method, string path, string? contentType, string? body, HttpStatusCode status, string code)
    {
        using HttpClient client = api.CreateClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        if (contentType is not null)
        {
            request.Content = new StringContent(body ?? "", Encoding.UTF8, contentType);
        }

        HttpResponseMessage response = await client.SendAsync(request);

        // Problem.ReadAsync asserts application/problem+json, a traceId, a detail and the path as instance.
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await Problem.ReadAsync(response)).Code);
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
