using System.Text.Json.Serialization;
using OboxSteam.Application.Utils;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryWorkflowTimelineDto
{
    public AdvisoryWorkflowStage CurrentStage { get; set; }

    public Guid? CurrentSubmissionId { get; set; }

    public AdvisoryResponsibleRole? ResponsibleRole { get; set; }

    public Guid? ResponsibleUserId { get; set; }

    public int OutstandingRequirementCount { get; set; }

    /// <summary>Submission number of the current or latest review round. Zero before the first submission.</summary>
    public int Round { get; set; }

    public AdvisoryNextActionDto NextAction { get; set; } = new();

    public List<AdvisoryWorkflowStageDto> Stages { get; set; } = [];
}

public sealed class AdvisoryWorkflowStageDto
{
    public string Key { get; set; } = null!;

    [JsonConverter(typeof(CamelCaseJsonStringEnumConverter))]
    public AdvisoryWorkflowStageState State { get; set; }

    public Guid? SubmissionId { get; set; }
}

public enum AdvisoryWorkflowStage
{
    Preparation,
    Review,
    Revision,
    AwaitingPublication,
    Published,
}

public enum AdvisoryWorkflowStageState
{
    Completed,
    Current,
    Upcoming,
    Skipped,
}

public enum AdvisoryResponsibleRole
{
    Manager,
    Advisor,
}
