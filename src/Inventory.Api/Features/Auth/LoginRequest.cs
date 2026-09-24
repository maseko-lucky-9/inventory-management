namespace Inventory.Api.Features.Auth;

// Nullable so a missing field reaches the validator as a field error, not a null dereference.
public sealed record LoginRequest(string? Username, string? Password);
