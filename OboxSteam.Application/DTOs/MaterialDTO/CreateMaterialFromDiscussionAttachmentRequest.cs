using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Application.DTOs.MaterialDTO;

public sealed class CreateMaterialFromDiscussionAttachmentRequest
{
    /// <summary>A sent attachment of the program advisory chat.</summary>
    [Required]
    public Guid AttachmentId { get; set; }

    /// <summary>SelfPaced activity of the same program, without a material yet.</summary>
    [Required]
    public Guid ActivityId { get; set; }

    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;
}
