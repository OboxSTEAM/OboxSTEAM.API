using Microsoft.EntityFrameworkCore;
using OboxSteam.Infrastructure.Persistence;

namespace OboxSteam.API.Architecture;

public static class MigrationExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app, ILogger logger)
    {
        logger.LogInformation("Applying database migrations...");

        using var scope = app.ApplicationServices.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<OboxSteamDbContext>();

        // EF Core's EnableRetryOnFailure handles connection retries automatically
        dbContext.Database.Migrate();

        logger.LogInformation("Database migrations applied successfully!");
    }
}

