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
    public IGenericRepository<Expert> Experts => Repository<Expert>();
    public IGenericRepository<Certificate> Certificates => Repository<Certificate>();
    public IGenericRepository<MediaAsset> MediaAssets => Repository<MediaAsset>();
    public IGenericRepository<HighlightVideo> HighlightVideos => Repository<HighlightVideo>();
    public IGenericRepository<FaceEmbedding> FaceEmbeddings => Repository<FaceEmbedding>();
    public IGenericRepository<Portfolio> Portfolios => Repository<Portfolio>();
    public IGenericRepository<PortfolioCustomItem> PortfolioCustomItems => Repository<PortfolioCustomItem>();
    public IGenericRepository<StudentSkill> StudentSkills => Repository<StudentSkill>();
    public IGenericRepository<StandardizedTest> StandardizedTests => Repository<StandardizedTest>();
    public IGenericRepository<Payment> Payments => Repository<Payment>();
    public IGenericRepository<ParentStudent> ParentStudents => Repository<ParentStudent>();
    public async Task<int> SaveChangesAsync()
    {
        return await _dbContext.SaveChangesAsync();
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
