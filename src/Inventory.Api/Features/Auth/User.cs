namespace Inventory.Api.Features.Auth;

/// <summary>A row of users. The hash is null until a password is set, and a user without one cannot log in.</summary>
public sealed record User(long Id, string Username, string? PasswordHash);
