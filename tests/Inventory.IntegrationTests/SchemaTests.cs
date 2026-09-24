using Dapper;
using Inventory.Api.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Inventory.IntegrationTests;

[Collection("api")]
public sealed class SchemaTests(ApiFactory api)
{
    [Fact]
    public async Task FreshDatabaseHasAllSixTables()
    {
        _ = api.Server;
        await using NpgsqlConnection connection = new(api.ConnectionString);

        IEnumerable<string> tables = await connection.QueryAsync<string>(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        Assert.Equal(["products", "stock", "transfer_orders", "user_warehouses", "users", "warehouses"], tables.Order());
    }

    [Fact]
    public async Task SchemaAppliesTwiceWithoutError()
    {
        SchemaInitializer initializer = api.Services.GetServices<IHostedService>().OfType<SchemaInitializer>().Single();

        // Startup already applied it once; a second run must be a no-op, not an error.
        await initializer.ApplyAsync(CancellationToken.None);
    }

    [Fact]
    public void SchemaFilesShipNextToTheBinaries()
    {
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "db", "schema.sql")));
        Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "db", "seed.sql")));
    }
}
