using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Inventory.IntegrationTests;

/// <summary>The Swagger UI and the document it reads are served only when OpenApi:Enabled is true (Development's default).</summary>
[Collection("api")]
public sealed class SwaggerUiTests(ApiFactory api)
{
    [Theory]
    [InlineData("/swagger/index.html", "text/html")]
    [InlineData("/openapi/v1.json", "application/json")]
    public async Task TheSwaggerUiAndItsDocumentAnswerWithoutATokenInDevelopment(string path, string mediaType)
    {
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
    }

    // Microsoft.AspNetCore.OpenApi stays the only document source; the UI just points at it.
    // Swashbuckle writes the document URL into its embedded index.js (configObject), not into index.html.
    [Fact]
    public async Task TheSwaggerUiReadsTheBuiltInDocument()
    {
        using HttpClient client = api.CreateAnonymousClient();

        string script = await client.GetStringAsync("/swagger/index.js");

        Assert.Contains("/openapi/v1.json", script, StringComparison.Ordinal);
    }

    // Nothing is mapped, so a signed-in caller gets the plain 404 of any unknown path.
    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task WithOpenApiDisabledASignedInCallerGets404(string path)
    {
        await using WebApplicationFactory<Program> disabled = api.WithWebHostBuilder(builder => builder.UseSetting("OpenApi:Enabled", "false"));
        using HttpClient client = disabled.CreateClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // With no endpoint matched, the fallback policy still runs and refuses the anonymous caller before any 404:
    // the same answer as any unknown path, so a disabled UI is indistinguishable from one that never existed.
    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task WithOpenApiDisabledAnAnonymousCallerGets401(string path)
    {
        await using WebApplicationFactory<Program> disabled = api.WithWebHostBuilder(builder => builder.UseSetting("OpenApi:Enabled", "false"));
        using HttpClient client = ApiFactory.WithoutToken(disabled.CreateClient());

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await Problem.ReadAsync(response)).Code);
    }

    // The setting, not the environment, decides: Compose runs Production and turns it on with OPENAPI_ENABLED=true.
    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task AProductionHostServesTheSwaggerUiWhenOpenApiIsEnabled(string path)
    {
        await using WebApplicationFactory<Program> production = api.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("OpenApi:Enabled", "true");
        });
        using HttpClient client = ApiFactory.WithoutToken(production.CreateClient());

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
