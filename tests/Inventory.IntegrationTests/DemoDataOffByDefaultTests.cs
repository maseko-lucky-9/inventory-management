using System.Net;
using Dapper;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>Seed:DemoData is opt-in: the shared fixture leaves it unset, as tests, CI and production do.</summary>
[Collection("api")]
public sealed class DemoDataOffByDefaultTests(ApiFactory api)
{
    [Fact]
    public async Task WithoutTheSettingNoDemoProductOrSiteIsLoaded()
    {
        using HttpClient client = api.CreateClient();

        HttpResponseMessage product = await client.GetAsync("/products/" + DemoDataTests.DemoProduct);

        Assert.Equal(HttpStatusCode.NotFound, product.StatusCode);
        await using NpgsqlConnection connection = new(api.ConnectionString);
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM warehouses WHERE code = 'FAC-JHB'"));
    }
}
