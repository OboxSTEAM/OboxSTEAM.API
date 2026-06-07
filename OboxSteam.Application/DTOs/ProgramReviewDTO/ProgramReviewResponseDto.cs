namespace OboxSteam.Application.DTOs.ProgramReviewDTO;

public class ProgramReviewResponseDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentAvatarUrl { get; set; }
    public int StarRating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
