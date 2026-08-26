namespace OboxSteam.Application.DTOs.ClassRedeliveryDTO;

public sealed class OpenRemedialClassRequestDto
{
    public Guid ModuleId { get; set; }
    public Guid MentorId { get; set; }
    public DateTime StartDate { get; set; }
    public int? Capacity { get; set; }
}

public sealed class OpenRemedialClassResponseDto
{
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public string ClassName { get; set; } = null!;
    public int OfferedRequestCount { get; set; }
}

public sealed class RedeliveryWaitlistModuleGroupDto
{
    public Guid ModuleId { get; set; }
    public string ModuleCode { get; set; } = null!;
    public string ModuleName { get; set; } = null!;
    public int WaitingCount { get; set; }
    public int OldestWaitingDays { get; set; }
}

public sealed class RedeliveryWaitlistProgramGroupDto
{
    public Guid ProgramId { get; set; }
    public string ProgramCode { get; set; } = null!;
    public string ProgramName { get; set; } = null!;
    public List<RedeliveryWaitlistModuleGroupDto> Modules { get; set; } = [];
}
