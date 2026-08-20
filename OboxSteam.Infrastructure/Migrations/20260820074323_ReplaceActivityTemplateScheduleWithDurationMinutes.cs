using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceActivityTemplateScheduleWithDurationMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Activities");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Activities",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Activities");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Activities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
