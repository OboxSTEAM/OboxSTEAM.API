using System.ComponentModel.DataAnnotations;
using OboxSteam.Domain.Enums;

namespace OboxSteam.Domain.Entities;

public class User : BaseEntity
{
    [MaxLength(50)]
    public string Code { get; set; } = null!; // e.g., STD-26001

    [MaxLength(255)]
    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    [MaxLength(255)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public string? AvatarUrl { get; set; } // S3 Link

    public RoleType Role { get; set; }

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    // JWT Token
    [MaxLength(128)]
    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public bool IsEmailVerified { get; set; }

    // Navigation properties
    public ICollection<ParentStudent> ParentRelations { get; set; } = new List<ParentStudent>();
    public ICollection<ParentStudent> StudentRelations { get; set; } = new List<ParentStudent>();
    public StudentProfile? StudentProfile { get; set; }
    public ICollection<StudentSkill> StudentSkills { get; set; } = new List<StudentSkill>();
    public ICollection<StandardizedTest> StandardizedTests { get; set; } = new List<StandardizedTest>();
    public Expert? Expert { get; set; }
    public FaceEmbedding? FaceEmbedding { get; set; }
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public ICollection<Submission> VerifiedSubmissions { get; set; } = new List<Submission>();
    public ICollection<ActivityBooking> ActivityBookings { get; set; } = new List<ActivityBooking>();
    public ICollection<CourseEnrollment> CourseEnrollments { get; set; } = new List<CourseEnrollment>();
    public ICollection<ModuleEnrollment> ModuleEnrollments { get; set; } = new List<ModuleEnrollment>();
    public ICollection<ActivityProgress> ActivityProgresses { get; set; } = new List<ActivityProgress>();
    public ICollection<ProgramEnrollment> ProgramEnrollments { get; set; } = new List<ProgramEnrollment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<Payment> PaidPayments { get; set; } = new List<Payment>();
    public ICollection<PaymentRequest> SentPaymentRequests { get; set; } = new List<PaymentRequest>();
    public ICollection<PaymentRequest> ReceivedPaymentRequests { get; set; } = new List<PaymentRequest>();
    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
    public ICollection<HighlightVideo> HighlightVideos { get; set; } = new List<HighlightVideo>();
    public ICollection<MediaAsset> UploadedMediaAssets { get; set; } = new List<MediaAsset>();
    public ICollection<MediaTag> MediaTags { get; set; } = new List<MediaTag>();
    public ICollection<ProgramReview> ProgramReviews { get; set; } = new List<ProgramReview>();
    public ICollection<Class> MentoredClasses { get; set; } = new List<Class>();
    public ICollection<ClassEnrollment> ClassEnrollments { get; set; } = new List<ClassEnrollment>();
    public ICollection<SessionAttendance> SessionAttendances { get; set; } = new List<SessionAttendance>();
    public ICollection<SessionAttendance> RecordedSessionAttendances { get; set; } = new List<SessionAttendance>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
