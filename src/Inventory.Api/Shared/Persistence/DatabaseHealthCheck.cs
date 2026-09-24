using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Inventory.Api.Shared.Persistence;

/// <summary>One round trip; a pooled open alone would not prove the server is reachable.</summary>
public sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    private const string PingSql = "SELECT 1";

    // A thrown exception is reported as Unhealthy (503) by the health check service.
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteScalarAsync<int>(new CommandDefinition(PingSql, cancellationToken: cancellationToken));
        return HealthCheckResult.Healthy();
    }
}
