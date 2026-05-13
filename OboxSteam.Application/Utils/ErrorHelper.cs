using OboxSteam.Application.Exceptions;

namespace OboxSteam.Application.Utils;

/// <summary>
/// Factory for throwing typed domain exceptions.
/// Prefer throwing these directly in service code for clarity.
/// </summary>
public static class ErrorHelper
{
    /// <summary>400 — Invalid request data or business rule violation.</summary>
    public static Exception BadRequest(string message = "Invalid request data.")
        => new BadRequestException(message);

    /// <summary>401 — User is not authenticated or token is invalid.</summary>
    public static Exception Unauthorized(string message = "Unauthorized.")
        => new UnauthorizedException(message);

    /// <summary>403 — User is authenticated but lacks permission.</summary>
    public static Exception Forbidden(string message = "Access denied.")
        => new ForbiddenException(message);

    /// <summary>404 — Entity or resource not found.</summary>
    public static Exception NotFound(string message = "Resource not found.")
        => new NotFoundException(message);

    /// <summary>409 — Duplicate or conflicting data (e.g. email already exists).</summary>
    public static Exception Conflict(string message = "A conflict occurred.")
        => new ConflictException(message);

    /// <summary>500 — Unexpected system error.</summary>
    public static Exception Internal(string message = "An internal server error occurred.")
        => new InternalException(message);
}