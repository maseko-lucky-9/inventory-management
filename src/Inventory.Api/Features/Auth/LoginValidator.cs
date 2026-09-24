using FluentValidation;

namespace Inventory.Api.Features.Auth;

public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(request => request.Username).NotEmpty();
        RuleFor(request => request.Password).NotEmpty();
    }
}
