namespace Inventory.Api.Shared.Errors;

/// <summary>Completes every Problem Details body (ADR-004). Status-code pages and framework rejections arrive with no code or detail.</summary>
public static class ErrorCodes
{
    // Shared with DomainExceptionHandler, so a bad body reads the same whether the framework threw or set a bare status.
    public const string MalformedRequestDetail = "The request body could not be read.";
    public const string UnsupportedMediaTypeDetail = "The request body must be JSON.";
    public const string InternalErrorDetail = "An unexpected error occurred.";

    /// <summary>Fills only what is missing, so a handler's own code and detail always win.</summary>
    public static void Complete(ProblemDetailsContext context)
    {
        int? status = context.ProblemDetails.Status;
        context.ProblemDetails.Extensions.TryAdd("code", ForStatus(status));
        context.ProblemDetails.Detail ??= DetailForStatus(status);
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    }

    public static string ForStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "malformed_request",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        StatusCodes.Status429TooManyRequests => "too_many_requests",
        StatusCodes.Status503ServiceUnavailable => "service_unavailable",
        _ => "internal_error",
    };

    public static string DetailForStatus(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => MalformedRequestDetail,
        StatusCodes.Status401Unauthorized => "A valid bearer token is required.",
        StatusCodes.Status403Forbidden => "This request is not allowed.",
        StatusCodes.Status404NotFound => "Nothing exists at this path.",
        StatusCodes.Status405MethodNotAllowed => "This path does not accept this method.",
        StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaTypeDetail,
        StatusCodes.Status429TooManyRequests => "Too many requests; try again later.",
        StatusCodes.Status503ServiceUnavailable => "The service is unavailable; try again later.",
        _ => InternalErrorDetail,
    };
}
