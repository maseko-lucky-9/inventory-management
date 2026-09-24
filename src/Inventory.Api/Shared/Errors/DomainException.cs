namespace Inventory.Api.Shared.Errors;

/// <summary>An expected refusal with its HTTP status and stable machine-readable code (ADR-004).</summary>
/// <remarks>The cause is the database error behind a translated refusal; it reaches the log, never the response.</remarks>
public abstract class DomainException(string message, int status, string code, Exception? cause = null)
    : Exception(message, cause)
{
    public int Status { get; } = status;

    public string Code { get; } = code;
}
