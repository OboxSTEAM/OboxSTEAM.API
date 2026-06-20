using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackgroundQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProgramEnrollments_Status_CreatedAt",
                table: "ProgramEnrollments",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_Status_StartDate",
                table: "Classes",
                columns: new[] { "Status", "StartDate" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ClassEnrollments_ClassId_Status",
                table: "ClassEnrollments",
                columns: new[] { "ClassId", "Status" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramEnrollments_Status_CreatedAt",
                table: "ProgramEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_Classes_Status_StartDate",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_ClassEnrollments_ClassId_Status",
                table: "ClassEnrollments");
        }
    }
}
