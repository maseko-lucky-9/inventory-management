namespace Inventory.Api.Features.Orders;

/// <summary>What the store decided; the service turns refusals into domain exceptions.</summary>
public abstract record TransferOutcome
{
    // Closed hierarchy: only the nested outcomes below exist.
    private TransferOutcome()
    {
    }

    public sealed record Completed(TransferOrder Order) : TransferOutcome;

    public sealed record UnknownCode(string Entity, string Code) : TransferOutcome;

    public sealed record Insufficient(int Available) : TransferOutcome;
}
