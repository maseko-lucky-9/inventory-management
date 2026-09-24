using Dapper;
using Npgsql;

namespace Inventory.Api.Shared.Persistence;

/// <summary>
/// Applies db/schema.sql then db/seed.sql before the server starts listening (ADR-005), then db/demo-data.sql
/// only when Seed:DemoData is true.
/// </summary>
public sealed class SchemaInitializer(NpgsqlDataSource dataSource, IConfiguration configuration) : IHostedLifecycleService
{
    // Any fixed key works, as long as every instance uses the same one.
    private const long LockKey = 7_231_004;
    private const string AdvisoryLockSql = "SELECT pg_advisory_xact_lock(@LockKey)";

    public Task StartingAsync(CancellationToken cancellationToken) => ApplyAsync(cancellationToken);

    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        string schema = await ReadAsync("schema.sql", cancellationToken);
        string seed = await ReadAsync("seed.sql", cancellationToken);
        // Opt-in play data for local runs, added after the four-hour build; off unless the setting says true.
        string? demo = configuration.GetValue<bool>("Seed:DemoData") ? await ReadAsync("demo-data.sql", cancellationToken) : null;

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        // The transaction-scoped lock serialises instances that start together.
        await connection.ExecuteAsync(new CommandDefinition(AdvisoryLockSql, new { LockKey }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(schema, transaction: transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(seed, transaction: transaction, cancellationToken: cancellationToken));
        if (demo is not null)
        {
            await connection.ExecuteAsync(new CommandDefinition(demo, transaction: transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static Task<string> ReadAsync(string file, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", file), cancellationToken);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
