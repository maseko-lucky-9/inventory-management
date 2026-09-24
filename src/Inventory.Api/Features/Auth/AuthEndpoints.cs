using Inventory.Api.Shared.Errors;
using Inventory.Api.Shared.Validation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/auth").WithTags("Auth");
        group.MapPost("/login", LoginAsync)
            .WithSummary("Log in and receive a bearer token.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(LoginRateLimit.Policy)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>();
        return app;
    }

    // The validator has run, so both fields are present. A wrong password and an unknown username
    // are the same refusal, and both pay for one hash check.
    private static async Task<Ok<IssuedToken>> LoginAsync(
        LoginRequest request, UserStore store, PasswordVerifier passwords, TokenIssuer issuer, CancellationToken cancellationToken)
    {
        User? user = await store.FindAsync(request.Username!, cancellationToken);
        return passwords.Verify(user, request.Password!)
            ? TypedResults.Ok(issuer.Issue(user.Id, user.Username))
            : throw new InvalidCredentialsException();
    }
}
