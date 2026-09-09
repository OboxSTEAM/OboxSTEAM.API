using Microsoft.EntityFrameworkCore;
using OboxSteam.Application.Interfaces;
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

        var portfolioService = scope.ServiceProvider.GetRequiredService<IPortfolioService>();
        var created = portfolioService.EnsureBuiltInSectionsForAllPortfoliosAsync()
            .GetAwaiter()
            .GetResult();

        if (created > 0)
        {
            logger.LogInformation(
                "Backfilled {Count} built-in portfolio section(s).",
                created);
        }
    }
}
