using Dapper;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Auth;

/// <summary>Users are not warehouse-scoped: login reads one by name, and startup sets the demo users' hashes (ADR-008).</summary>
public sealed class UserStore(NpgsqlDataSource dataSource)
{
    private const string FindSql = """
        SELECT id AS Id, username AS Username, password_hash AS PasswordHash
        FROM users
        WHERE username = @Username
        """;

    private const string SetPasswordHashSql = """
        UPDATE users SET password_hash = @PasswordHash
        WHERE username = @Username
        """;

    public async Task<User?> FindAsync(string username, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<User>(
                new CommandDefinition(FindSql, new { Username = username }, cancellationToken: cancellationToken));
        }
        // An outage is 503 database_unavailable, as on every other path; a read cannot raise a duplicate.
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "user", username, exception) is { } refusal)
        {
            throw refusal;
        }
    }

    // Startup only: a failure here should stop the host with the database's own error, so nothing is translated.
    public async Task SetPasswordHashAsync(string username, string? passwordHash, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            SetPasswordHashSql, new { Username = username, PasswordHash = passwordHash }, cancellationToken: cancellationToken));
    }
}
