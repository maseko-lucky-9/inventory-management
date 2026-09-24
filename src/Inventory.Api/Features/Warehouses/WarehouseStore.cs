using Dapper;
using Inventory.Api.Shared.Auth;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Warehouses;

/// <summary>Warehouses are scoped: a caller lists only the ones linked to them, and is linked to each one they create (ADR-006).</summary>
public sealed class WarehouseStore(NpgsqlDataSource dataSource, ICurrentUser currentUser)
{
    // The inner join is the scope: a warehouse with no link to the caller has no row to return.
    private const string ListSql = """
        SELECT w.code AS Code, w.name AS Name
        FROM warehouses w
        JOIN user_warehouses uw ON uw.warehouse_id = w.id AND uw.user_id = @UserId
        ORDER BY w.code
        """;

    // One statement, so one transaction: the warehouse and its creator's link are stored together or not at all (G37).
    // The unique constraint, not a prior lookup, decides a duplicate, so racing creates cannot both win,
    // and an existing code fails the whole statement, so it never links the caller to someone else's warehouse.
    private const string InsertSql = """
        WITH created AS (
            INSERT INTO warehouses (code, name)
            VALUES (@Code, @Name)
            RETURNING id, code, name
        ), linked AS (
            INSERT INTO user_warehouses (user_id, warehouse_id)
            SELECT @UserId, created.id FROM created
        )
        SELECT created.code AS Code, created.name AS Name
        FROM created
        """;

    public async Task<IReadOnlyList<Warehouse>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            IEnumerable<Warehouse> warehouses = await connection.QueryAsync<Warehouse>(
                new CommandDefinition(ListSql, new { currentUser.UserId }, cancellationToken: cancellationToken));
            return warehouses.AsList();
        }
        // A read names no code; the translator only needs one for a duplicate, which a SELECT cannot raise.
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "warehouse", string.Empty, exception) is { } refusal)
        {
            throw refusal;
        }
    }

    public async Task<Warehouse> CreateAsync(string code, string name, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.QuerySingleAsync<Warehouse>(
                new CommandDefinition(InsertSql, new { Code = code, Name = name, currentUser.UserId }, cancellationToken: cancellationToken));
        }
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "warehouse", code, exception) is { } refusal)
        {
            throw refusal;
        }
    }
}
