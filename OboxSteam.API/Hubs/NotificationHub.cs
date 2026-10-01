using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OboxSteam.Application.Commons;
using OboxSteam.Domain.Interfaces;

namespace OboxSteam.API.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationHub(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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
    }

    public Task LeaveAdvisorySync(Guid programId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"advisory:{programId}");

    private string? CurrentUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? Context.User?.FindFirst("sub")?.Value;
}
