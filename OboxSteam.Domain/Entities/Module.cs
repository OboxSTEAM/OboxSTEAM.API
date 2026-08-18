using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Module : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!;

    public Guid ProgramId { get; set; }
    public Program Program { get; set; } = null!;

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public ModuleType ModuleType { get; set; }

    public int ModuleOrder { get; set; }

    /// <summary>Prerequisite module that must be completed before this one can be accessed.</summary>
    public Guid? PrerequisiteModuleId { get; set; }
    public Module? PrerequisiteModule { get; set; }

    public bool IsMandatory { get; set; } = true;

    /// <summary>Retail price for purchasing this module individually.</summary>
    public decimal Price { get; set; }

    /// <summary>Retake fee if the student fails.</summary>
    public decimal RetakeFee { get; set; }

    /// <summary>What students will learn in this module.</summary>
    public string[] LearningOutcomes { get; set; } = Array.Empty<string>();

    // Navigation
    public ICollection<Course> Courses { get; set; } = new List<Course>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<ModuleEnrollment> ModuleEnrollments { get; set; } = new List<ModuleEnrollment>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
    public ICollection<ResearchMilestone> ResearchMilestones { get; set; } = new List<ResearchMilestone>();
}
