namespace OboxSteam.Domain.Entities;

/// <summary>
/// Join table for Parent-Student many-to-many relationship.
/// Composite key: (ParentId, StudentId)
/// </summary>
public class ParentStudent
{
    public Guid ParentId { get; set; }
    public User Parent { get; set; } = null!;

    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
}
