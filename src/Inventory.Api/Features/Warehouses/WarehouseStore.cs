using Dapper;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Warehouses;

public sealed class WarehouseStore(NpgsqlDataSource dataSource)
{
    private const string ListSql = """
        SELECT code AS Code, name AS Name
        FROM warehouses
        ORDER BY code
        """;

    // One round trip: the unique constraint, not a prior lookup, decides a duplicate, so racing creates cannot both win.
    private const string InsertSql = """
        INSERT INTO warehouses (code, name)
        VALUES (@Code, @Name)
        RETURNING code AS Code, name AS Name
        """;

    public async Task<IReadOnlyList<Warehouse>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            IEnumerable<Warehouse> warehouses = await connection.QueryAsync<Warehouse>(
                new CommandDefinition(ListSql, cancellationToken: cancellationToken));
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
                new CommandDefinition(InsertSql, new { Code = code, Name = name }, cancellationToken: cancellationToken));
        }
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "warehouse", code, exception) is { } refusal)
        {
            throw refusal;
        }
    }
}
