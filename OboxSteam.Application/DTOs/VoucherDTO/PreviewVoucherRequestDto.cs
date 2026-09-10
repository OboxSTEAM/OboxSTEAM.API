using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.VoucherDTO;

public sealed class PreviewVoucherRequestDto
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid? BundleId { get; set; }

    public Guid? ProgramId { get; set; }
}
