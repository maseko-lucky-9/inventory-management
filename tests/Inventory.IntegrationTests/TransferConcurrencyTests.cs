using System.Net;
using System.Net.Http.Json;
using Dapper;
using Npgsql;

namespace Inventory.IntegrationTests;

/// <summary>ADR-003 proofs: the guarded decrement never oversells, and a blocked transfer re-checks committed stock.</summary>
[Collection("api")]
public sealed class TransferConcurrencyTests(ApiFactory api)
{
    // The test's own guarded decrement, standing in for a transfer that holds the source row.
    private const string TakeSql = """
        UPDATE stock SET quantity = quantity - @quantity
        WHERE product_id = (SELECT id FROM products WHERE code = @product) AND warehouse_id = @sourceId AND quantity >= @quantity
        """;

    private const string LockRowSql = """
        SELECT 1 FROM stock
        WHERE product_id = (SELECT id FROM products WHERE code = @product) AND warehouse_id = @sourceId
        FOR UPDATE
        """;

    // A transfer's decrement waiting on the holder's row lock, not merely running.
    private const string BlockedDecrementSql = """
        SELECT count(*)::int FROM pg_stat_activity
        WHERE datname = current_database() AND pid <> pg_backend_pid()
          AND query LIKE 'UPDATE stock%' AND wait_event_type = 'Lock'
          AND @holderPid = ANY(pg_blocking_pids(pid))
        """;

    [Fact]
    public async Task TenConcurrentTransfersOfThreeFromTenCompleteExactlyThreeAndNeverOversell()
    {
        (string product, string source, string destination, long _) = await ArrangeAsync(10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage[] responses = await FireAsync(client, Body(product, source, destination, 3), 10);

        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        HttpResponseMessage[] refused = responses.Where(response => response.StatusCode == HttpStatusCode.BadRequest).ToArray();
        Assert.Equal(7, refused.Length);
        foreach (HttpResponseMessage response in refused)
        {
            Assert.Equal("insufficient_stock", (await Problem.ReadAsync(response)).Code);
        }

        Assert.Equal(1, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(9, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(3, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task TwoConcurrentTransfersOfSevenFromTenCompleteOnceAndRefuseTheOtherWithAvailableThree()
    {
        (string product, string source, string destination, long _) = await ArrangeAsync(10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage[] responses = await FireAsync(client, Body(product, source, destination, 7), 2);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        HttpResponseMessage refused = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.BadRequest);
        Problem problem = await Problem.ReadAsync(refused);
        Assert.Equal("insufficient_stock", problem.Code);
        Assert.Contains("available 3", problem.Detail);
        Assert.Equal(3, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(1, await Seed.OrderCountAsync(api, product));
    }

    // Pins the guard's boundary: the second transfer takes exactly what is left.
    [Fact]
    public async Task TwoConcurrentTransfersOfFiveFromTenBothCompleteAndDrainTheSourceToZero()
    {
        (string product, string source, string destination, long _) = await ArrangeAsync(10);
        using HttpClient client = api.CreateClient();

        HttpResponseMessage[] responses = await FireAsync(client, Body(product, source, destination, 5), 2);

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        Assert.Equal(0, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(10, await Seed.QuantityAsync(api, product, destination));
        Assert.Equal(2, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task ATransferBlockedByAnUncommittedDecrementRechecksTheCommittedQuantityAndIsRefused()
    {
        (string product, string source, string destination, long sourceId) = await ArrangeAsync(10);
        using HttpClient client = api.CreateClient();
        await using NpgsqlConnection holder = new(api.ConnectionString);
        await holder.OpenAsync();
        await using NpgsqlTransaction transaction = await holder.BeginTransactionAsync();
        Assert.Equal(1, await holder.ExecuteAsync(TakeSql, new { quantity = 5, product, sourceId }, transaction));

        Task<HttpResponseMessage> transfer = client.PostAsJsonAsync("/orders", Body(product, source, destination, 7));
        await WaitUntilBlockedAsync(transfer, holder.ProcessID);
        await transaction.CommitAsync();
        HttpResponseMessage response = await transfer;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("insufficient_stock", problem.Code);
        Assert.Contains("available 5", problem.Detail);
        Assert.Equal(5, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    [Fact]
    public async Task ATransferWaitingLongerThanTheLockTimeoutIsRefusedWith503ConcurrencyConflictAndRetryAfter()
    {
        (string product, string source, string destination, long sourceId) = await ArrangeAsync(10);
        using HttpClient client = api.CreateClient();
        // Bounds the wait if the lock timeout ever stops firing.
        client.Timeout = TimeSpan.FromSeconds(30);
        await using NpgsqlConnection holder = new(api.ConnectionString);
        await holder.OpenAsync();
        NpgsqlTransaction transaction = await holder.BeginTransactionAsync();
        HttpResponseMessage response;
        try
        {
            Assert.Equal(1, await holder.ExecuteScalarAsync<int>(LockRowSql, new { product, sourceId }, transaction));
            response = await client.PostAsJsonAsync("/orders", Body(product, source, destination, 1));
        }
        finally
        {
            // Released only after the transfer answered, so the lock was held past the 3 s lock_timeout.
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
        }

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("concurrency_conflict", (await Problem.ReadAsync(response)).Code);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(10, await Seed.QuantityAsync(api, product, source));
        Assert.Equal(0, await Seed.OrderCountAsync(api, product));
    }

    // Polls until the transfer's decrement is seen waiting on the holder's lock; fails fast if it answered first.
    private async Task WaitUntilBlockedAsync(Task<HttpResponseMessage> transfer, int holderPid)
    {
        await using NpgsqlConnection observer = new(api.ConnectionString);
        await observer.OpenAsync();
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (await observer.ExecuteScalarAsync<int>(BlockedDecrementSql, new { holderPid }) == 0)
        {
            if (transfer.IsCompleted)
            {
                Assert.Fail("The transfer answered " + (await transfer).StatusCode + " before it was seen waiting on the row lock.");
            }

            Assert.True(DateTime.UtcNow < deadline, "The transfer was never seen waiting on the row lock.");
            await Task.WhenAny(transfer, Task.Delay(TimeSpan.FromMilliseconds(20)));
        }
    }

    // Every request waits on one gate, so they reach the server together.
    private static async Task<HttpResponseMessage[]> FireAsync(HttpClient client, object body, int count)
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<HttpResponseMessage>[] requests = Enumerable.Range(0, count)
            .Select(_ => Task.Run(async () =>
            {
                await gate.Task;
                return await client.PostAsJsonAsync("/orders", body);
            }))
            .ToArray();
        gate.SetResult();
        return await Task.WhenAll(requests);
    }

    // The source is created first, so it has the lower id and its row is touched first under either lock order.
    private async Task<(string Product, string Source, string Destination, long SourceId)> ArrangeAsync(int sourceQuantity)
    {
        string product = TestData.Unique("SKU");
        string source = TestData.Unique("WH");
        string destination = TestData.Unique("WH");
        await Seed.ProductAsync(api, product);
        long sourceId = await Seed.WarehouseAsync(api, source);
        long destinationId = await Seed.WarehouseAsync(api, destination);
        Assert.True(sourceId < destinationId);
        await Seed.StockAsync(api, product, source, sourceQuantity);
        return (product, source, destination, sourceId);
    }

    private static object Body(string product, string source, string destination, int quantity) => new
    {
        productCode = product,
        sourceWarehouseCode = source,
        destinationWarehouseCode = destination,
        quantity,
    };
}
