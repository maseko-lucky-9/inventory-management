using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Inventory.Api.Features.Auth;

/// <summary>A fixed window per client IP on POST /auth/login, against password guessing (FR-021).</summary>
public static class LoginRateLimit
{
    public const string Policy = "login";

    private const string PermitLimitSetting = "RateLimiting:LoginPermitLimit";
    private const int DefaultPermitLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddLoginRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        int permitLimit = configuration.GetValue(PermitLimitSetting, DefaultPermitLimit);
        return services.AddRateLimiter(options =>
        {
            // The status-code pages turn the bare 429 into Problem Details with code too_many_requests.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Behind a proxy every caller shares the proxy's address; forwarded headers would be needed there.
            options.AddPolicy(Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = Window }));
        });
    }
}
