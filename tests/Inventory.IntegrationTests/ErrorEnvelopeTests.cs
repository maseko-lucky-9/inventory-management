using System.Net;
using System.Text.Json;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class ErrorEnvelopeTests(ApiFactory api)
{
    [Fact]
    public async Task UnknownRouteAnswersWithProblemDetailsCarryingCodeAndTraceId()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/no-such-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }
}
