using Dapper;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>Arranges and inspects rows directly, so each slice's tests stand alone. The act under test always goes through HTTP.</summary>
public static class Seed
{
    public static Task ProductAsync(ApiFactory api, string code) => ExecuteAsync(api,
        "INSERT INTO products (code, description) VALUES (@code, @code)", new { code });

    /// <summary>Returns the new id; ids ascend in creation order, which the lock-order tests rely on.</summary>
    public static async Task<long> WarehouseAsync(ApiFactory api, string code)
    {
        await using NpgsqlConnection connection = Open(api);
        return await connection.ExecuteScalarAsync<long>(
            "INSERT INTO warehouses (code, name) VALUES (@code, @code) RETURNING id", new { code });
    }

    /// <summary>Sets (not adds) the level of one product in one warehouse.</summary>
    public static Task StockAsync(ApiFactory api, string productCode, string warehouseCode, int quantity) => ExecuteAsync(api,
        """
        INSERT INTO stock (product_id, warehouse_id, quantity)
        SELECT p.id, w.id, @quantity FROM products p JOIN warehouses w ON w.code = @warehouseCode WHERE p.code = @productCode
        ON CONFLICT (product_id, warehouse_id) DO UPDATE SET quantity = EXCLUDED.quantity
        """,
        new { productCode, warehouseCode, quantity });

    /// <summary>The stored level, or null when no stock row exists.</summary>
    public static async Task<int?> QuantityAsync(ApiFactory api, string productCode, string warehouseCode)
    {
        await using NpgsqlConnection connection = Open(api);
        return await connection.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT s.quantity FROM stock s
            JOIN products p ON p.id = s.product_id
            JOIN warehouses w ON w.id = s.warehouse_id
            WHERE p.code = @productCode AND w.code = @warehouseCode
            """,
            new { productCode, warehouseCode });
    }

    public static async Task<int> OrderCountAsync(ApiFactory api, string productCode)
    {
        await using NpgsqlConnection connection = Open(api);
        return await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM transfer_orders t JOIN products p ON p.id = t.product_id WHERE p.code = @productCode",
            new { productCode });
    }

    public static async Task<long> UserIdAsync(ApiFactory api, string username)
    {
        await using NpgsqlConnection connection = Open(api);
        return await connection.ExecuteScalarAsync<long>("SELECT id FROM users WHERE username = @username", new { username });
    }

    private static async Task ExecuteAsync(ApiFactory api, string sql, object parameters)
    {
        await using NpgsqlConnection connection = Open(api);
        await connection.ExecuteAsync(sql, parameters);
    }

    // Touching Server starts the host, so the schema exists before the first row is written.
    private static NpgsqlConnection Open(ApiFactory api)
    {
        _ = api.Server;
        return new NpgsqlConnection(api.ConnectionString);
    }
}
