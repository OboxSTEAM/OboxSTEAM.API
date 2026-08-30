using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionAttendanceParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LeftAt",
                table: "SessionAttendances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParticipationMinutes",
                table: "SessionAttendances",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "ClassSessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeftAt",
                table: "SessionAttendances");

            migrationBuilder.DropColumn(
                name: "ParticipationMinutes",
                table: "SessionAttendances");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "ClassSessions");
        }
    }
}
