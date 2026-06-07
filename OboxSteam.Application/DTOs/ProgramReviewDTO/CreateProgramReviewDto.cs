using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ProgramReviewDTO;

public class CreateProgramReviewDto
{
    /// <summary>Star rating from 1 to 5.</summary>
    [Range(1, 5, ErrorMessage = "StarRating must be between 1 and 5.")]
    public int StarRating { get; set; }

    [MaxLength(2000)]
    public string? Comment { get; set; }
}
