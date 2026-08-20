using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.ClassSessionDTO;

/// <summary>
/// Student self check-in for an Offline session — either the QR <see cref="Token"/>
/// (mobile scan) or the 6-digit fallback <see cref="Code"/> (web manual entry).
/// </summary>
public class ClassSessionCheckInRequestDto
{
    public Guid? Token { get; set; }

    [MaxLength(6)]
    public string? Code { get; set; }
}
