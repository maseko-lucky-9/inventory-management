using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Shared.Auth;

/// <summary>Bearer tokens are required on every endpoint unless it is marked AllowAnonymous (ADR-006, FR-012).</summary>
public static class TokenAuthentication
{
    public static IServiceCollection AddTokenAuthentication(this IServiceCollection services)
    {
        services.AddSingleton<SigningKey>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        // ValidateOnStart builds these options, and so the signing key, while the host starts:
        // a missing production key stops startup instead of failing the first request.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<SigningKey>(Configure)
            .ValidateOnStart();
        // The fallback applies to every endpoint with no authorization metadata of its own, so a new route is protected by default.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    private static void Configure(JwtBearerOptions options, SigningKey key)
    {
        // Claims keep their JWT names (sub, unique_name) instead of being renamed to XML-schema URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TokenSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = TokenSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TokenSettings.ClockSkew,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key.Key,
            // Only the algorithm we sign with: a token claiming "none" or another algorithm is refused.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
        };
    }
}
