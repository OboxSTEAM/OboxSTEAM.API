using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueFrameworkPerProgramAndMultiCoTeach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_ClassSessionExperts_ClassSessionId",
                table: "ClassSessionExperts");

            migrationBuilder.Sql(
                """
                UPDATE "Programs" AS p
                SET "FrameworkId" = NULL
                WHERE p."IsDeleted" = false
                  AND p."FrameworkId" IS NOT NULL
                  AND p."Id" NOT IN (
                    SELECT kept."Id"
                    FROM (
                      SELECT DISTINCT ON ("FrameworkId") "Id"
                      FROM "Programs"
                      WHERE "IsDeleted" = false AND "FrameworkId" IS NOT NULL
                      ORDER BY "FrameworkId",
                               CASE "Status"
                                 WHEN 'Active' THEN 0
                                 WHEN 'Approved' THEN 1
                                 WHEN 'PendingReview' THEN 2
                                 WHEN 'Draft' THEN 3
                                 ELSE 4
                               END,
                               "CreatedAt"
                    ) AS kept
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessionExperts_ClassSessionId",
                table: "ClassSessionExperts",
                column: "ClassSessionId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Invited', 'Accepted')");
        }
    }
}
