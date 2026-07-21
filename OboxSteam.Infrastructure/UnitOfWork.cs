using Microsoft.EntityFrameworkCore;
using OboxSteam.Application.Interfaces;
using OboxSteam.Domain.Entities;
using OboxSteam.Domain.Interfaces;
using OboxSteam.Infrastructure.Persistence;
using OboxSteam.Infrastructure.Repositories;
namespace OboxSteam.Infrastructure;

public class UnitOfWork : IUnitOfWork
{
    private readonly OboxSteamDbContext _dbContext;
    private readonly ICurrentTime _timeService;
    private readonly IClaimsService _claimsService;
    private readonly Dictionary<Type, object> _repositories = new();
    private bool _disposed;

    public UnitOfWork(OboxSteamDbContext dbContext, ICurrentTime timeService, IClaimsService claimsService)
    {
        _dbContext = dbContext;
        _timeService = timeService;
        _claimsService = claimsService;
    }

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        var type = typeof(TEntity);

        if (!_repositories.ContainsKey(type))
        {
            var repositoryInstance = new GenericRepository<TEntity>(_dbContext, _timeService, _claimsService);
            _repositories[type] = repositoryInstance;
        }

        return (IGenericRepository<TEntity>)_repositories[type];
    }
    public IGenericRepository<User> Users => Repository<User>();
    public IGenericRepository<OtpStorage> OtpStorages => Repository<OtpStorage>();
    public IGenericRepository<Program> Programs => Repository<Program>();
    public IGenericRepository<ProgramEnrollment> ProgramEnrollments => Repository<ProgramEnrollment>();
    public IGenericRepository<Course> Courses => Repository<Course>();
    public IGenericRepository<CourseEnrollment> CourseEnrollments => Repository<CourseEnrollment>();
    public IGenericRepository<Module> Modules => Repository<Module>();
    public IGenericRepository<ModuleEnrollment> ModuleEnrollments => Repository<ModuleEnrollment>();
    public IGenericRepository<Material> Materials => Repository<Material>();
    public IGenericRepository<Assignment> Assignments => Repository<Assignment>();
    public IGenericRepository<Submission> Submissions => Repository<Submission>();
    public IGenericRepository<QuizQuestion> QuizQuestions => Repository<QuizQuestion>();
    public IGenericRepository<QuizOption> QuizOptions => Repository<QuizOption>();
    public IGenericRepository<Activity> Activities => Repository<Activity>();
    public IGenericRepository<ActivityBooking> ActivityBookings => Repository<ActivityBooking>();
    public IGenericRepository<ActivityProgress> ActivityProgresses => Repository<ActivityProgress>();
    public IGenericRepository<Class> Classes => Repository<Class>();
    public IGenericRepository<ClassEnrollment> ClassEnrollments => Repository<ClassEnrollment>();
    public IGenericRepository<ClassSession> ClassSessions => Repository<ClassSession>();
    public IGenericRepository<SessionAttendance> SessionAttendances => Repository<SessionAttendance>();
    public IGenericRepository<ClassSkill> ClassSkills => Repository<ClassSkill>();
    public IGenericRepository<ClassMentorRequest> ClassMentorRequests => Repository<ClassMentorRequest>();
    public IGenericRepository<Expert> Experts => Repository<Expert>();
    public IGenericRepository<Certificate> Certificates => Repository<Certificate>();
    public IGenericRepository<MediaAsset> MediaAssets => Repository<MediaAsset>();
    public IGenericRepository<MediaTag> MediaTags => Repository<MediaTag>();
    public IGenericRepository<HighlightVideoStack> HighlightVideoStacks => Repository<HighlightVideoStack>();
    public IGenericRepository<HighlightVideoItem> HighlightVideoItems => Repository<HighlightVideoItem>();
    public IGenericRepository<FaceEmbedding> FaceEmbeddings => Repository<FaceEmbedding>();
    public IGenericRepository<Portfolio> Portfolios => Repository<Portfolio>();
    public IGenericRepository<PortfolioCustomItem> PortfolioCustomItems => Repository<PortfolioCustomItem>();
    public IGenericRepository<PortfolioItemSubmission> PortfolioItemSubmissions => Repository<PortfolioItemSubmission>();
    public IGenericRepository<PortfolioSection> PortfolioSections => Repository<PortfolioSection>();
    public IGenericRepository<PortfolioMediaAsset> PortfolioMediaAssets => Repository<PortfolioMediaAsset>();
    public IGenericRepository<PortfolioMediaPlacement> PortfolioMediaPlacements => Repository<PortfolioMediaPlacement>();
    public IGenericRepository<ResearchMilestone> ResearchMilestones => Repository<ResearchMilestone>();
    public IGenericRepository<ResearchMilestoneActivity> ResearchMilestoneActivities => Repository<ResearchMilestoneActivity>();
    public IGenericRepository<Skill> Skills => Repository<Skill>();
    public IGenericRepository<StudentSkill> StudentSkills => Repository<StudentSkill>();
    public IGenericRepository<StudentSkillEvidence> StudentSkillEvidences => Repository<StudentSkillEvidence>();
    public IGenericRepository<MentorSkill> MentorSkills => Repository<MentorSkill>();
    public IGenericRepository<StandardizedTest> StandardizedTests => Repository<StandardizedTest>();
    public IGenericRepository<StudentProfile> StudentProfiles => Repository<StudentProfile>();
    public IGenericRepository<SubmissionEvidence> SubmissionEvidences => Repository<SubmissionEvidence>();
    public IGenericRepository<Payment> Payments => Repository<Payment>();
    public IGenericRepository<ProgramBoard> ProgramBoards => Repository<ProgramBoard>();
    public IGenericRepository<ParentStudent> ParentStudents => Repository<ParentStudent>();
    public IGenericRepository<QuestionBank> QuestionBanks => Repository<QuestionBank>();
    public IGenericRepository<BankQuestion> BankQuestions => Repository<BankQuestion>();
    public IGenericRepository<BankQuestionOption> BankQuestionOptions => Repository<BankQuestionOption>();
    public IGenericRepository<QuizAnswer> QuizAnswers => Repository<QuizAnswer>();
    public IGenericRepository<ProgramReview> ProgramReviews => Repository<ProgramReview>();
    public IGenericRepository<PaymentRequest> PaymentRequests => Repository<PaymentRequest>();
    public IGenericRepository<Invoice> Invoices => Repository<Invoice>();
    public IGenericRepository<Notification> Notifications => Repository<Notification>();
    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
    }

    public async Task TruncateAllApplicationTablesAsync()
    {
        const string sql = """
            DO $$ DECLARE table_name text;
            BEGIN
              FOR table_name IN
                SELECT quote_ident(tablename)
                FROM pg_tables
                WHERE schemaname = 'public'
                  AND tablename <> '__EFMigrationsHistory'
              LOOP
                EXECUTE 'TRUNCATE TABLE ' || table_name || ' RESTART IDENTITY CASCADE';
              END LOOP;
            END $$;
            """;

        await _dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _dbContext.Dispose();
            }

            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
