namespace Inventory.Api.Shared.Errors;

/// <summary>Maps a PostgreSQL SQLSTATE to a domain error; null means a defect, answered with 500.</summary>
public static class DbErrorTranslator
{
    // The cause becomes the refusal's InnerException, so the 5xx log keeps the SQLSTATE and server message.
    public static DomainException? Translate(string? sqlState, string entity, string code, Exception? cause = null) => sqlState switch
    {
        "23505" => new DuplicateCodeException(entity, code, cause),
        "22003" => new QuantityOutOfRangeException(cause),
        // Deadlock, serialization failure, lock_timeout: safe for the client to retry.
        "40P01" or "40001" or "55P03" => new ConcurrencyConflictException(cause),
        // statement_timeout, too many connections, admin shutdown, connection failures, no server reply.
        "57014" or "53300" or "57P01" or null => new DatabaseUnavailableException(cause),
        _ when sqlState.StartsWith("08", StringComparison.Ordinal) => new DatabaseUnavailableException(cause),
        // 23514 (CHECK) and anything else is a defect: the guard should have refused first.
        _ => null,
    };
}
