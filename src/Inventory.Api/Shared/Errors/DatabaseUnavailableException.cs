namespace Inventory.Api.Shared.Errors;

public sealed class DatabaseUnavailableException()
    : DomainException("The database is unavailable; retry shortly.", StatusCodes.Status503ServiceUnavailable, "database_unavailable");
