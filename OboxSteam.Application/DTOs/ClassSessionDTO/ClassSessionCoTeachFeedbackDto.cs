namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Private expert-to-mentor feedback. Populate only for Mentor (class owner), Manager, and Admin.
/// </summary>
public sealed class ClassSessionCoTeachFeedbackDto
{
    public string Comment { get; set; } = null!;
    public int Rating { get; set; }
    public DateTime FeedbackAt { get; set; }
}
