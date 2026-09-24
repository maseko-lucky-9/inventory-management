namespace Inventory.Api.Shared.Errors;

/// <summary>A wrong password and an unknown username are one refusal, so the response cannot reveal which usernames exist.</summary>
public sealed class InvalidCredentialsException()
    : DomainException("The username or password is incorrect.", StatusCodes.Status401Unauthorized, "invalid_credentials");
