namespace Inventory.Api.Features.Orders;

/// <summary>The seam that lets TransferService be unit-tested without a database.</summary>
public interface ITransferStore
{
    Task<TransferOutcome> ExecuteAsync(TransferCommand command, CancellationToken cancellationToken);
}
