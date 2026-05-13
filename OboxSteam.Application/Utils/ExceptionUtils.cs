using OboxSteam.Application.Exceptions;

namespace OboxSteam.Application.Utils;

public static class ExceptionUtils
{
    /// <summary>
    /// Extracts the HTTP status code from an exception.
    /// AppException subclasses carry their code directly; all others default to 500.
    /// </summary>
    public static int ExtractStatusCode(Exception ex)
        => ex is AppException appEx ? appEx.StatusCode : 500;

    public static string ExtractMessage(Exception ex)
        => ex.Message ?? "Lỗi không xác định.";

    public static ApiResult<T> CreateErrorResponse<T>(Exception ex)
    {
        var code = ExtractStatusCode(ex).ToString();
        var message = ExtractMessage(ex);
        return ApiResult<T>.Failure(code, message);
    }
}