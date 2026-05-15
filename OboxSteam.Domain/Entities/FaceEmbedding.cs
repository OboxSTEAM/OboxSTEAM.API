using System.ComponentModel.DataAnnotations;

namespace OboxSteam.Domain.Entities;

/// <summary>
/// Maps a student to their indexed face in AWS Rekognition Collection.
/// 1:1 with User (student). Rekognition stores the actual face vectors on AWS cloud.
/// </summary>
public class FaceEmbedding : BaseEntity
{
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;

    [MaxLength(255)]
    public string AwsFaceId { get; set; } = null!;
}

