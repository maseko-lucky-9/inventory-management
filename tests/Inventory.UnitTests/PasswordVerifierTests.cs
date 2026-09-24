using Inventory.Api.Features.Auth;
using Microsoft.AspNetCore.Identity;

namespace Inventory.UnitTests;

public sealed class PasswordVerifierTests
{
    private readonly CountingHasher hasher = new();

    // Timing is not asserted directly; the proof is that a refusal does the same hash work as a wrong password.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnUnknownUserOrOneWithoutAPasswordIsRefusedAfterOneHashCheck(bool userExists)
    {
        PasswordVerifier verifier = new(hasher);
        User? user = userExists ? new User(3, "carol", null) : null;

        bool verified = verifier.Verify(user, "any password");

        Assert.False(verified);
        Assert.Equal(1, hasher.Verifications);
    }

    [Fact]
    public void OnlyThePasswordThatWasHashedVerifies()
    {
        PasswordVerifier verifier = new(hasher);
        User user = new(1, "alice", verifier.Hash("right password"));

        Assert.Equal((true, false), (verifier.Verify(user, "right password"), verifier.Verify(user, "wrong password")));
    }

    private sealed class CountingHasher : PasswordHasher<User>
    {
        public int Verifications { get; private set; }

        public override PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
        {
            Verifications++;
            return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }
}
