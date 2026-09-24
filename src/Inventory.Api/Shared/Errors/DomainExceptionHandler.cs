using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Shared.Errors;

/// <summary>The one place an exception becomes an HTTP response (ADR-004).</summary>
public sealed class DomainExceptionHandler(IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string code, string detail) = Describe(exception);
        if (status >= StatusCodes.Status500InternalServerError)
        {
            // .NET 10 no longer logs exceptions a handler has handled, so 5xx are logged here.
            logger.LogError(exception, "Request failed with {Status} {Code}", status, code);
        }

        if (exception is ConcurrencyConflictException)
        {
            httpContext.Response.Headers.RetryAfter = "1";
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Detail = detail, Extensions = { ["code"] = code } },
        });
    }

    // No stack trace or exception text ever reaches the client.
    private static (int Status, string Code, string Detail) Describe(Exception exception) => exception switch
    {
        DomainException domain => (domain.Status, domain.Code, domain.Message),
        BadHttpRequestException { StatusCode: StatusCodes.Status415UnsupportedMediaType } =>
            (StatusCodes.Status415UnsupportedMediaType, "unsupported_media_type", "The request body must be JSON."),
        BadHttpRequestException bad => (bad.StatusCode, "malformed_request", "The request body could not be read."),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
    };
}
