namespace Inventory.Api.Shared.Errors;

/// <summary>A lock wait timed out or a deadlock was broken; the client may retry.</summary>
public sealed class ConcurrencyConflictException(Exception? cause = null)
    : DomainException("The stock is busy; retry shortly.", StatusCodes.Status503ServiceUnavailable, "concurrency_conflict", cause);
