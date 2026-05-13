using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Enums;
using System.ComponentModel.DataAnnotations;
namespace OboxSteam.Domain.Entities;
public class OtpStorage : BaseEntity
{
    [Required]
    [MaxLength(255)]
    public string Target { get; set; } = null!;

    [Required]
    [MaxLength(10)]
    public string OtpCode { get; set; } = null!;

    public DateTime ExpiredAt { get; set; }

    public bool IsUsed { get; set; }

    public OtpPurpose Purpose { get; set; }
}
