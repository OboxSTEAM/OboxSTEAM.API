using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateStudentProgramUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Certificates_StudentId",
                table: "Certificates");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_StudentId_ProgramId",
                table: "Certificates",
                columns: new[] { "StudentId", "ProgramId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ModuleId\" IS NULL AND \"ProgramId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Certificates_StudentId_ProgramId",
                table: "Certificates");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_StudentId",
                table: "Certificates",
                column: "StudentId");
        }
    }
}
