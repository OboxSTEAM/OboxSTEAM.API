using OboxSteam.Application.Exceptions;
using OboxSteam.Application.Utils;
using OboxSteam.Infrastructure.Observability;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var statusCode = ResolveStatusCode(ex);

            // The exception never reaches the ASP.NET Core instrumentation, so record it on the
            // request span explicitly; 4xx business errors are expected and must not become Issues.
            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                TelemetrySources.RecordException(Activity.Current, ex);
            }

            await HandleExceptionAsync(context, ex, statusCode);
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

    // AppException subclasses carry their own status code
    // Fall back to BCL exception types for backward compatibility
    private static int ResolveStatusCode(Exception exception) => exception switch
    {
        AppException appEx => appEx.StatusCode,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        ArgumentException => StatusCodes.Status400BadRequest,
        UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status500InternalServerError
    };

    private static Task HandleExceptionAsync(HttpContext context, Exception exception, int statusCode)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var errorCode = exception is AppException appException && !string.IsNullOrWhiteSpace(appException.ErrorCode)
            ? appException.ErrorCode
            : statusCode.ToString();

        var message = statusCode == 500 ? "An unexpected error occurred." : exception.Message;
        var response = ApiResult<object>.Failure(errorCode, message);
        if (exception is AppException { Payload: { } payload })
        {
            response.Value = new ResponseDataContent<object> { Code = errorCode, Message = message, Data = payload };
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}
