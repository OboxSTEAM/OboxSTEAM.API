namespace OboxSteam.Application.Interfaces;

public interface IClaimsService
{
    Guid GetCurrentUserId { get; }
    string? IpAddress { get; }
}
