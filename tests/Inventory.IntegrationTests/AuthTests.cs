using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Inventory.Api.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.IntegrationTests;

/// <summary>ADR-006 authentication: login issues a bearer token; every other route but health and the API document needs one.</summary>
[Collection("api")]
public sealed partial class AuthTests(ApiFactory api)
{
    // The only routes a caller may reach without a token, as "METHOD pattern".
    private static readonly string[] AnonymousRoutes = ["GET /health", "GET /openapi/{documentName}.json", "POST /auth/login"];

    [Fact]
    public async Task DemoLoginReturnsAnHourLongBearerTokenForThatUserWhichOpensProtectedRoutes()
    {
        using HttpClient client = api.CreateAnonymousClient();
        DateTime before = DateTime.UtcNow;

        HttpResponseMessage response = await LoginAsync(client, "alice", api.DemoPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", body.GetProperty("tokenType").GetString());
        JsonWebToken token = new(body.GetProperty("accessToken").GetString());
        Assert.Equal("HS256", token.Alg);
        Assert.Equal((await Seed.UserIdAsync(api, "alice")).ToString(CultureInfo.InvariantCulture), token.Subject);
        Assert.Equal("alice", token.GetClaim("unique_name").Value);
        Assert.Equal("inventory-auth", token.Issuer);
        Assert.Equal(["inventory-api"], token.Audiences);
        // The token carries whole seconds, so issued-at may read up to a second before the request started.
        Assert.InRange(token.IssuedAt, before.AddSeconds(-1), DateTime.UtcNow);
        Assert.Equal(TimeSpan.FromMinutes(60), token.ValidTo - token.IssuedAt);
        Assert.Equal(token.ValidTo, body.GetProperty("expiresAt").GetDateTimeOffset().UtcDateTime);
        Assert.Equal(HttpStatusCode.OK, (await GetProductsWithAsync(client, token.EncodedToken)).StatusCode);
    }

    [Fact]
    public async Task AWrongPasswordAndAnUnknownUserGetTheSameInvalidCredentialsRefusal()
    {
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage wrongPassword = await LoginAsync(client, "alice", "not-" + api.DemoPassword);
        HttpResponseMessage unknownUser = await LoginAsync(client, TestData.Unique("nobody"), api.DemoPassword);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (wrongPassword.StatusCode, unknownUser.StatusCode));
        Problem wrong = await Problem.ReadAsync(wrongPassword);
        Problem unknown = await Problem.ReadAsync(unknownUser);
        Assert.Equal("invalid_credentials", wrong.Code);
        // Only the per-request trace id may differ, so the body cannot reveal whether a username exists.
        Assert.Equal(WithoutTraceId(wrong.Body), WithoutTraceId(unknown.Body));
    }

    [Theory]
    [InlineData("""{"password":"x"}""", "username")]
    [InlineData("""{"username":"alice"}""", "password")]
    public async Task ALoginMissingAFieldIsRefusedAsValidationFailedOnThatField(string json, string field)
    {
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage response = await client.PostAsync("/auth/login", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Problem problem = await Problem.ReadAsync(response);
        Assert.Equal("validation_failed", problem.Code);
        Assert.Equal([field], problem.Body.GetProperty("errors").EnumerateObject().Select(error => error.Name));
    }

    // Enumerated from the app's own endpoint list, so a route added later is covered without editing this test.
    [Fact]
    public async Task EveryRouteButLoginHealthAndTheApiDocumentAnswers401UnauthorizedWithoutAToken()
    {
        string[] routes = [.. Endpoints().SelectMany(Keys).Except(AnonymousRoutes)];
        using HttpClient client = api.CreateAnonymousClient();

        Assert.NotEmpty(routes);
        foreach (string route in routes)
        {
            string[] parts = route.Split(' ', 2);
            using HttpRequestMessage request = new(new HttpMethod(parts[0]), RouteParameter().Replace(parts[1], "probe"));

            HttpResponseMessage response = await client.SendAsync(request);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, route + " answered " + (int)response.StatusCode + " without a token.");
            Assert.Equal("unauthorized", (await Problem.ReadAsync(response)).Code);
        }
    }

    [Fact]
    public void OnlyLoginHealthAndTheApiDocumentAllowAnonymousCallers()
    {
        IEnumerable<string> anonymous = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(Keys);

        Assert.Equal(AnonymousRoutes.Order(StringComparer.Ordinal), anonymous.Order(StringComparer.Ordinal));
    }

