namespace OboxSteam.Application.DTOs.ProgramDTO;

public sealed class UpgradeProgramFrameworkVersionRequest
{
    /// <summary>A published version of the program's framework, newer than the pinned one.</summary>
    public Guid FrameworkVersionId { get; set; }
}
