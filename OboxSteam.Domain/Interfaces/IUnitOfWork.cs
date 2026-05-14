using OboxSteam.Domain.Entities;

namespace OboxSteam.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    // Generic method - access any repository
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : Entities.BaseEntity;

    IGenericRepository<User> Users { get; }
    IGenericRepository<OtpStorage> OtpStorages { get; }
    IGenericRepository<FaceEmbedding> FaceEmbeddings { get; }
    

    Task<int> SaveChangesAsync();
}
