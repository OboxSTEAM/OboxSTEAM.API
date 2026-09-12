using OboxSteam.Application.Exceptions;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Factory for throwing typed domain exceptions.
/// Prefer throwing these directly in service code for clarity.
/// </summary>
public static class ErrorHelper
{
    /// <summary>400 — Invalid request data or business rule violation.</summary>
    public static Exception BadRequest(string message = "Invalid request data.", string? errorCode = null)
        => new BadRequestException(message, errorCode);

    /// <summary>401 — User is not authenticated or token is invalid.</summary>
    public static Exception Unauthorized(string message = "Unauthorized.", string? errorCode = null)
        => new UnauthorizedException(message, errorCode);

    /// <summary>403 — User is authenticated but lacks permission.</summary>
    public static Exception Forbidden(string message = "Access denied.", string? errorCode = null)
        => new ForbiddenException(message, errorCode);

    /// <summary>404 — Entity or resource not found.</summary>
    public static Exception NotFound(string message = "Resource not found.", string? errorCode = null)
        => new NotFoundException(message, errorCode);

    /// <summary>409 — Duplicate or conflicting data (e.g. email already exists).</summary>
    public static Exception Conflict(string message = "A conflict occurred.", string? errorCode = null)
        => new ConflictException(message, errorCode);

    /// <summary>410 — Resource or API surface permanently removed.</summary>
    public static Exception Gone(string message = "This resource is no longer available.", string? errorCode = null)
        => new GoneException(message, errorCode);

    /// <summary>500 — Unexpected system error.</summary>
    public static Exception Internal(string message = "An internal server error occurred.", string? errorCode = null)
        => new InternalException(message, errorCode);
}