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
    public IGenericRepository<FaceEmbedding> FaceEmbeddings => Repository<FaceEmbedding>();


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
