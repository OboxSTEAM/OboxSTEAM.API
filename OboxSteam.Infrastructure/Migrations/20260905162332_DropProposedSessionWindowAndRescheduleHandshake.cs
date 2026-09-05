using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropProposedSessionWindowAndRescheduleHandshake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Notifications"
                WHERE "Type" IN (
                    'ClassSessionExpertRescheduleRequested',
                    'ClassSessionExpertRescheduleDeclined');
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClassSessions_ProposedWindowPair",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "ProposedEndTime",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "ProposedStartTime",
                table: "ClassSessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedEndTime",
                table: "ClassSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedStartTime",
                table: "ClassSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClassSessions_ProposedWindowPair",
                table: "ClassSessions",
                sql: "(\"ProposedStartTime\" IS NULL AND \"ProposedEndTime\" IS NULL) OR (\"ProposedStartTime\" IS NOT NULL AND \"ProposedEndTime\" IS NOT NULL AND \"ProposedEndTime\" > \"ProposedStartTime\")");
        }
    }
}
