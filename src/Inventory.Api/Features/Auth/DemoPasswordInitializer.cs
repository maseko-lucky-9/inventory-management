using Inventory.Api.Shared.Auth;

namespace Inventory.Api.Features.Auth;

/// <summary>Hashes DemoUsers:Password into the seeded demo users at startup (ADR-008). Without one, they cannot log in.</summary>
public sealed class DemoPasswordInitializer(
    UserStore store, PasswordVerifier passwords, IConfiguration configuration, ILogger<DemoPasswordInitializer> logger)
    : IHostedService
{
    private const string Setting = "DemoUsers:Password";

    // The users db/seed.sql creates.
    private static readonly string[] DemoUsernames = ["alice", "bob", "carol"];

    // StartAsync runs after every hosted service's StartingAsync, so SchemaInitializer has already seeded the users.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? password = ConfiguredSecret.Read(configuration, Setting);
        if (password is null)
        {
            logger.LogWarning("DemoUsers__Password is not set; the demo users cannot log in.");
        }

        foreach (string username in DemoUsernames)
        {
            // A fresh salt per user. With no password the hash is cleared, so one set by an earlier run stops working.
            string? hash = password is null ? null : passwords.Hash(password);
            await store.SetPasswordHashAsync(username, hash, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
