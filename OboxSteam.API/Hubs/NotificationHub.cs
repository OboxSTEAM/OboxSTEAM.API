using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OboxSteam.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
        }

        await base.OnConnectedAsync();
    }

    public Task JoinProgramSync(Guid programId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"program:{programId}");

    public Task LeaveProgramSync(Guid programId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"program:{programId}");
}
