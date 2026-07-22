using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Application.DTOs.ClassMentorRequestDTO;

public class CreateClassMentorRequestDto
{
    [Required]
    public Guid ClassId { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }
}
