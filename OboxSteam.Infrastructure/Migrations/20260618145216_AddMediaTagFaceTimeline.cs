using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaTagFaceTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FaceSegmentsJson",
                table: "MediaTags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasOtherFaces",
                table: "MediaTags",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceSegmentsJson",
                table: "MediaTags");

            migrationBuilder.DropColumn(
                name: "HasOtherFaces",
                table: "MediaTags");
        }
    }
}
