using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OboxSteam.Application.Commons;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdvisoryPresenceTracker _presenceTracker;

    public NotificationHub(IUnitOfWork unitOfWork, IAdvisoryPresenceTracker presenceTracker)
    {
        _unitOfWork = unitOfWork;
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = CurrentUserId();
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

    /// <summary>Joins the advisory workspace group of a program; only advisory participants may join.</summary>
    public async Task JoinAdvisorySync(Guid programId)
    {
        var userId = Guid.TryParse(CurrentUserId(), out var parsed) ? parsed : Guid.Empty;
        if (!await AdvisoryParticipantAccess.IsParticipantAsync(_unitOfWork, userId, programId))
        {
            throw new HubException("You are not a member of this program advisory team.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"advisory:{programId}");
        _presenceTracker.Join(programId, userId, Context.ConnectionId);
    }

    public Task LeaveAdvisorySync(Guid programId)
    {
        _presenceTracker.Leave(programId, Context.ConnectionId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"advisory:{programId}");
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _presenceTracker.Disconnect(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private string? CurrentUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? Context.User?.FindFirst("sub")?.Value;
}
