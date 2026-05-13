using Microsoft.AspNetCore.Http;
using OboxSteam.Application.Interfaces;
using System.Security.Claims;

namespace OboxSteam.Application.Commons;

public class ClaimsService : IClaimsService
{
    public ClaimsService(IHttpContextAccessor httpContextAccessor)
    {
        var identity = httpContextAccessor.HttpContext?.User?.Identity as ClaimsIdentity;

        var extractedId = GetCurrentUserIdFromIdentity(identity);
        GetCurrentUserId = Guid.TryParse(extractedId, out var parsedId) ? parsedId : Guid.Empty;

        IpAddress = httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    public Guid GetCurrentUserId { get; }
    public string? IpAddress { get; }

    private static string? GetCurrentUserIdFromIdentity(ClaimsIdentity? identity)
    {
        if (identity == null) return null;
        var userId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userId;
    }
}
