using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalLifecycleRemovePendingReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Programs" SET "Status" = 'Draft' WHERE "Status" = 'PendingReview';
                """);

            // Programs approved under the submission flow get one active approval at their
            // current curriculum version so they stay publishable.
            migrationBuilder.Sql("""
                INSERT INTO "ProgramApprovals" (
                    "Id", "ProgramId", "CurriculumVersion", "FromVersion", "FrameworkVersionId",
                    "FrameworkCheckJson", "CurriculumSnapshotJson", "ApprovedByExpertId", "ApprovedAt",
                    "Comment", "IsDeleted", "CreatedAt", "CreatedBy")
                SELECT gen_random_uuid(), p."Id", p."CurriculumVersion", 0, p."FrameworkVersionId",
                       '{}'::jsonb, '{}'::jsonb, COALESCE(r."ExpertId", p."AdvisorExpertId"),
                       COALESCE(r."ReviewedAt", now() AT TIME ZONE 'utc'), LEFT(r."Comment", 2000), false,
                       now() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'
                FROM "Programs" p
                LEFT JOIN LATERAL (
                    SELECT cr."ExpertId", cr."ReviewedAt", cr."Comment"
                    FROM "CurriculumReviews" cr
                    WHERE cr."ProgramId" = p."Id" AND cr."Decision" = 'Approved' AND cr."IsDeleted" = false
                    ORDER BY cr."ReviewedAt" DESC
                    LIMIT 1
                ) r ON true
                WHERE p."Status" = 'Approved'
                  AND p."IsDeleted" = false
                  AND COALESCE(r."ExpertId", p."AdvisorExpertId") IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM "ProgramApprovals" a
                      WHERE a."ProgramId" = p."Id" AND a."RevokedAt" IS NULL AND a."IsDeleted" = false);
                """);

            // Approved programs with no identifiable approving expert cannot be backfilled.
            migrationBuilder.Sql("""
                UPDATE "Programs" p SET "Status" = 'Draft'
                WHERE p."Status" = 'Approved'
                  AND NOT EXISTS (
                      SELECT 1 FROM "ProgramApprovals" a
                      WHERE a."ProgramId" = p."Id" AND a."RevokedAt" IS NULL AND a."IsDeleted" = false);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
