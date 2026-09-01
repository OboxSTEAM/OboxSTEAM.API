using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassSessionExpertCoTeachRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessionExperts_ClassSessionId",
                table: "ClassSessionExperts",
                column: "ClassSessionId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Invited', 'Accepted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClassSessions_ProposedWindowPair",
                table: "ClassSessions");

            migrationBuilder.DropIndex(
                name: "IX_ClassSessionExperts_ClassSessionId",
                table: "ClassSessionExperts");

            migrationBuilder.DropColumn(
                name: "ProposedEndTime",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "ProposedStartTime",
                table: "ClassSessions");
        }
    }
}
