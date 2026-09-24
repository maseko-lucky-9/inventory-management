using Dapper;
using Inventory.Api.Shared.Errors;
using Npgsql;

namespace Inventory.Api.Features.Orders;

/// <summary>One transfer in one READ COMMITTED transaction; the guarded UPDATE decides (ADR-003).</summary>
public sealed class TransferStore(NpgsqlDataSource dataSource) : ITransferStore
{
    // A literal: SET takes no parameters. Waiting longer than this becomes 503 concurrency_conflict.
    private const string LockTimeoutSql = "SET LOCAL lock_timeout = '3s'";

    private const string ResolveSql = """
        SELECT
            (SELECT id FROM products WHERE code = @ProductCode) AS ProductId,
            (SELECT id FROM warehouses WHERE code = @SourceWarehouseCode) AS SourceId,
            (SELECT id FROM warehouses WHERE code = @DestinationWarehouseCode) AS DestinationId
        """;

    // Trade-off: one row lock per hot (product, source) pair; bucket counters if one item needs > ~200 transfers/s.
    private const string DecrementSql = """
        UPDATE stock SET quantity = quantity - @Quantity, updated_at = now()
        WHERE product_id = @ProductId AND warehouse_id = @SourceId AND quantity >= @Quantity
        """;

    // No stock row means nothing is available (G7).
    private const string AvailableSql = """
        SELECT COALESCE((SELECT quantity FROM stock WHERE product_id = @ProductId AND warehouse_id = @SourceId), 0)
        """;

    private const string IncrementSql = """
        INSERT INTO stock (product_id, warehouse_id, quantity)
        VALUES (@ProductId, @DestinationId, @Quantity)
        ON CONFLICT (product_id, warehouse_id) DO UPDATE SET quantity = stock.quantity + EXCLUDED.quantity, updated_at = now()
        """;

    private const string InsertOrderSql = """
        INSERT INTO transfer_orders (product_id, source_warehouse_id, destination_warehouse_id, quantity, created_by)
        VALUES (@ProductId, @SourceId, @DestinationId, @Quantity, NULL)
        RETURNING id AS Id, created_at AS CreatedAt
        """;

    public async Task<TransferOutcome> ExecuteAsync(TransferCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(LockTimeoutSql, transaction: transaction, cancellationToken: cancellationToken));
            TransferOutcome outcome = await MoveAsync(connection, transaction, command, cancellationToken);
            // Only a completed transfer commits; every refusal undoes whatever ran before it.
            await (outcome is TransferOutcome.Completed
                ? transaction.CommitAsync(cancellationToken)
                : transaction.RollbackAsync(cancellationToken));
            return outcome;
        }
        catch (NpgsqlException exception) when (DbErrorTranslator.Translate((exception as PostgresException)?.SqlState, "product", command.ProductCode) is { } refusal)
        {
            throw refusal;
        }
    }

    private static async Task<TransferOutcome> MoveAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, TransferCommand command, CancellationToken cancellationToken)
    {
        object codes = new { command.ProductCode, command.SourceWarehouseCode, command.DestinationWarehouseCode };
        ResolvedIds ids = await connection.QuerySingleAsync<ResolvedIds>(
            new CommandDefinition(ResolveSql, codes, transaction, cancellationToken: cancellationToken));
        if (FindUnknown(ids, command) is { } unknown)
        {
            return unknown;
        }

        object rows = new { ids.ProductId, ids.SourceId, ids.DestinationId, command.Quantity };
        // Lower warehouse id first: one global lock order leaves no circular wait (Coffman), so opposing transfers cannot deadlock.
        bool destinationFirst = ids.DestinationId < ids.SourceId;
        TransferOutcome.Insufficient? refusal = destinationFirst
            ? await CreditThenDebitAsync(connection, transaction, rows, cancellationToken)
            : await DebitThenCreditAsync(connection, transaction, rows, cancellationToken);
        return refusal ?? await RecordAsync(connection, transaction, command, rows, cancellationToken);
    }

    // A refused debit returns without committing, so ExecuteAsync's rollback also undoes the credit that ran first.
    private static async Task<TransferOutcome.Insufficient?> CreditThenDebitAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, object rows, CancellationToken cancellationToken)
    {
        await CreditAsync(connection, transaction, rows, cancellationToken);
        return await DebitAsync(connection, transaction, rows, cancellationToken);
    }

    private static async Task<TransferOutcome.Insufficient?> DebitThenCreditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, object rows, CancellationToken cancellationToken)
    {
        TransferOutcome.Insufficient? refusal = await DebitAsync(connection, transaction, rows, cancellationToken);
        if (refusal is null)
        {
            await CreditAsync(connection, transaction, rows, cancellationToken);
        }

        return refusal;
    }

    // Null when the guard matched; otherwise the refusal, carrying what is available now.
    private static async Task<TransferOutcome.Insufficient?> DebitAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, object rows, CancellationToken cancellationToken)
    {
        int decremented = await connection.ExecuteAsync(new CommandDefinition(DecrementSql, rows, transaction, cancellationToken: cancellationToken));
        if (decremented > 0)
        {
            return null;
        }

        // QuerySingle, not ExecuteScalar: a NULL must fail loudly, not quietly become 0.
        return new TransferOutcome.Insufficient(await connection.QuerySingleAsync<int>(
            new CommandDefinition(AvailableSql, rows, transaction, cancellationToken: cancellationToken)));
    }

    private static Task<int> CreditAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, object rows, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(IncrementSql, rows, transaction, cancellationToken: cancellationToken));

    private static async Task<TransferOutcome> RecordAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, TransferCommand command, object rows, CancellationToken cancellationToken)
    {
        InsertedOrder inserted = await connection.QuerySingleAsync<InsertedOrder>(
            new CommandDefinition(InsertOrderSql, rows, transaction, cancellationToken: cancellationToken));
        return new TransferOutcome.Completed(new TransferOrder(
            inserted.Id, command.ProductCode, command.SourceWarehouseCode, command.DestinationWarehouseCode, command.Quantity, inserted.CreatedAt));
    }

    // Names the first missing code: product, then source, then destination.
    private static TransferOutcome.UnknownCode? FindUnknown(ResolvedIds ids, TransferCommand command) =>
        ids.ProductId is null ? new TransferOutcome.UnknownCode("product", command.ProductCode)
        : ids.SourceId is null ? new TransferOutcome.UnknownCode("warehouse", command.SourceWarehouseCode)
        : ids.DestinationId is null ? new TransferOutcome.UnknownCode("warehouse", command.DestinationWarehouseCode)
        : null;

    private sealed record ResolvedIds(long? ProductId, long? SourceId, long? DestinationId);

    private sealed record InsertedOrder(long Id, DateTime CreatedAt);
}
