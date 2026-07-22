using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HighlightVideoStackClassScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User-approved: discard program-scoped stacks before remapping to ClassId.
            migrationBuilder.Sql("""DELETE FROM "HighlightVideoItems";""");
            migrationBuilder.Sql("""DELETE FROM "HighlightVideoStacks";""");

            migrationBuilder.DropForeignKey(
                name: "FK_HighlightVideoStacks_Programs_ProgramId",
                table: "HighlightVideoStacks");

            migrationBuilder.RenameColumn(
                name: "ProgramId",
                table: "HighlightVideoStacks",
                newName: "ClassId");

            migrationBuilder.RenameIndex(
                name: "IX_HighlightVideoStacks_ProgramId_StudentId_StrengthDescription",
                table: "HighlightVideoStacks",
                newName: "IX_HighlightVideoStacks_ClassId_StudentId_StrengthDescription");

            migrationBuilder.AddForeignKey(
                name: "FK_HighlightVideoStacks_Classes_ClassId",
                table: "HighlightVideoStacks",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HighlightVideoStacks_Classes_ClassId",
                table: "HighlightVideoStacks");

            migrationBuilder.RenameColumn(
                name: "ClassId",
                table: "HighlightVideoStacks",
                newName: "ProgramId");

            migrationBuilder.RenameIndex(
                name: "IX_HighlightVideoStacks_ClassId_StudentId_StrengthDescription",
                table: "HighlightVideoStacks",
                newName: "IX_HighlightVideoStacks_ProgramId_StudentId_StrengthDescription");

            migrationBuilder.AddForeignKey(
                name: "FK_HighlightVideoStacks_Programs_ProgramId",
                table: "HighlightVideoStacks",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
