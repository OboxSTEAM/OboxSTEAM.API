using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorVideoJobRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VideoJobRef",
                table: "MediaAssets",
                newName: "RawVideoS3Key");

            migrationBuilder.AddColumn<string>(
                name: "FaceSearchJobId",
                table: "MediaAssets",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LabelJobRef",
                table: "MediaAssets",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaConvertJobId",
                table: "MediaAssets",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FaceSearchJobId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "LabelJobRef",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "MediaConvertJobId",
                table: "MediaAssets");

            migrationBuilder.RenameColumn(
                name: "RawVideoS3Key",
                table: "MediaAssets",
                newName: "VideoJobRef");
        }
    }
}
