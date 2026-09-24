namespace Inventory.Api.Shared.Errors;

/// <summary>The code for a response that has none yet: status-code pages and framework rejections.</summary>
public static class ErrorCodes
{
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
}
