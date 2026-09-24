using System.Net.Http.Headers;
using System.Security.Cryptography;
using Dapper;
using Inventory.Api.Features.Auth;
using Inventory.Api.Shared.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Inventory.IntegrationTests;

/// <summary>One postgres:17-alpine container and one API host, shared by the "api" collection.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The user every client from CreateClient() is signed in as.</summary>
    public const string FixtureUsername = "fixture";

    // Returns the id whether the row is new or already there.
    private const string EnsureUserSql = """
        INSERT INTO users (username) VALUES (@username)
        ON CONFLICT (username) DO UPDATE SET username = EXCLUDED.username
        RETURNING id
        """;

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    // Throwaway secrets, new every run and never printed.
    private readonly string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public string ConnectionString => database.GetConnectionString();

    /// <summary>The password the host hashes into the demo users at startup.</summary>
    public string DemoPassword { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    public Task InitializeAsync() => database.StartAsync();

    // UseSetting reaches Program's configuration before Build(), unlike ConfigureAppConfiguration.
    // Hosts derived with WithWebHostBuilder inherit these, so they share the signing key and the demo password.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Inventory", database.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", signingKey);
        builder.UseSetting("DemoUsers:Password", DemoPassword);
        // Far above what the suite needs; the rate-limit test builds its own host with a limit of one.
        builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
    }

    /// <summary>A client signed in as the given user, whose row is created if it is missing.</summary>
    public HttpClient CreateClientFor(string username)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Authorization = Bearer(username);
        return client;
    }

    /// <summary>A client that sends no token.</summary>
    public HttpClient CreateAnonymousClient() => WithoutToken(CreateClient());

    /// <summary>Strips the default token from a client of any host, including one derived with WithWebHostBuilder.</summary>
    public static HttpClient WithoutToken(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    // Every client starts signed in as the fixture user, so tests that are not about authentication need no login.
    // Hosts derived with WithWebHostBuilder call this too; they share the signing key, so the token opens them as well.
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Authorization = Bearer(FixtureUsername);
    }

    // Minted by the app's own issuer, so the token is exactly what login would return for this user.
    private AuthenticationHeaderValue Bearer(string username)
    {
        TokenIssuer issuer = Services.GetRequiredService<TokenIssuer>();
        using NpgsqlConnection connection = new(ConnectionString);
        long userId = connection.ExecuteScalar<long>(EnsureUserSql, new { username });
        return new AuthenticationHeaderValue("Bearer", issuer.Issue(userId, username).AccessToken);
    }

    /// <summary>A new, empty database on the same server, for a host whose startup must not touch the shared data.</summary>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        string name = "t" + Guid.NewGuid().ToString("N");
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync();
        // CREATE DATABASE takes no parameters; the name is generated above, never input.
        await using NpgsqlCommand command = new("CREATE DATABASE " + name, connection);
        await command.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>The real pipeline pointed at a port nothing listens on, for outage tests. Dispose it after use.</summary>
    public WebApplicationFactory<Program> WithUnreachableDatabase() => WithWebHostBuilder(builder =>
    {
        builder.UseSetting("ConnectionStrings:Inventory", new NpgsqlConnectionStringBuilder(ConnectionString) { Port = 1 }.ConnectionString);
        // Both startup steps need the database, so either would fail startup before any request is served.
        builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(SchemaInitializer)));
            services.Remove(services.Single(descriptor => descriptor.ImplementationType == typeof(DemoPasswordInitializer)));
        });
    });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
    }
}
