using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Inventory.Api.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>
/// Seed:DemoData=true loads db/demo-data.sql, read back through the normal endpoints (added after the four-hour build).
/// Every test starts its own postgres:17-alpine container, so the shared fixture's database never holds demo rows.
/// </summary>
public sealed class DemoDataTests : IAsyncLifetime
{
    /// <summary>Made in FAC-DBN (bob's factory) and held in DC-CPT (alice), DC-PLZ (alice and bob) and DC-BFN (bob).</summary>
    public const string DemoProduct = "VLV-BALL-DN50";

    private const string RowCountsSql = """
        SELECT (SELECT count(*) FROM products), (SELECT count(*) FROM warehouses), (SELECT count(*) FROM users),
               (SELECT count(*) FROM user_warehouses), (SELECT count(*) FROM stock), (SELECT count(*) FROM transfer_orders)
        """;

    private const string StockedSitesSql = """
        SELECT w.code FROM stock s
        JOIN products p ON p.id = s.product_id
        JOIN warehouses w ON w.id = s.warehouse_id
        WHERE p.code = @productCode
        ORDER BY w.code
        """;

    private readonly ApiFactory demo = new() { DemoData = true };

    public Task InitializeAsync() => demo.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)demo).DisposeAsync();

    [Fact]
    public async Task TheProductListIncludesADemoProduct()
    {
        using HttpClient client = demo.CreateClient();

        JsonElement[] products = await client.GetFromJsonAsync<JsonElement[]>("/products") ?? [];

        Assert.Contains(DemoProduct, products.Select(product => product.GetProperty("code").GetString() ?? ""));
    }

    [Fact]
    public async Task AliceSeesWhAPlusHerDemoFactoriesAndDistributionCentres()
    {
        using HttpClient client = demo.CreateClientFor("alice");

        Assert.Equal(["DC-CPT", "DC-PLZ", "FAC-JHB", "FAC-PTA", "WH-A"], await WarehouseCodesAsync(client));
    }

    [Fact]
    public async Task AStockQueryForADemoProductReturnsAliceOnlyTheRowsInHerLinkedSites()
    {
        using HttpClient client = demo.CreateClientFor("alice");

        HttpResponseMessage response = await client.GetAsync($"/stock?productCode={DemoProduct}");

        // Not vacuous: the product is also held in sites alice is not linked to.
        Assert.Equal(["DC-BFN", "DC-CPT", "DC-PLZ", "FAC-DBN"], await StockedSitesAsync(DemoProduct));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement[] rows = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        Assert.Equal(["DC-CPT", "DC-PLZ"], rows.Select(row => row.GetProperty("warehouseCode").GetString() ?? ""));
    }

    // The spec needs one user with no links; the demo data must not give carol any.
    [Fact]
    public async Task CarolStillSeesNoWarehousesAndNoStock()
    {
        using HttpClient client = demo.CreateClientFor("carol");

        JsonElement[] stock = await client.GetFromJsonAsync<JsonElement[]>($"/stock?productCode={DemoProduct}") ?? [];

        Assert.Empty(await WarehouseCodesAsync(client));
        Assert.Empty(stock);
    }

    [Fact]
    public async Task ApplyingTheInitializerAgainChangesNoRowCount()
    {
        SchemaInitializer initializer = Initializer();
        (long, long, long, long, long, long) before = await RowCountsAsync();

        await initializer.ApplyAsync(CancellationToken.None);

        Assert.Equal(before, await RowCountsAsync());
    }

    // A restart re-applies the file; it must never reset a level the owner changed by playing.
    [Fact]
    public async Task ApplyingTheInitializerAgainKeepsALevelChangedSinceStartup()
    {
        using HttpClient client = demo.CreateClientFor("alice");
        HttpResponseMessage receipt = await client.PostAsJsonAsync(
            "/stock", new { productCode = DemoProduct, warehouseCode = "DC-CPT", quantity = 5 });
        int? changed = await Seed.QuantityAsync(demo, DemoProduct, "DC-CPT");

        await Initializer().ApplyAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        Assert.Equal(changed, await Seed.QuantityAsync(demo, DemoProduct, "DC-CPT"));
    }

    private SchemaInitializer Initializer() =>
        demo.Services.GetServices<IHostedService>().OfType<SchemaInitializer>().Single();

    private static async Task<string[]> WarehouseCodesAsync(HttpClient client)
    {
        JsonElement[] warehouses = await client.GetFromJsonAsync<JsonElement[]>("/warehouses") ?? [];
        return [.. warehouses.Select(warehouse => warehouse.GetProperty("code").GetString() ?? "")];
    }

    private async Task<string[]> StockedSitesAsync(string productCode)
    {
        await using NpgsqlConnection connection = new(demo.ConnectionString);
        return [.. await connection.QueryAsync<string>(StockedSitesSql, new { productCode })];
    }

    private async Task<(long, long, long, long, long, long)> RowCountsAsync()
    {
        _ = demo.Server;
        await using NpgsqlConnection connection = new(demo.ConnectionString);
        return await connection.QuerySingleAsync<(long, long, long, long, long, long)>(RowCountsSql);
    }
}
