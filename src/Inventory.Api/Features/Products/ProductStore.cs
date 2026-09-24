using Dapper;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Products;

/// <summary>Products are a global catalogue, so no query here joins user_warehouses.</summary>
public sealed class ProductStore(NpgsqlDataSource dataSource)
{
    private const string ListSql = """
        SELECT code AS Code, description AS Description, created_at AS CreatedAt
        FROM products
        ORDER BY code
        """;

    private const string FindSql = """
        SELECT code AS Code, description AS Description, created_at AS CreatedAt
        FROM products
        WHERE code = @Code
        """;

    // No SELECT before the INSERT: the UNIQUE constraint decides, so two racing creates cannot both win (ADR-004).
    private const string CreateSql = """
        INSERT INTO products (code, description)
        VALUES (@Code, @Description)
        RETURNING code AS Code, description AS Description, created_at AS CreatedAt
        """;

    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            IEnumerable<Product> products = await connection.QueryAsync<Product>(
                new CommandDefinition(ListSql, cancellationToken: cancellationToken));
            return products.AsList();
        }
        catch (NpgsqlException exception) when (Refusal(exception, string.Empty) is { } refusal)
        {
            throw refusal;
        }
    }

    public async Task<Product?> FindAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.QuerySingleOrDefaultAsync<Product>(
                new CommandDefinition(FindSql, new { Code = code }, cancellationToken: cancellationToken));
        }
        catch (NpgsqlException exception) when (Refusal(exception, code) is { } refusal)
        {
            throw refusal;
        }
    }

    public async Task<Product> CreateAsync(string code, string description, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            return await connection.QuerySingleAsync<Product>(
                new CommandDefinition(CreateSql, new { Code = code, Description = description }, cancellationToken: cancellationToken));
        }
        catch (NpgsqlException exception) when (Refusal(exception, code) is { } refusal)
        {
            throw refusal;
        }
    }

    // Reads translate too, so an outage is 503 database_unavailable on every path; unmapped states stay 500 defects.
    // The code only reaches a 23505 duplicate, which a read cannot raise.
    private static DomainException? Refusal(NpgsqlException exception, string code) =>
        DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "product", code);
}
