using OboxSteam.Application.DTOs.EnrollmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IEnrollmentCurriculumService
{
    Task<EnrollmentCurriculumDto> GetEnrollmentCurriculumAsync(Guid programEnrollmentId);

    Task<CompleteActivityResponseDto> CompleteActivityAsync(
        Guid programEnrollmentId,
        Guid activityId,
        CompleteActivityRequestDto? request);

    Task EnsureActivityAccessibleAsync(Guid programEnrollmentId, Guid activityId);

    Task EnsureStudentEnrolledInProgramAsync(Guid programId);

    Task<SaveActivityCheckpointResponseDto> SaveActivityCheckpointAsync(
        Guid programEnrollmentId,
        Guid activityId,
        SaveActivityCheckpointRequestDto request);

    Task<ActivityLearningProgressDto?> GetActivityLearningProgressAsync(
        Guid programEnrollmentId,
        Guid activityId);
}
