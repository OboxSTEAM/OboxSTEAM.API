using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Utils;
using System.Text.Json;

namespace OboxSteam.API.Middlewares;

// Catch all unhandled exceptions in the API
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            LogException(ex);
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Business exceptions (4xx) just Warning — behavior, not system error.
    /// Server errors (5xx) log Error with full stack trace.
    /// </summary>
    private void LogException(Exception ex)
    {
        var isClientError = ex is AppException appEx && appEx.StatusCode < 500
                            || ex is KeyNotFoundException
                            || ex is ArgumentException;

        if (isClientError)
            _logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
        else
            _logger.LogError(ex, "An unhandled server error occurred.");
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // AppException subclasses carry their own status code
        // Fall back to BCL exception types for backward compatibility
        var statusCode = exception switch
        {
            AppException appEx => appEx.StatusCode,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;

        var response = ApiResult<object>.Failure(
            statusCode.ToString(),
            statusCode == 500 ? "An unexpected error occurred." : exception.Message
        );

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
