using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHighlightVideoFailureReasonAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HighlightVideos_ProgramId",
                table: "HighlightVideos");

            migrationBuilder.AddColumn<string>(
                name: "PersonalVideoFailureReason",
                table: "HighlightVideos",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideos_ProgramId_StudentId",
                table: "HighlightVideos",
                columns: new[] { "ProgramId", "StudentId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HighlightVideos_ProgramId_StudentId",
                table: "HighlightVideos");

            migrationBuilder.DropColumn(
                name: "PersonalVideoFailureReason",
                table: "HighlightVideos");

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideos_ProgramId",
                table: "HighlightVideos",
                column: "ProgramId");
        }
    }
}
