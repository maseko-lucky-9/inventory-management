namespace Inventory.IntegrationTests;

/// <summary>The system clock moved by a fixed offset, so a host can mint a token that is already expired.</summary>
public sealed class ShiftedClock(TimeSpan offset) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + offset;
}
