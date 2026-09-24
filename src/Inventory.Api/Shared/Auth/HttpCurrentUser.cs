using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Inventory.Api.Shared.Auth;

/// <summary>Reads the caller's id from the validated token's sub claim; claims keep their JWT names (MapInboundClaims is off).</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Every scoped route requires a token, so a missing or non-numeric sub is a defect (500), never user 0.
    public long UserId =>
        long.TryParse(accessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub), NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            ? id
            : throw new InvalidOperationException("No authenticated user id: the request's token carries no numeric sub claim.");
}
