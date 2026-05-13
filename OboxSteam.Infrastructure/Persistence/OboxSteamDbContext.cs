using Microsoft.EntityFrameworkCore;
using OboxSteam.Domain.Entities;
using OboxSteam.Infrastructure.Commons;

namespace OboxSteam.Infrastructure.Persistence;

public class OboxSteamDbContext : DbContext
{
    public OboxSteamDbContext(DbContextOptions<OboxSteamDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<OtpStorage> OtpStorages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Store all enum properties as strings in the database instead of integers.
        // This makes the data human-readable and prevents silent bugs when enum values are reordered.
        modelBuilder.UseStringForEnums();

        // Global query filter for soft delete
        modelBuilder.Entity<User>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OtpStorage>().HasQueryFilter(e => !e.IsDeleted);

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
