using System.Net;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class HealthTests(ApiFactory api)
{
    [Fact]
    public async Task HealthReportsHealthyWhenTheDatabaseIsReachable()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
