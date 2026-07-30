using OboxSteam.Application.Commons;
using OboxSteam.Application.DTOs.AssignmentDTO;
using OboxSteam.Application.DTOs.AssignmentSubmissionDTO;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.Interfaces;

public interface IAssignmentService
{
    /// <summary>
    /// Get a paginated list of assignments with module/program context.
    /// Supports search (title, code, module, program), filter, and sort.
    /// </summary>
    Task<Pagination<AssignmentListItemDto>> GetAllAssignments(
        string? search,
        string? sortBy,
        bool isDescending,
        int page,
        int pageSize,
        Guid? moduleId = null,
        Guid? programId = null,
        Guid? courseId = null,
        AssignmentType? assignmentType = null);

    /// <summary>
    /// List submissions of an assignment made by students actively enrolled in a class.
    /// Mentors may only view classes they are assigned to.
    /// </summary>
    Task<List<AssignmentSubmissionListItemDto>> GetAssignmentSubmissions(Guid assignmentId, Guid classId);

    Task<AssignmentResponseDto> CreateAssignment(CreateAssignmentRequestDto request);
    Task<AssignmentResponseDto?> GetAssignmentById(Guid assignmentId);
    Task<AssignmentResponseDto?> UpdateAssignment(Guid assignmentId, UpdateAssignmentRequestDto request);
    Task<bool> DeleteAssignment(Guid assignmentId);
}
