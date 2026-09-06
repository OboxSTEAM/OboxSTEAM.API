using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProgramEnrollmentSupersededBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByEnrollmentId",
                table: "ProgramEnrollments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_SupersededByEnrollmentId",
                table: "ProgramEnrollments",
                column: "SupersededByEnrollmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramEnrollments_ProgramEnrollments_SupersededByEnrollmen~",
                table: "ProgramEnrollments",
                column: "SupersededByEnrollmentId",
                principalTable: "ProgramEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill: Active/Deferred rebuys already paid before this column existed.
            migrationBuilder.Sql(
                """
                UPDATE "ProgramEnrollments" AS source
                SET "SupersededByEnrollmentId" = rebuy."Id"
                FROM "ProgramEnrollments" AS rebuy
                WHERE rebuy."SourceProgramEnrollmentId" = source."Id"
                  AND rebuy."IsDeleted" = false
                  AND source."IsDeleted" = false
                  AND source."SupersededByEnrollmentId" IS NULL
                  AND rebuy."Status" IN ('Active', 'Deferred');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgramEnrollments_ProgramEnrollments_SupersededByEnrollmen~",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_SupersededByEnrollmentId",
                table: "ProgramEnrollments");

            migrationBuilder.DropColumn(
                name: "SupersededByEnrollmentId",
                table: "ProgramEnrollments");
        }
    }
}
