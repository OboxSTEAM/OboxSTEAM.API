namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class RecordAdvisoryThreadReadRequest
{
    public long LastDisplayedSequence { get; set; }
    public string? Cursor { get; set; }
}
