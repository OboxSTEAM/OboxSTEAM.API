namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

/// <summary>
/// Live curriculum parents for a thread target so the manager editor can open the node.
/// Material and rubric targets resolve to the owning activity or assignment.
/// </summary>
public sealed class AdvisoryTargetPathDto
{
    public Guid? ModuleId { get; set; }

    public Guid? CourseId { get; set; }

    public Guid? ActivityId { get; set; }

    public Guid? AssignmentId { get; set; }
}
