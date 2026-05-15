using OboxSteam.Application.Exceptions;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Utility helpers for converting exceptions into HTTP status codes
/// and standardised <see cref="ApiResult{T}"/> error payloads.
/// </summary>
public static class ExceptionUtils
{
    /// <summary>
    /// Extracts the appropriate HTTP status code from an exception.
    /// Uses <see cref="AppException.StatusCode"/> when available;
    /// falls back to well-known BCL types, then defaults to 500.
    /// </summary>
    public static int ExtractStatusCode(Exception ex) => ex switch
    {
        AppException appEx          => appEx.StatusCode,
        KeyNotFoundException        => 404,
        ArgumentException           => 400,
        UnauthorizedAccessException => 401,
        _                           => 500
    };

    /// <summary>
    /// Creates a typed <see cref="ApiResult{T}"/> failure payload from an exception.
    /// 500-level errors surface a generic message to avoid leaking internals.
    /// </summary>
    public static ApiResult<T> CreateErrorResponse<T>(Exception ex)
    {
        var statusCode = ExtractStatusCode(ex);
        var message    = statusCode >= 500
            ? "An unexpected error occurred. Please try again later."
            : ex.Message;

        return ApiResult<T>.Failure(statusCode.ToString(), message);
    }
}
