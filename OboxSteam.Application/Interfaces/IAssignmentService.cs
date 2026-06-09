using OboxSteam.Application.DTOs.AssignmentDTO;

namespace OboxSteam.Application.Interfaces;

public interface IAssignmentService
{
    Task<AssignmentResponseDto> CreateAssignment(CreateAssignmentRequestDto request);
    Task<AssignmentResponseDto?> GetAssignmentById(Guid assignmentId);
    Task<AssignmentResponseDto?> UpdateAssignment(Guid assignmentId, UpdateAssignmentRequestDto request);
    Task<bool> DeleteAssignment(Guid assignmentId);
}
