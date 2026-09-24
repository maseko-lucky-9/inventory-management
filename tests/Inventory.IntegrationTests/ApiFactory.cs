using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
    }
}
