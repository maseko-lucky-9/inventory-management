namespace Inventory.Api.Shared.Errors;

/// <summary>An unknown (or unlinked) code in a path or query.</summary>
public sealed class NotFoundException(string entity, string code)
    : DomainException($"No {entity} with code '{code}'.", StatusCodes.Status404NotFound, $"{entity}_not_found");
