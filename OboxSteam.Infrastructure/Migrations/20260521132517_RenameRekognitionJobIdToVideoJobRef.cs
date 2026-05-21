using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameRekognitionJobIdToVideoJobRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RekognitionJobId",
                table: "MediaAssets",
                newName: "VideoJobRef");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VideoJobRef",
                table: "MediaAssets",
                newName: "RekognitionJobId");
        }
    }
}
