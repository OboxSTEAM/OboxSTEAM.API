using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassSessionExpertDTO;

public sealed class SubmitClassSessionExpertFeedbackDto
{
    [Required(ErrorMessage = "Comment is required.")]
    [MaxLength(4000)]
    public string Comment { get; set; } = null!;

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }
}
