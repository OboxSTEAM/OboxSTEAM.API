using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProgramPurchaseRebuy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments");

            migrationBuilder.AddColumn<decimal>(
                name: "RetakeFee",
                table: "Programs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "ProgramEnrollments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "ProgramEnrollments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EndedModuleId",
                table: "ProgramEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceProgramEnrollmentId",
                table: "ProgramEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_EndedModuleId",
                table: "ProgramEnrollments",
                column: "EndedModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_SourceProgramEnrollmentId",
                table: "ProgramEnrollments",
                column: "SourceProgramEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments",
                columns: new[] { "StudentId", "ProgramId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('PendingPayment', 'Active')");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramEnrollments_Modules_EndedModuleId",
                table: "ProgramEnrollments",
                column: "EndedModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramEnrollments_ProgramEnrollments_SourceProgramEnrollme~",
                table: "ProgramEnrollments",
                column: "SourceProgramEnrollmentId",
                principalTable: "ProgramEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramEnrollments_Modules_EndedModuleId",
                table: "ProgramEnrollments");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramEnrollments_ProgramEnrollments_SourceProgramEnrollme~",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_EndedModuleId",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_SourceProgramEnrollmentId",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "RetakeFee",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "EndedModuleId",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "SourceProgramEnrollmentId",
                table: "ProgramEnrollments");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_StudentId_ProgramId",
                table: "ProgramEnrollments",
                columns: new[] { "StudentId", "ProgramId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
