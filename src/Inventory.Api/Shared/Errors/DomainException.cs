namespace Inventory.Api.Shared.Errors;

/// <summary>An expected refusal with its HTTP status and stable machine-readable code (ADR-004).</summary>
public abstract class DomainException(string message, int status, string code) : Exception(message)
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}
