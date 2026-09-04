using OboxSteam.Application.DTOs.ClassStudentProgressDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassStudentProgressService
{
    /// <summary>
    /// Roster-complete activity progress for the assigned mentor of the class.
    /// </summary>
    Task<ClassActivityStudentProgressDto> GetActivityStudentProgressAsync(Guid classId, Guid activityId);

    /// <summary>
    /// Roster-complete assignment progress for the assigned mentor of the class.
    /// </summary>
    Task<ClassAssignmentStudentProgressDto> GetAssignmentStudentProgressAsync(Guid classId, Guid assignmentId);
}
