namespace Inventory.Api.Shared.Errors;

public sealed class DuplicateCodeException(string entity, string code)
    : DomainException($"A {entity} with code '{code}' already exists.", StatusCodes.Status409Conflict, $"duplicate_{entity}_code");
