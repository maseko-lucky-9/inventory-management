using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Shared.Auth;

/// <summary>The HMAC key that signs and validates every token (ADR-006), resolved once while the host starts.</summary>
public sealed class SigningKey
{
    private const string Setting = "Jwt:SigningKey";
    private const string TestEnvironment = "Test";

    // HS256 needs a key at least as long as its 256-bit output.
    private const int MinimumBytes = 32;
    private const string Fix = "Generate one with `openssl rand -base64 48` and set Jwt__SigningKey in .env.";

    public SigningKey(IConfiguration configuration, IHostEnvironment environment, ILogger<SigningKey> logger)
    {
        Key = new SymmetricSecurityKey(Resolve(ConfiguredSecret.Read(configuration, Setting), environment, logger));
        Credentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256);
    }

    public SymmetricSecurityKey Key { get; }

    public SigningCredentials Credentials { get; }

    // The key's value is never logged or put in an error message.
    private static byte[] Resolve(string? configured, IHostEnvironment environment, ILogger logger)
    {
        if (configured is null)
        {
            if (!environment.IsDevelopment() && !environment.IsEnvironment(TestEnvironment))
            {
                throw new InvalidOperationException("Jwt__SigningKey is not set. " + Fix);
            }

            logger.LogWarning("Jwt__SigningKey is not set; using a random per-process key, so tokens stop working after a restart.");
            return RandomNumberGenerator.GetBytes(64);
        }

        byte[] key = Encoding.UTF8.GetBytes(configured);
        return key.Length >= MinimumBytes
            ? key
            : throw new InvalidOperationException("Jwt__SigningKey must be at least 32 bytes. " + Fix);
    }
}
