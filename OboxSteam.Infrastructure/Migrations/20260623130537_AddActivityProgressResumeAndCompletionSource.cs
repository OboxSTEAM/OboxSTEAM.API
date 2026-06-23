using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityProgressResumeAndCompletionSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletionSource",
                table: "ActivityProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccessedAt",
                table: "ActivityProgresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeState",
                table: "ActivityProgresses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionSource",
                table: "ActivityProgresses");

            migrationBuilder.DropColumn(
                name: "LastAccessedAt",
                table: "ActivityProgresses");

            migrationBuilder.DropColumn(
                name: "ResumeState",
                table: "ActivityProgresses");
        }
    }
}
