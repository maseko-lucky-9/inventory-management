using System.Globalization;
using System.Net;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Inventory.IntegrationTests;

/// <summary>The harness signs clients in; the scoping tests rely on each client being exactly the user it names.</summary>
[Collection("api")]
public sealed class ApiFactoryTests(ApiFactory api)
{
    [Fact]
    public async Task EveryDefaultClientIsSignedInAsTheFixtureUser()
    {
        using HttpClient client = api.CreateClient();

        JsonWebToken token = new(client.DefaultRequestHeaders.Authorization?.Parameter);

        Assert.Equal(ApiFactory.FixtureUsername, token.GetClaim("unique_name").Value);
        Assert.Equal(await IdOfAsync(ApiFactory.FixtureUsername), token.Subject);
    }

    [Fact]
    public async Task CreateClientForSignsInAsThatUserAndCreatesTheRowIfMissing()
    {
        string username = TestData.Unique("user");

        using HttpClient client = api.CreateClientFor(username);

        JsonWebToken token = new(client.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.Equal(username, token.GetClaim("unique_name").Value);
        Assert.Equal(await IdOfAsync(username), token.Subject);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/products")).StatusCode);
    }

    // Zero, never a real id, when the row is missing.
    private async Task<string> IdOfAsync(string username) =>
        (await Seed.UserIdAsync(api, username)).ToString(CultureInfo.InvariantCulture);
}
