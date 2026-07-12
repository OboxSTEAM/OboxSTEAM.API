namespace OboxSteam.Application.DTOs.RetrospectiveDTO;

public class SubmitRetrospectiveRequestDto
{
    /// <summary>
    /// Optional final plain text. When omitted, the last saved draft is submitted.
    /// </summary>
    public string? ContentText { get; set; }
}
