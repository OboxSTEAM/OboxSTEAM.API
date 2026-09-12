using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumReviewClientOperationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientOperationId",
                table: "CurriculumReviews",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ProgramId_ClientOperationId",
                table: "CurriculumReviews",
                columns: new[] { "ProgramId", "ClientOperationId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ClientOperationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CurriculumReviews_ProgramId_ClientOperationId",
                table: "CurriculumReviews");

            migrationBuilder.DropColumn(
                name: "ClientOperationId",
                table: "CurriculumReviews");
        }
    }
}
