namespace OboxSteam.Application.DTOs.ProgramFrameworkDTO;

public sealed class SaveFrameworkRubricRequest
{
    public IReadOnlyList<FrameworkRubricCriterionRequest> Criteria { get; set; } = [];
}
