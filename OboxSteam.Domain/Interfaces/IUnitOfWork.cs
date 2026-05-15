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
    IGenericRepository<Expert> Experts { get; }
    IGenericRepository<Certificate> Certificates { get; }
    IGenericRepository<MediaAsset> MediaAssets { get; }
    IGenericRepository<HighlightVideo> HighlightVideos { get; }
    IGenericRepository<FaceEmbedding> FaceEmbeddings { get; }
    IGenericRepository<Portfolio> Portfolios { get; }
    IGenericRepository<PortfolioCustomItem> PortfolioCustomItems { get; }
    IGenericRepository<StudentSkill> StudentSkills { get; }
    IGenericRepository<StandardizedTest> StandardizedTests { get; }
    IGenericRepository<Payment> Payments { get; }

    Task<int> SaveChangesAsync();
}
