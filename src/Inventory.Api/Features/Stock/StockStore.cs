using Dapper;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Stock;

public sealed class StockStore(NpgsqlDataSource dataSource)
{
    // One statement: the upsert adds to the stored level, so concurrent receipts never lose an update.
    private const string ReceiveSql = """
        INSERT INTO stock (product_id, warehouse_id, quantity)
        SELECT p.id, w.id, @Quantity
        FROM products p
        JOIN warehouses w ON w.code = @WarehouseCode
        WHERE p.code = @ProductCode
        ON CONFLICT (product_id, warehouse_id)
        DO UPDATE SET quantity = stock.quantity + EXCLUDED.quantity, updated_at = now()
        RETURNING quantity
        """;

    // An absent filter matches every row. Each call is planned with its values, so a present filter
    // reaches stock through the primary key (product_id first) or stock_warehouse_id_idx.
    private const string ListSql = """
        SELECT p.code AS ProductCode, w.code AS WarehouseCode, s.quantity AS Quantity, s.updated_at AS UpdatedAt
        FROM stock s
        JOIN products p ON p.id = s.product_id
        JOIN warehouses w ON w.id = s.warehouse_id
        WHERE (@ProductCode IS NULL OR p.code = @ProductCode)
          AND (@WarehouseCode IS NULL OR w.code = @WarehouseCode)
        ORDER BY p.code, w.code
        """;

    private const string CodesExistSql = """
        SELECT EXISTS (SELECT 1 FROM products WHERE code = @ProductCode) AS ProductExists,
               EXISTS (SELECT 1 FROM warehouses WHERE code = @WarehouseCode) AS WarehouseExists
        """;

    /// <summary>Adds the quantity to the level and returns the resulting level.</summary>
    public async Task<int> ReceiveAsync(string productCode, string warehouseCode, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            int? level = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
                ReceiveSql,
                new { ProductCode = productCode, WarehouseCode = warehouseCode, Quantity = quantity },
                cancellationToken: cancellationToken));
            // No row means a code matched nothing; the product is named first when both are unknown.
            return level ?? throw ((await CodesExistAsync(connection, productCode, warehouseCode, cancellationToken)).Product
                ? new UnknownCodeException("warehouse", warehouseCode)
                : new UnknownCodeException("product", productCode));
        }
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "product", productCode, exception) is { } refusal)
        {
            throw refusal;
        }
    }

    /// <summary>Levels matching every filter given, ordered by product code then warehouse code.</summary>
    public async Task<IReadOnlyList<StockLevel>> ListAsync(string? productCode, string? warehouseCode, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            List<StockLevel> levels = (await connection.QueryAsync<StockLevel>(new CommandDefinition(
                ListSql,
                new { ProductCode = productCode, WarehouseCode = warehouseCode },
                cancellationToken: cancellationToken))).AsList();
            // Only an empty result can hide an unknown filter code; a known code with no stock is an empty list.
            if (levels.Count == 0)
            {
                await EnsureFiltersExistAsync(connection, productCode, warehouseCode, cancellationToken);
            }

            return levels;
        }
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "product", productCode ?? "", exception) is { } refusal)
        {
            throw refusal;
        }
    }

    private static async Task EnsureFiltersExistAsync(
        NpgsqlConnection connection, string? productCode, string? warehouseCode, CancellationToken cancellationToken)
    {
        (bool productExists, bool warehouseExists) = await CodesExistAsync(connection, productCode, warehouseCode, cancellationToken);
        if (productCode is not null && !productExists)
        {
            throw new NotFoundException("product", productCode);
        }

        if (warehouseCode is not null && !warehouseExists)
        {
            throw new NotFoundException("warehouse", warehouseCode);
        }
    }

    private static Task<(bool Product, bool Warehouse)> CodesExistAsync(
        NpgsqlConnection connection, string? productCode, string? warehouseCode, CancellationToken cancellationToken) =>
        connection.QuerySingleAsync<(bool Product, bool Warehouse)>(new CommandDefinition(
            CodesExistSql,
            new { ProductCode = productCode, WarehouseCode = warehouseCode },
            cancellationToken: cancellationToken));
}
