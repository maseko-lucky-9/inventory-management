using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Identity;

namespace Inventory.Api.Features.Auth;

/// <summary>Hashes and checks passwords with the framework's PBKDF2 hasher (ADR-006).</summary>
public sealed class PasswordVerifier
{
    // The framework hasher ignores the user argument; this stands in wherever there is no real row.
    private static readonly User Nobody = new(0, string.Empty, null);

    private readonly PasswordHasher<User> hasher;

    // Checked when the user is unknown or has no password, so every refusal costs one hash check
    // and the response time does not reveal whether a username exists.
    private readonly string dummyHash;

    public PasswordVerifier(PasswordHasher<User> hasher)
    {
        this.hasher = hasher;
        dummyHash = hasher.HashPassword(Nobody, Guid.NewGuid().ToString());
    }

    public string Hash(string password) => hasher.HashPassword(Nobody, password);

    public bool Verify([NotNullWhen(true)] User? user, string password)
    {
        if (user?.PasswordHash is not { } hash)
        {
            hasher.VerifyHashedPassword(Nobody, dummyHash, password);
            return false;
        }

        return hasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed;
    }
}
