using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OboxSteam.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for OboxSteamDbContext.
/// Used by EF Core tools (migrations, scaffolding) when running from CLI.
/// </summary>
public class OboxSteamDbContextFactory : IDesignTimeDbContextFactory<OboxSteamDbContext>
{
    public OboxSteamDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json in the API project
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "OboxSteam.API"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<OboxSteamDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.MigrationsAssembly(typeof(OboxSteamDbContext).Assembly.FullName);
        });

        return new OboxSteamDbContext(optionsBuilder.Options);
    }
}
