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
    IGenericRepository<Expert> Experts { get; }
    IGenericRepository<Certificate> Certificates { get; }
    IGenericRepository<MediaAsset> MediaAssets { get; }
    IGenericRepository<MediaTag> MediaTags { get; }
    IGenericRepository<HighlightVideo> HighlightVideos { get; }
    IGenericRepository<FaceEmbedding> FaceEmbeddings { get; }
    IGenericRepository<Portfolio> Portfolios { get; }
    IGenericRepository<PortfolioCustomItem> PortfolioCustomItems { get; }
    IGenericRepository<PortfolioItemSubmission> PortfolioItemSubmissions { get; }
    IGenericRepository<ResearchMilestone> ResearchMilestones { get; }
    IGenericRepository<ResearchMilestoneActivity> ResearchMilestoneActivities { get; }
    IGenericRepository<StudentSkill> StudentSkills { get; }
    IGenericRepository<StandardizedTest> StandardizedTests { get; }
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

    Task<int> SaveChangesAsync();
}
