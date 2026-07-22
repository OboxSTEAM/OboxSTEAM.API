using OboxSteam.Application.DTOs.ClassQuizQuestionSetDTO;

namespace OboxSteam.Application.Interfaces;

public interface IClassQuizQuestionSetService
{
    Task<ClassQuizQuestionSetResponseDto> PullAsync(Guid assignmentId, Guid classId);

    Task<ClassQuizQuestionSetResponseDto> GetAsync(Guid assignmentId, Guid classId);

    Task<ClassQuizQuestionResponseDto> UpdateQuestionAsync(
        Guid assignmentId,
        Guid classId,
        Guid questionId,
        UpdateClassQuizQuestionRequestDto request);
}
