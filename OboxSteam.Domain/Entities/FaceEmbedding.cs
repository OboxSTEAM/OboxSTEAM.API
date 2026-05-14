using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Face embedding for AI-based student recognition (AWS Rekognition + pgvector).
/// 1:1 with User (student).
/// </summary>
public class FaceEmbedding : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    [MaxLength(255)]
    public string AwsFaceId { get; set; } = null!;

    // Note: For pgvector, configure column type "vector(128)" in Fluent API
    public string? Embedding { get; set; }

    public string? SourceImageUrl { get; set; }
}
