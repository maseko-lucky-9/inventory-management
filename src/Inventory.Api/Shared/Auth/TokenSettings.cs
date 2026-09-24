namespace Inventory.Api.Shared.Auth;

/// <summary>What every token carries and how strictly it is checked (ADR-006). Fixed in code: none of it is a secret.</summary>
public static class TokenSettings
{
    public const string Issuer = "inventory-auth";
    public const string Audience = "inventory-api";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(60);

    // Tolerates small clock drift between machines; the framework default of five minutes would stretch every token.
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);
}
