using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssignmentWindowPerClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill missing AssignmentWindow sessions from catalog dates before those columns drop.
            migrationBuilder.Sql(
                """
                INSERT INTO "ClassSessions" (
                    "Id",
                    "ClassId",
                    "ModuleId",
                    "AssignmentId",
                    "SessionKind",
                    "Title",
                    "StartTime",
                    "EndTime",
                    "RequiresAttendance",
                    "RequiresMentorCheckIn",
                    "Status",
                    "IsDeleted",
                    "CreatedAt",
                    "CreatedBy"
                )
                SELECT
                    gen_random_uuid(),
                    c."Id",
                    a."ModuleId",
                    a."Id",
                    'AssignmentWindow',
                    LEFT(a."Title", 255),
                    COALESCE(a."AvailableFrom", c."StartDate"),
                    COALESCE(a."AvailableUntil", a."DueDate", c."EndDate"),
                    false,
                    false,
                    'Scheduled',
                    false,
                    (NOW() AT TIME ZONE 'utc'),
                    '00000000-0000-0000-0000-000000000000'::uuid
                FROM "Classes" c
                INNER JOIN "Modules" m
                    ON m."ProgramId" = c."ProgramId"
                    AND m."IsDeleted" = false
                INNER JOIN "Assignments" a
                    ON a."ModuleId" = m."Id"
                    AND a."IsDeleted" = false
                WHERE c."IsDeleted" = false
                  AND c."Kind" = 'Standard'
                  AND COALESCE(a."AvailableUntil", a."DueDate", c."EndDate")
                      > COALESCE(a."AvailableFrom", c."StartDate")
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "ClassSessions" s
                      WHERE s."ClassId" = c."Id"
                        AND s."AssignmentId" = a."Id"
                        AND s."SessionKind" = 'AssignmentWindow'
                        AND s."IsDeleted" = false
                        AND s."Status" <> 'Cancelled'
                  );
                """);

            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "AvailableUntil",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "PersonalAvailableUntil",
                table: "AssessmentRecoveryRequests");

            migrationBuilder.DropColumn(
                name: "PersonalDueDate",
                table: "AssessmentRecoveryRequests");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessions_ClassId_AssignmentId",
                table: "ClassSessions",
                columns: new[] { "ClassId", "AssignmentId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" <> 'Cancelled' AND \"SessionKind\" = 'AssignmentWindow' AND \"AssignmentId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClassSessions_ClassId_AssignmentId",
                table: "ClassSessions");

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                table: "Assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableUntil",
                table: "Assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Assignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalAvailableUntil",
                table: "AssessmentRecoveryRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalDueDate",
                table: "AssessmentRecoveryRequests",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
