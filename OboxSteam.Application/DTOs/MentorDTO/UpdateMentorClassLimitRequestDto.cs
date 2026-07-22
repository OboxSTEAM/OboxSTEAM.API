using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MentorDTO;

public class UpdateMentorClassLimitRequestDto
{
    /// <summary>
    /// Per-mentor concurrent class cap. Null clears the override (system default applies).
    /// </summary>
    [Range(1, 50)]
    public int? MaxConcurrentClasses { get; set; }
}
