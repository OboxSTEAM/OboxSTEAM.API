using System.Text.Json;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ProgramAdvisoryDTO;

public sealed class AdvisoryDiscussionSystemEventDto
{
    public DiscussionSystemEventCode Code { get; set; }
    public JsonElement? Payload { get; set; }
}
