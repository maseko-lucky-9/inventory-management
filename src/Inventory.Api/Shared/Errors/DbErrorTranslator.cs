namespace Inventory.Api.Shared.Errors;

/// <summary>Maps a PostgreSQL SQLSTATE to a domain error; null means a defect, answered with 500.</summary>
public static class DbErrorTranslator
{
    public static DomainException? Translate(string? sqlState, string entity, string code) => sqlState switch
    {
        "23505" => new DuplicateCodeException(entity, code),
        "22003" => new QuantityOutOfRangeException(),
        // Deadlock, serialization failure, lock_timeout: safe for the client to retry.
        "40P01" or "40001" or "55P03" => new ConcurrencyConflictException(),
        // statement_timeout, too many connections, admin shutdown, connection failures, no server reply.
        "57014" or "53300" or "57P01" or null => new DatabaseUnavailableException(),
        _ when sqlState.StartsWith("08", StringComparison.Ordinal) => new DatabaseUnavailableException(),
        // 23514 (CHECK) and anything else is a defect: the guard should have refused first.
        _ => null,
    };
}
