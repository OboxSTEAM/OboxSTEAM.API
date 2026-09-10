using OboxSteam.Application.Commons;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryBoardDto
{
    public Guid SubmissionId { get; set; }

    public Guid? PreviousSubmissionId { get; set; }

    public AdvisoryBoardProgramDto Program { get; set; } = new();

    public CurriculumReviewSnapshotBuilder.CurriculumSnapshotDocument Curriculum { get; set; } = new();

    public List<AdvisoryThreadPinDto> ThreadPins { get; set; } = [];

    public SubmissionChangesDto? ChangeSummary { get; set; }

    public List<FrameworkHighlightDto> FrameworkHighlights { get; set; } = [];
}

public sealed class AdvisoryBoardProgramDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Code { get; set; } = null!;

    public ProgramStatus Status { get; set; }

    public string? Description { get; set; }

    public string? SkillsGained { get; set; }

    public Guid? FrameworkVersionId { get; set; }
}

public sealed class AdvisoryThreadPinDto
{
    public Guid ThreadId { get; set; }

    public Guid? SubmissionId { get; set; }

    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public ProgramAdvisoryThreadType Type { get; set; }

    public ProgramAdvisoryThreadStatus Status { get; set; }

    public int MessageCount { get; set; }

    public string? AuthorName { get; set; }

    public string? LastMessagePreview { get; set; }

    public DateTime LastMessageAt { get; set; }

    public string TargetLabel { get; set; } = null!;
}

public sealed class AdvisoryThreadPinSummaryDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public int OpenRequired { get; set; }

    public int OpenSuggestions { get; set; }

    public int Total { get; set; }
}

public sealed class FrameworkHighlightDto
{
    public ProgramAdvisoryTargetType TargetType { get; set; }

    public Guid TargetId { get; set; }

    public string CheckCode { get; set; } = null!;

    public string Label { get; set; } = null!;

    public bool Passed { get; set; }
}
