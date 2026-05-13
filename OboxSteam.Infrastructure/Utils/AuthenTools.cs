using System.Security.Claims;
using Microsoft.Extensions.Logging;

#pragma warning disable CS8603

namespace OboxSteam.Infrastructure.Utils;

public static class AuthenTools
{
    public static string? GetCurrentUserId(ClaimsIdentity? identity, ILogger? logger = null)
    {
        if (identity == null)
            return null;

        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        logger?.LogInformation("Extracted UserId from claims: {UserId}", userId);
        return userId;
    }
}

