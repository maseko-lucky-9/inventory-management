namespace Inventory.Api.Shared.Auth;

/// <summary>Reads a secret from configuration. Blank, or the placeholder that .env.example commits, counts as unset.</summary>
public static class ConfiguredSecret
{
    public const string Placeholder = "<PLACEHOLDER>";

    public static string? Read(IConfiguration configuration, string key) =>
        configuration[key] is { } value && !string.IsNullOrWhiteSpace(value) && value != Placeholder ? value : null;
}
