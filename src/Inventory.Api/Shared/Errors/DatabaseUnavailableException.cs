namespace Inventory.Api.Shared.Errors;

public sealed class DatabaseUnavailableException(Exception? cause = null)
    : DomainException("The database is unavailable; retry shortly.", StatusCodes.Status503ServiceUnavailable, "database_unavailable", cause);
