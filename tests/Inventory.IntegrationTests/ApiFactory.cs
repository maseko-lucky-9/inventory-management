using Inventory.Api.Shared.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Inventory.IntegrationTests;

/// <summary>One postgres:17-alpine container and one API host, shared by the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string ConnectionString => database.GetConnectionString();

    public Task InitializeAsync() => database.StartAsync();

    // UseSetting reaches Program's configuration before Build(), unlike ConfigureAppConfiguration.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Inventory", database.GetConnectionString());

    /// <summary>The real pipeline pointed at a port nothing listens on, for outage tests. Dispose it after use.</summary>
    public WebApplicationFactory<Program> WithUnreachableDatabase() => WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:Inventory", new NpgsqlConnectionStringBuilder(ConnectionString) { Port = 1 }.ConnectionString);
        // The schema step needs the database, so it would fail startup before any request is served.
        builder.ConfigureTestServices(services =>
            services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(SchemaInitializer))));
    });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
    }
}
