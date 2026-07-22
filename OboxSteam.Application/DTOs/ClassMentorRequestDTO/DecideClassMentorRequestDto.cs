using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassMentorRequestDTO;

public class DecideClassMentorRequestDto
{
    [MaxLength(1000)]
    public string? DecisionNote { get; set; }
}
