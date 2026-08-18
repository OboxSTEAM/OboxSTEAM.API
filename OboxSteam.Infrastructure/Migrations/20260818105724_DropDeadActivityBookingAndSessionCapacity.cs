using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadActivityBookingAndSessionCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityBookings");

            migrationBuilder.DropColumn(
                name: "DefaultSchedulingMode",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "MaxCapacity",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "MaxCapacity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SchedulingMode",
                table: "Activities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultSchedulingMode",
                table: "Modules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCapacity",
                table: "ClassSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCapacity",
                table: "Activities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchedulingMode",
                table: "Activities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ActivityBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityBookings_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityBookings_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityBookings_ActivityId",
                table: "ActivityBookings",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityBookings_StudentId_ActivityId",
                table: "ActivityBookings",
                columns: new[] { "StudentId", "ActivityId" },
                unique: true);
        }
    }
}
