using System.Globalization;
using Inventory.Api.Shared.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Inventory.Api.Features.Auth;

/// <summary>Signs a 60-minute HS256 token naming the user (ADR-006). JwtBearer validates it against the same settings.</summary>
public sealed class TokenIssuer(SigningKey key, JsonWebTokenHandler handler, TimeProvider clock)
{
    public IssuedToken Issue(long userId, string username)
    {
        // Whole seconds, as the token stores them, so expiresAt equals the exp claim exactly.
        DateTime issuedAt = DateTimeOffset.FromUnixTimeSeconds(clock.GetUtcNow().ToUnixTimeSeconds()).UtcDateTime;
        DateTime expiresAt = issuedAt + TokenSettings.Lifetime;
        string token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TokenSettings.Issuer,
            Audience = TokenSettings.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(CultureInfo.InvariantCulture),
                [JwtRegisteredClaimNames.UniqueName] = username,
            },
            SigningCredentials = key.Credentials,
        });
        return new IssuedToken(token, "Bearer", expiresAt);
    }
}
