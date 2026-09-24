using FluentValidation.Results;
using Inventory.Api.Features.Auth;

namespace Inventory.UnitTests;

public sealed class LoginValidatorTests
{
    private readonly LoginValidator validator = new();

    [Fact]
    public void AUsernameAndPasswordHaveNoErrors() =>
        Assert.Empty(validator.Validate(new LoginRequest("alice", "any password")).Errors);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AMissingOrBlankUsernameIsOneErrorOnUsername(string? username) =>
        AssertSingleError(new LoginRequest(username, "any password"), nameof(LoginRequest.Username));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AMissingOrBlankPasswordIsOneErrorOnPassword(string? password) =>
        AssertSingleError(new LoginRequest("alice", password), nameof(LoginRequest.Password));

    private void AssertSingleError(LoginRequest request, string property)
    {
        ValidationResult result = validator.Validate(request);

        ValidationFailure failure = Assert.Single(result.Errors);
        Assert.Equal(property, failure.PropertyName);
    }
}
