using OboxSteam.Domain.Entities;

namespace OboxSteam.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // Generic method - access any repository
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : Entities.BaseEntity;

    IGenericRepository<User> Users { get; }
    IGenericRepository<OtpStorage> OtpStorages { get; }
    IGenericRepository<Program> Programs { get; }
    IGenericRepository<ProgramEnrollment> ProgramEnrollments { get; }
    IGenericRepository<Course> Courses { get; }
    IGenericRepository<CourseEnrollment> CourseEnrollments { get; }
    IGenericRepository<Module> Modules { get; }
    IGenericRepository<ModuleEnrollment> ModuleEnrollments { get; }
    IGenericRepository<Material> Materials { get; }
    IGenericRepository<Assignment> Assignments { get; }
    IGenericRepository<Submission> Submissions { get; }
    IGenericRepository<QuizQuestion> QuizQuestions { get; }
    IGenericRepository<QuizOption> QuizOptions { get; }
    IGenericRepository<Activity> Activities { get; }
    IGenericRepository<ActivityBooking> ActivityBookings { get; }
    IGenericRepository<ActivityProgress> ActivityProgresses { get; }
    IGenericRepository<Class> Classes { get; }
    IGenericRepository<ClassEnrollment> ClassEnrollments { get; }
    IGenericRepository<ClassSession> ClassSessions { get; }
    IGenericRepository<SessionAttendance> SessionAttendances { get; }
    IGenericRepository<ClassSkill> ClassSkills { get; }
    IGenericRepository<ClassMentorRequest> ClassMentorRequests { get; }
    IGenericRepository<ClassQuizQuestionSet> ClassQuizQuestionSets { get; }
    IGenericRepository<ClassQuizQuestion> ClassQuizQuestions { get; }
    IGenericRepository<ClassQuizQuestionOption> ClassQuizQuestionOptions { get; }
    IGenericRepository<Expert> Experts { get; }
    IGenericRepository<Certificate> Certificates { get; }
    IGenericRepository<MediaAsset> MediaAssets { get; }
    IGenericRepository<MediaTag> MediaTags { get; }
    IGenericRepository<HighlightVideoStack> HighlightVideoStacks { get; }
    IGenericRepository<HighlightVideoItem> HighlightVideoItems { get; }
    IGenericRepository<FaceEmbedding> FaceEmbeddings { get; }
    IGenericRepository<Portfolio> Portfolios { get; }
    IGenericRepository<PortfolioCustomItem> PortfolioCustomItems { get; }
    IGenericRepository<PortfolioItemSubmission> PortfolioItemSubmissions { get; }
    IGenericRepository<PortfolioSection> PortfolioSections { get; }
    IGenericRepository<PortfolioMediaAsset> PortfolioMediaAssets { get; }
    IGenericRepository<PortfolioMediaPlacement> PortfolioMediaPlacements { get; }
    IGenericRepository<ResearchMilestone> ResearchMilestones { get; }
    IGenericRepository<ResearchMilestoneActivity> ResearchMilestoneActivities { get; }
    IGenericRepository<Skill> Skills { get; }
    IGenericRepository<StudentSkill> StudentSkills { get; }
    IGenericRepository<StudentSkillEvidence> StudentSkillEvidences { get; }
    IGenericRepository<MentorSkill> MentorSkills { get; }
    IGenericRepository<MentorSkillEvidence> MentorSkillEvidences { get; }
    IGenericRepository<StandardizedTest> StandardizedTests { get; }
    IGenericRepository<StudentProfile> StudentProfiles { get; }
    IGenericRepository<MentorProfile> MentorProfiles { get; }
    IGenericRepository<SubmissionEvidence> SubmissionEvidences { get; }
    IGenericRepository<Payment> Payments { get; }
    IGenericRepository<ProgramBoard> ProgramBoards { get; }
    IGenericRepository<ParentStudent> ParentStudents { get; }
    IGenericRepository<QuestionBank> QuestionBanks { get; }
    IGenericRepository<BankQuestion> BankQuestions { get; }
    IGenericRepository<BankQuestionOption> BankQuestionOptions { get; }
    IGenericRepository<QuizAnswer> QuizAnswers { get; }
    IGenericRepository<ProgramReview> ProgramReviews { get; }
    IGenericRepository<PaymentRequest> PaymentRequests { get; }
    IGenericRepository<Invoice> Invoices { get; }
    IGenericRepository<Notification> Notifications { get; }

    Task<int> SaveChangesAsync();

    /// <summary>Dev-only: truncate all application tables (keeps EF migration history).</summary>
    Task TruncateAllApplicationTablesAsync();
}
