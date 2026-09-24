using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneralAdvisoryThread : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "ProgramAdvisoryThreads" (
                    "Id", "ProgramId", "AuthorUserId", "SubmissionId", "TargetType", "TargetId",
                    "TargetLabel", "TargetContext", "Type", "Status", "ConcurrencyVersion",
                    "LatestActivitySequence", "LastMessageAt", "IsDeleted", "CreatedAt", "CreatedBy",
                    "UpdatedAt", "UpdatedBy", "DeletedAt", "DeletedBy")
                SELECT
                    gen_random_uuid(),
                    d."ProgramId",
                    first_msg."AuthorUserId",
                    NULL,
                    'Program',
                    d."ProgramId",
                    p."Name",
                    'General discussion',
                    'General',
                    'Open',
                    gen_random_uuid(),
                    COALESCE(stats."MaxSequence", 0),
                    COALESCE(stats."LastMessageAt", NOW() AT TIME ZONE 'utc'),
                    FALSE,
                    COALESCE(first_msg."CreatedAt", NOW() AT TIME ZONE 'utc'),
                    first_msg."AuthorUserId",
                    NULL, NULL, NULL, NULL
                FROM (
                    SELECT DISTINCT "ProgramId"
                    FROM "ProgramAdvisoryDiscussionMessages"
                    WHERE "IsDeleted" = FALSE
                ) AS d
                JOIN "Programs" AS p ON p."Id" = d."ProgramId"
                JOIN LATERAL (
                    SELECT m."AuthorUserId", m."CreatedAt"
                    FROM "ProgramAdvisoryDiscussionMessages" AS m
                    WHERE m."ProgramId" = d."ProgramId" AND m."IsDeleted" = FALSE
                    ORDER BY m."Sequence"
                    LIMIT 1
                ) AS first_msg ON TRUE
                JOIN LATERAL (
                    SELECT MAX(m."Sequence") AS "MaxSequence", MAX(m."CreatedAt") AS "LastMessageAt"
                    FROM "ProgramAdvisoryDiscussionMessages" AS m
                    WHERE m."ProgramId" = d."ProgramId" AND m."IsDeleted" = FALSE
                ) AS stats ON TRUE
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "ProgramAdvisoryThreads" AS t
                    WHERE t."ProgramId" = d."ProgramId"
                      AND t."Type" = 'General'
                      AND t."IsDeleted" = FALSE
                );

                INSERT INTO "ProgramAdvisoryMessages" (
                    "Id", "ThreadId", "AuthorUserId", "StreamSequence", "Message",
                    "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "DeletedAt", "DeletedBy")
                SELECT
                    gen_random_uuid(),
                    t."Id",
                    m."AuthorUserId",
                    m."Sequence",
                    m."Text",
                    FALSE,
                    m."CreatedAt",
                    m."CreatedBy",
                    NULL, NULL, NULL, NULL
                FROM "ProgramAdvisoryDiscussionMessages" AS m
                JOIN "ProgramAdvisoryThreads" AS t
                    ON t."ProgramId" = m."ProgramId"
                   AND t."Type" = 'General'
                   AND t."IsDeleted" = FALSE
                WHERE m."IsDeleted" = FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ProgramAdvisoryMessages" AS existing
                      WHERE existing."ThreadId" = t."Id"
                        AND existing."StreamSequence" = m."Sequence"
                        AND existing."IsDeleted" = FALSE
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_OneGeneralPerProgram",
                table: "ProgramAdvisoryThreads",
                column: "ProgramId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Type\" = 'General'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryThreads_OneGeneralPerProgram",
                table: "ProgramAdvisoryThreads");
        }
    }
}
