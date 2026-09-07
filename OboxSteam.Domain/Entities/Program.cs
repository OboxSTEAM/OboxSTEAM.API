using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class Program : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., PRG-ROBOTICS

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    public string? SeriesName { get; set; } // e.g., Obox Master Track

    public string? Description { get; set; }

    public DifficultyLevel Level { get; set; } = DifficultyLevel.Beginner;

    public ProgramCategory Category { get; set; } 

    [MaxLength(255)]
    public string? EstimatedDuration { get; set; } // e.g., 3 months at 10 hours a week

    public string? SkillsGained { get; set; } // JSON array or comma separated

    public decimal? Rating { get; set; } // e.g., 4.8

    public int TotalReviews { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>Catalog lifecycle: Draft, PendingReview, Approved, Active, or Inactive.</summary>
    public ProgramStatus Status { get; set; } = ProgramStatus.Draft;

    /// <summary>Bundle price for the entire program (usually discounted vs sum of modules).</summary>
    public decimal? Price { get; set; }

    /// <summary>Retake fee when re-enrolling after a Failed/Dropped enrollment. Null = use Price.</summary>
    public decimal? RetakeFee { get; set; }

    /// <summary>Optional reusable framework identity. Kept for additive API compatibility.</summary>
    public Guid? FrameworkId { get; set; }
    public ProgramFramework? Framework { get; set; }

    /// <summary>Published immutable framework version pinned by this program.</summary>
    public Guid? FrameworkVersionId { get; set; }
    public ProgramFrameworkVersion? FrameworkVersion { get; set; }

    /// <summary>The single expert responsible for future academic decisions.</summary>
    public Guid? AdvisorExpertId { get; set; }
    public Expert? AdvisorExpert { get; set; }

    // Navigation
    public ICollection<ProgramBoard> ProgramBoards { get; set; } = new List<ProgramBoard>();
    public ICollection<Module> Modules { get; set; } = new List<Module>();
    public ICollection<ProgramEnrollment> ProgramEnrollments { get; set; } = new List<ProgramEnrollment>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<ProgramReview> Reviews { get; set; } = new List<ProgramReview>();
    public ICollection<CurriculumReview> CurriculumReviews { get; set; } = new List<CurriculumReview>();
    public ICollection<ProgramReviewSubmission> ReviewSubmissions { get; set; } = [];
    public ICollection<ProgramAdvisoryThread> AdvisoryThreads { get; set; } = [];
    public ICollection<Class> Classes { get; set; } = new List<Class>();
    public ICollection<PaymentRequest> PaymentRequests { get; set; } = new List<PaymentRequest>();
    public ICollection<ProgramBundleItem> BundleItems { get; set; } = new List<ProgramBundleItem>();
}
