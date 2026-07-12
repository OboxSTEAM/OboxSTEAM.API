namespace OboxSteam.Application.DTOs.RetrospectiveDTO;

public class SaveRetrospectiveDraftRequestDto
{
    /// <summary>Plain-text reflection draft. Empty string clears the draft.</summary>
    public string? ContentText { get; set; }
}
