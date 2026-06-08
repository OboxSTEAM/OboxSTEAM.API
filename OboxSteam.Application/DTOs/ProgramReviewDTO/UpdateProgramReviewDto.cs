using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramReviewDTO;

public class UpdateProgramReviewDto
{
    /// <summary>Optional updated star rating (1–5). Null means no change.</summary>
    [Range(1, 5, ErrorMessage = "StarRating must be between 1 and 5.")]
    public int? StarRating { get; set; }

    /// <summary>Optional updated comment. Null means no change.</summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }
}
