using Inventory.Api.Shared.Errors;

namespace Inventory.Api.Features.Orders;

/// <summary>Turns the store's outcome into the order, or into the refusal the client sees.</summary>
public sealed class TransferService(ITransferStore store)
{
    public async Task<TransferOrder> TransferAsync(TransferCommand command, CancellationToken cancellationToken)
    {
        TransferOutcome outcome = await store.ExecuteAsync(command, cancellationToken);
        return outcome switch
        {
            TransferOutcome.Completed completed => completed.Order,
            TransferOutcome.UnknownCode unknown => throw new UnknownCodeException(unknown.Entity, unknown.Code),
            TransferOutcome.Insufficient insufficient => throw new InsufficientStockException(
                command.ProductCode, command.SourceWarehouseCode, command.Quantity, insufficient.Available),
            _ => throw new InvalidOperationException("Unhandled transfer outcome " + outcome.GetType().Name + "."),
        };
    }
}
