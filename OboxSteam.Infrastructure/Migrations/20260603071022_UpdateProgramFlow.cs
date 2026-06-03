using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProgramFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModuleEnrollments_Users_StudentId",
                table: "ModuleEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_StudentId",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ModuleEnrollments_StudentId",
                table: "ModuleEnrollments");

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleEnrollmentId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentFailureCount",
                table: "ModuleEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "ModuleEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramEnrollmentId",
                table: "ModuleEnrollments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "IsRequiredForModulePass",
                table: "Assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PassScore",
                table: "Assignments",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ActivityProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityProgresses_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ActivityProgresses_ModuleEnrollments_ModuleEnrollmentId",
                        column: x => x.ModuleEnrollmentId,
                        principalTable: "ModuleEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActivityProgresses_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ModuleEnrollmentId",
                table: "Submissions",
                column: "ModuleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments",
                columns: new[] { "StudentId", "ProgramId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleEnrollments_ProgramEnrollmentId",
                table: "ModuleEnrollments",
                column: "ProgramEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleEnrollments_StudentId_ModuleId_AttemptNumber",
                table: "ModuleEnrollments",
                columns: new[] { "StudentId", "ModuleId", "AttemptNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityProgresses_ActivityId",
                table: "ActivityProgresses",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityProgresses_ModuleEnrollmentId_ActivityId",
                table: "ActivityProgresses",
                columns: new[] { "ModuleEnrollmentId", "ActivityId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityProgresses_StudentId",
                table: "ActivityProgresses",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModuleEnrollments_ProgramEnrollments_ProgramEnrollmentId",
                table: "ModuleEnrollments",
                column: "ProgramEnrollmentId",
                principalTable: "ProgramEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ModuleEnrollments_Users_StudentId",
                table: "ModuleEnrollments",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ModuleEnrollments_ModuleEnrollmentId",
                table: "Submissions",
                column: "ModuleEnrollmentId",
                principalTable: "ModuleEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ModuleEnrollments_ProgramEnrollments_ProgramEnrollmentId",
                table: "ModuleEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_ModuleEnrollments_Users_StudentId",
                table: "ModuleEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ModuleEnrollments_ModuleEnrollmentId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "ActivityProgresses");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ModuleEnrollmentId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ModuleEnrollments_ProgramEnrollmentId",
                table: "ModuleEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ModuleEnrollments_StudentId_ModuleId_AttemptNumber",
                table: "ModuleEnrollments");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ModuleEnrollmentId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AssignmentFailureCount",
                table: "ModuleEnrollments");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "ModuleEnrollments");

            migrationBuilder.DropColumn(
                name: "ProgramEnrollmentId",
                table: "ModuleEnrollments");

            migrationBuilder.DropColumn(
                name: "IsRequiredForModulePass",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "PassScore",
                table: "Assignments");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StudentId",
                table: "ProgramEnrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleEnrollments_StudentId",
                table: "ModuleEnrollments",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ModuleEnrollments_Users_StudentId",
                table: "ModuleEnrollments",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
