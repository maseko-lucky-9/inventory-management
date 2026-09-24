using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Inventory.IntegrationTests;

/// <summary>FR-021 behind a proxy: the login window follows the client a trusted proxy names, and nobody else's claim.</summary>
[Collection("api")]
public sealed class LoginRateLimitTests(ApiFactory api)
{
    private const string RemoteAddressHeader = "X-Test-Remote-Address";
    private const string ClientA = "198.51.100.1";
    private const string ClientB = "198.51.100.2";

    [Fact]
    public async Task ClientsBehindALoopbackProxyEachGetTheirOwnLoginWindow()
    {
        await using WebApplicationFactory<Program> host = HostWithLimitOfOne();
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());

        HttpStatusCode first = await LoginAsync(client, "127.0.0.1", ClientA);
        HttpStatusCode other = await LoginAsync(client, "127.0.0.1", ClientB);
        HttpStatusCode again = await LoginAsync(client, "127.0.0.1", ClientA);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests), (first, other, again));
    }

    // The header is the caller's own claim unless a trusted proxy wrote it.
    [Fact]
    public async Task AForwardedForHeaderFromAnUntrustedAddressDoesNotEscapeTheWindow()
    {
        await using WebApplicationFactory<Program> host = HostWithLimitOfOne();
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());

        HttpStatusCode first = await LoginAsync(client, "203.0.113.7", ClientA);
        HttpStatusCode spoofed = await LoginAsync(client, "203.0.113.7", ClientB);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.TooManyRequests), (first, spoofed));
    }

    // A proxy in its own container (Compose, a load balancer) is not loopback, so its network has to be named.
    // A dual-stack socket reports the same IPv4 proxy as an IPv4-mapped IPv6 address.
    [Theory]
    [InlineData("10.9.4.2")]
    [InlineData("::ffff:10.9.4.2")]
    public async Task AProxyInANetworkNamedInSettingsIsTrusted(string proxyAddress)
    {
        await using WebApplicationFactory<Program> host = HostWithLimitOfOne(
            builder => builder.UseSetting("ForwardedHeaders:KnownNetworks", "192.0.2.0/24, 10.9.0.0/16"));
        using HttpClient client = ApiFactory.WithoutToken(host.CreateClient());

        HttpStatusCode first = await LoginAsync(client, proxyAddress, ClientA);
        HttpStatusCode other = await LoginAsync(client, proxyAddress, ClientB);
        HttpStatusCode again = await LoginAsync(client, proxyAddress, ClientA);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests), (first, other, again));
    }

    // A typo must not quietly leave every client behind the proxy in one window.
    [Fact]
    public void AMistypedTrustedNetworkStopsStartup()
    {
        using WebApplicationFactory<Program> host = api.WithWebHostBuilder(
            builder => builder.UseSetting("ForwardedHeaders:KnownNetworks", "10.9.0.0/99"));

        Assert.Throws<FormatException>(() => host.Server);
    }

    private WebApplicationFactory<Program> HostWithLimitOfOne(Action<IWebHostBuilder>? configure = null) =>
        api.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:LoginPermitLimit", "1");
            builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, RemoteAddressFromHeader>());
            configure?.Invoke(builder);
        });

    private async Task<HttpStatusCode> LoginAsync(HttpClient client, string remoteAddress, string forwardedFor)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new { username = "alice", password = api.DemoPassword }),
        };
        request.Headers.Add(RemoteAddressHeader, remoteAddress);
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        HttpResponseMessage response = await client.SendAsync(request);
        return response.StatusCode;
    }

    // The test server has no socket, so the connection's address comes from a header only these tests send.
    // A startup filter runs before the app's own pipeline, as the network would.
    private sealed class RemoteAddressFromHeader : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[RemoteAddressHeader].ToString());
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}
