namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class RecordAdvisoryDiscussionReadRequest
{
    public long LastDisplayedSequence { get; set; }
    public string? Cursor { get; set; }
}
