using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

/// <summary>File shared in the program chat. <see cref="MessageId"/> stays null until the message is sent.</summary>
public sealed class ProgramAdvisoryDiscussionAttachment : BaseEntity
{
    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;
    public Guid? MessageId { get; set; }
    public ProgramAdvisoryDiscussionMessage? Message { get; set; }
    public Guid UploaderUserId { get; set; }
    public User UploaderUser { get; set; } = null!;
    [MaxLength(255)] public string FileName { get; set; } = null!;
    [MaxLength(150)] public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DiscussionAttachmentKind Kind { get; set; }
    [MaxLength(1024)] public string StorageKey { get; set; } = null!;
}
