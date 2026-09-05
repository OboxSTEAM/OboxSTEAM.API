using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRequireFinalAssessmentToRequireCapstoneResearchMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequireFinalAssessment",
                table: "ProgramFrameworks",
                newName: "RequireCapstoneResearchMilestone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RequireCapstoneResearchMilestone",
                table: "ProgramFrameworks",
                newName: "RequireFinalAssessment");
        }
    }
}
