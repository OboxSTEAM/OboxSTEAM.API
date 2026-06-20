using OboxSteam.Application.DTOs.ResearchMilestoneDTO;

namespace OboxSteam.Application.Interfaces;

/// <summary>
/// Mentor/admin milestone management and student unlock/progress views for research modules.
/// </summary>
public interface IResearchMilestoneService
{
    Task<ResearchMilestoneResponseDto> CreateMilestone(
        Guid moduleId,
        CreateResearchMilestoneRequestDto request);

    Task<ResearchMilestoneResponseDto?> GetMilestoneById(Guid milestoneId);

    Task<List<ResearchMilestoneResponseDto>> GetMilestonesByModule(Guid moduleId);

    Task<ResearchMilestoneResponseDto?> UpdateMilestone(
        Guid milestoneId,
        UpdateResearchMilestoneRequestDto request);

    Task<bool> DeleteMilestone(Guid milestoneId);

    Task<ResearchMilestoneActivityResponseDto> LinkActivity(
        Guid milestoneId,
        LinkMilestoneActivityRequestDto request);

    Task<ResearchMilestoneActivityResponseDto?> UpdateActivityLink(
        Guid milestoneId,
        Guid activityId,
        UpdateMilestoneActivityLinkRequestDto request);

    Task<bool> UnlinkActivity(Guid milestoneId, Guid activityId);

    Task<StudentMilestoneProgressDto> GetStudentMilestoneProgress(Guid moduleEnrollmentId);
}
