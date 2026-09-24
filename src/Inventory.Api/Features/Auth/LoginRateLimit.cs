using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using IPNetwork = System.Net.IPNetwork;

namespace Inventory.Api.Features.Auth;

/// <summary>A fixed window per client IP on POST /auth/login, against password guessing (FR-021).</summary>
public static class LoginRateLimit
{
    public const string Policy = "login";

    private const string PermitLimitSetting = "RateLimiting:LoginPermitLimit";
    private const string KnownNetworksSetting = "ForwardedHeaders:KnownNetworks";
    private const int DefaultPermitLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        int permitLimit = configuration.GetValue(PermitLimitSetting, DefaultPermitLimit);
        services.AddTrustedProxies(configuration);
        return services.AddRateLimiter(options =>
        {
            // The status-code pages turn the bare 429 into Problem Details with code too_many_requests.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // The client's address, once UseForwardedHeaders has taken it from a trusted proxy's X-Forwarded-For.
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window }));
        });
    }

    // Behind a proxy every caller would share the proxy's address, and so one window. X-Forwarded-For is honoured
    // only from loopback (the framework default) and the networks in ForwardedHeaders__KnownNetworks
    // (comma-separated CIDR), so a direct caller cannot pick its own window. ForwardLimit stays at one hop.
    private static void AddTrustedProxies(this IServiceCollection services, IConfiguration configuration)
    {
        string[] networks = (configuration[KnownNetworksSetting] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        // Parsed here, so a mistyped network stops startup instead of silently trusting nothing.
        IPNetwork[] parsed = [.. networks.Select(network => IPNetwork.Parse(network))];
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            foreach (IPNetwork network in parsed)
            {
                options.KnownIPNetworks.Add(network);
            }
        });
    }
}
