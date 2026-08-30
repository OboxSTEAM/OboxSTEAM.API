using OboxSteam.Application.DTOs.ClassSessionDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>JaaS meeting join/leave with server-side LiveOnline attendance.</summary>
public interface ISessionMeetingService
{
    Task<ClassSessionJoinResponseDto> JoinAsync(Guid classSessionId);

    Task<ClassSessionLeaveResponseDto> LeaveAsync(Guid classSessionId);
}
