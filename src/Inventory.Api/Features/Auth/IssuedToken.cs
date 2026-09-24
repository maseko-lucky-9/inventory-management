namespace Inventory.Api.Features.Auth;

/// <summary>The login response. ExpiresAt equals the token's exp claim.</summary>
public sealed record IssuedToken(string AccessToken, string TokenType, DateTime ExpiresAt);