    // The Compose health check and the API explorer call these without logging in.
    [Theory]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    public async Task HealthAndTheApiDocumentAnswerWithoutAToken(string path)
    {
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ATokenWithOneSignatureCharacterChangedIsRejected()
    {
        using HttpClient client = api.CreateAnonymousClient();
        string token = await DemoTokenAsync(client);
        // A middle character: all six of its bits are signature, unlike the last character's.
        int index = token.LastIndexOf('.') + 10;
        string tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        HttpResponseMessage original = await GetProductsWithAsync(client, token);
        HttpResponseMessage response = await GetProductsWithAsync(client, tampered);

        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await Problem.ReadAsync(response)).Code);
    }

    // Right key, issuer, audience and lifetime: only the algorithm is wrong.
    [Fact]
    public async Task ATokenSignedWithTheRightKeyButAnotherAlgorithmIsRejected()
    {
        SigningKey key = api.Services.GetRequiredService<SigningKey>();
        string token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenSettings.Issuer,
            Audience = TokenSettings.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = "1", ["unique_name"] = ApiFactory.FixtureUsername },
            SigningCredentials = new SigningCredentials(key.Key, SecurityAlgorithms.HmacSha384),
        });
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage response = await GetProductsWithAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await Problem.ReadAsync(response)).Code);
    }

    [Fact]
    public async Task ATokenThatExpiredAMinuteAgoIsRejected()
    {
        // A host whose clock runs 61 minutes behind issues a 60-minute token that expired a minute ago, past the 30-second skew.
        await using WebApplicationFactory<Program> past = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<TimeProvider>(new ShiftedClock(TimeSpan.FromMinutes(-61)))));
        using HttpClient pastClient = ApiFactory.WithoutToken(past.CreateClient());
        string expired = await DemoTokenAsync(pastClient);
        using HttpClient client = api.CreateAnonymousClient();

        HttpResponseMessage response = await GetProductsWithAsync(client, expired);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("unauthorized", (await Problem.ReadAsync(response)).Code);
    }

    // Its own database: this host's startup clears the demo users' password hashes.
    [Theory]
    [InlineData("")]
    [InlineData("<PLACEHOLDER>")]
    public async Task WithoutADemoPasswordTheDemoUsersCannotLogInAndStartupWarns(string setting)
    {
        LogCollector logs = new();
        string connectionString = await api.CreateEmptyDatabaseAsync();
        await using WebApplicationFactory<Program> host = api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Inventory", connectionString);
            builder.UseSetting("DemoUsers:Password", setting);
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());

        // The placeholder is public in .env.example, so it must never work as a password.
        HttpResponseMessage response = await LoginAsync(client, "alice", setting.Length > 0 ? setting : api.DemoPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("invalid_credentials", (await Problem.ReadAsync(response)).Code);
        Assert.Contains(logs.Warnings, warning => warning.Contains("DemoUsers__Password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WithALimitOfOneTheSecondLoginInTheWindowIsRefusedWith429TooManyRequests()
    {
        await using WebApplicationFactory<Program> host = api.WithWebHostBuilder(
            builder => builder.UseSetting("RateLimiting:LoginPermitLimit", "1"));
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());

        HttpResponseMessage first = await LoginAsync(client, "alice", api.DemoPassword);
        HttpResponseMessage second = await LoginAsync(client, "alice", api.DemoPassword);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.TooManyRequests), (first.StatusCode, second.StatusCode));
        Assert.Equal("too_many_requests", (await Problem.ReadAsync(second)).Code);
    }

    [Theory]
    [InlineData("<PLACEHOLDER>")]
    [InlineData("")]
    public void OutsideDevelopmentAMissingSigningKeyStopsStartupAndNamesTheFix(string setting)
    {
        using WebApplicationFactory<Program> host = api.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("Jwt:SigningKey", setting);
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.Server);

        // "not set", not "too short": the placeholder is recognised as missing, whatever its length.
        Assert.Contains("Jwt__SigningKey is not set", error.Message, StringComparison.Ordinal);
        Assert.Contains("openssl rand -base64 48", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void ASigningKeyShorterThan32BytesStopsStartupInEveryEnvironment(string environment)
    {
        // 16 hex characters: 16 bytes of UTF-8.
        string shortKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        using WebApplicationFactory<Program> host = api.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:SigningKey", shortKey);
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => host.Server);

        Assert.Contains("at least 32 bytes", error.Message, StringComparison.Ordinal);
        Assert.Contains("openssl rand -base64 48", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public async Task InDevelopmentAndTestAMissingSigningKeyBecomesARandomKeyWithAWarning(string environment)
    {
        LogCollector logs = new();
        await using WebApplicationFactory<Program> host = api.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:SigningKey", "<PLACEHOLDER>");
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
        });
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());
        using HttpClient shared = api.CreateAnonymousClient();
        string ownToken = await DemoTokenAsync(client);
        string sharedKeyToken = await DemoTokenAsync(shared);

        HttpResponseMessage own = await GetProductsWithAsync(client, ownToken);
        HttpResponseMessage foreign = await GetProductsWithAsync(client, sharedKeyToken);

        // The host signs and validates with its own key, so a token signed with the configured key does not open it.
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Unauthorized), (own.StatusCode, foreign.StatusCode));
        Assert.Contains(logs.Warnings, warning => warning.Contains("Jwt__SigningKey", StringComparison.Ordinal));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/auth/login", new { username, password });

    private async Task<string> DemoTokenAsync(HttpClient client)
    {
        HttpResponseMessage response = await LoginAsync(client, "alice", api.DemoPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("accessToken").GetString() ?? "";
    }

    private static async Task<HttpResponseMessage> GetProductsWithAsync(HttpClient client, string token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private static string WithoutTraceId(JsonElement body)
    {
        JsonObject copy = JsonNode.Parse(body.GetRawText())?.AsObject() ?? [];
        copy.Remove("traceId");
        return copy.ToJsonString();
    }

    private IEnumerable<RouteEndpoint> Endpoints() =>
        api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

    // A route without method metadata (the health check) answers any method; GET stands for it.
    private static IEnumerable<string> Keys(RouteEndpoint endpoint) =>
        (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
            .Select(method => method + " " + endpoint.RoutePattern.RawText);

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex RouteParameter();
}
