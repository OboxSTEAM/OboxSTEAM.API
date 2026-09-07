namespace OboxSteam.Application.Exceptions;

/// <summary>
/// Base class for all application domain exceptions.
/// Carries an HTTP status code so the GlobalExceptionMiddleware can respond correctly
/// without any magic string lookups in Exception.Data.
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public AppException(int statusCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

// ─── Concrete typed exceptions ──────────────────────────────────────────────

/// <summary>400 — Invalid request data or business rule violation.</summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message = "Invalid request data.") : base(400, message) { }
}

/// <summary>401 — User is not authenticated.</summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized.") : base(401, message) { }
}

/// <summary>403 — User is authenticated but does not have permission.</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Access denied.") : base(403, message) { }
}

/// <summary>404 — Requested resource does not exist.</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message = "Resource not found.") : base(404, message) { }
}

/// <summary>409 — Duplicate or conflicting data.</summary>
public class ConflictException : AppException
{
    public ConflictException(string message = "A conflict occurred.") : base(409, message) { }
}

/// <summary>410 — Resource or API surface permanently removed.</summary>
public class GoneException : AppException
{
    public GoneException(string message = "This resource is no longer available.") : base(410, message) { }
}

/// <summary>500 — Unexpected system error.</summary>
public class InternalException : AppException
{
    public InternalException(string message = "An internal server error occurred.") : base(500, message) { }
    public InternalException(string message, Exception inner) : base(500, message, inner) { }
}
