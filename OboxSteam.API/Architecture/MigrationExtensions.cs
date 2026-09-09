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

        BackfillFrameworkVersionsAndAdvisors(dbContext, logger);

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

    private static void BackfillFrameworkVersionsAndAdvisors(
        OboxSteamDbContext dbContext,
        ILogger logger)
    {
        using var transaction = dbContext.Database.BeginTransaction();
        var affected = dbContext.Database.ExecuteSqlRaw(
            """
            INSERT INTO "ProgramFrameworkVersions"
                ("Id", "FrameworkId", "VersionNumber", "Description", "AcademicGuidance",
                 "MinModules", "MinOfflineSessions", "MinLiveSessions",
                 "RequireCapstoneResearchMilestone", "IsPublished", "PublishedAt",
                 "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
                 "DeletedAt", "DeletedBy")
            SELECT f."Id", f."Id", 1, f."Description", NULL,
                   f."MinModules", f."MinOfflineSessions", f."MinLiveSessions",
                   f."RequireCapstoneResearchMilestone", TRUE,
                   COALESCE(f."UpdatedAt", f."CreatedAt"),
                   f."IsDeleted", f."CreatedAt", f."CreatedBy", f."UpdatedAt", f."UpdatedBy",
                   f."DeletedAt", f."DeletedBy"
            FROM "ProgramFrameworks" f
            WHERE NOT EXISTS (
                SELECT 1 FROM "ProgramFrameworkVersions" v
                WHERE v."FrameworkId" = f."Id" AND v."VersionNumber" = 1);

            UPDATE "FrameworkRubricCriteria"
            SET "FrameworkVersionId" = "FrameworkId"
            WHERE "FrameworkVersionId" IS NULL;

            UPDATE "Programs"
            SET "FrameworkVersionId" = "FrameworkId"
            WHERE "FrameworkId" IS NOT NULL AND "FrameworkVersionId" IS NULL;

            UPDATE "Programs" p
            SET "AdvisorExpertId" = f."ExpertId"
            FROM "ProgramFrameworks" f
            JOIN "Experts" e ON e."Id" = f."ExpertId" AND e."IsDeleted" = FALSE
            JOIN "Users" u ON u."Id" = e."UserId" AND u."IsDeleted" = FALSE
                AND u."Role" = 'Expert' AND u."Status" = 'Active'
            WHERE p."FrameworkId" = f."Id" AND p."AdvisorExpertId" IS NULL;

            UPDATE "Programs" p
            SET "AdvisorExpertId" = candidate."ExpertId"
            FROM (
                SELECT pb."ProgramId", MIN(pb."ExpertId") AS "ExpertId"
                FROM "ProgramBoards" pb
                JOIN "Experts" e ON e."Id" = pb."ExpertId" AND e."IsDeleted" = FALSE
                JOIN "Users" u ON u."Id" = e."UserId" AND u."IsDeleted" = FALSE
                    AND u."Role" = 'Expert' AND u."Status" = 'Active'
                WHERE pb."IsDeleted" = FALSE
                GROUP BY pb."ProgramId"
                HAVING COUNT(DISTINCT pb."ExpertId") = 1
            ) candidate
            WHERE p."Id" = candidate."ProgramId"
              AND p."FrameworkId" IS NULL
              AND p."AdvisorExpertId" IS NULL;

            UPDATE "ProgramBoards" pb
            SET "IsDeleted" = FALSE, "DeletedAt" = NULL, "DeletedBy" = NULL,
                "UpdatedAt" = NOW()
            FROM "Programs" p
            WHERE p."Id" = pb."ProgramId"
              AND p."AdvisorExpertId" = pb."ExpertId"
              AND pb."IsDeleted" = TRUE;

            INSERT INTO "ProgramBoards"
                ("Id", "ProgramId", "ExpertId", "RoleInBoard", "IsDeleted",
                 "CreatedAt", "CreatedBy")
            SELECT gen_random_uuid(), p."Id", p."AdvisorExpertId", 'Responsible advisor',
                   FALSE, NOW(), '00000000-0000-0000-0000-000000000000'
            FROM "Programs" p
            WHERE p."AdvisorExpertId" IS NOT NULL
              AND NOT EXISTS (
                  SELECT 1 FROM "ProgramBoards" pb
                  WHERE pb."ProgramId" = p."Id"
                    AND pb."ExpertId" = p."AdvisorExpertId");
            """);
        transaction.Commit();
        if (affected > 0)
        {
            logger.LogInformation(
                "Framework/advisor compatibility backfill affected {Count} row operations.",
                affected);
        }
    }
}
