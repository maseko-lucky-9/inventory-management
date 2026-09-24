namespace Inventory.Api.Shared.Errors;

/// <summary>An unknown (or unlinked) code in a request body.</summary>
public sealed class UnknownCodeException(string entity, string code)
    : DomainException($"Unknown {entity} code '{code}'.", StatusCodes.Status400BadRequest, $"unknown_{entity}_code");
