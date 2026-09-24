using System.Security.Claims;
using Inventory.Api.Shared.Auth;
using Microsoft.AspNetCore.Http;

namespace Inventory.UnitTests;

public sealed class HttpCurrentUserTests
{
    [Fact]
    public void UserIdIsTheNumericSubClaimNotTheUsername()
    {
        HttpCurrentUser user = For(new Claim("sub", "42"), new Claim("unique_name", "7"));

        Assert.Equal(42L, user.UserId);
    }

    // A scoped query run as user 0 would quietly return nothing; a defect must fail loudly instead.
    [Theory]
    [InlineData(null)]
    [InlineData("alice")]
    [InlineData("-1")]
    public void AMissingOrNonNumericSubIsADefectNotAUserId(string? sub)
    {
        HttpCurrentUser user = sub is null ? For() : For(new Claim("sub", sub));

        Assert.Throws<InvalidOperationException>(() => user.UserId);
    }

    private static HttpCurrentUser For(params Claim[] claims) => new(new HttpContextAccessor
    {
        HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) },
    });
}
