using OboxSteam.Application.DTOs.ClassCurriculumProgressDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassCurriculumProgressService
{
    /// <summary>
    /// Returns activity and assignment progress aggregates for a class cohort.
    /// Caller must be the assigned mentor of the class.
    /// </summary>
    Task<ClassCurriculumProgressDto> GetCurriculumProgressAsync(Guid classId);
}
