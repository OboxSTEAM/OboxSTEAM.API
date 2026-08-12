namespace OboxSteam.Application.DTOs.ClassRedeliveryDTO;

public class DecideClassRedeliveryRequestDto
{
    /// <summary>Required when manager assigns a target class (no auto-match).</summary>
    public Guid? TargetClassId { get; set; }

    public string? DecisionNote { get; set; }
}
