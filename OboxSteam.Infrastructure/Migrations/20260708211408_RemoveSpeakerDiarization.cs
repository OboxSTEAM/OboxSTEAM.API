using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSpeakerDiarization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MappedSpeakerLabel",
                table: "MediaTags");

            migrationBuilder.DropColumn(
                name: "VoiceSegmentsJson",
                table: "MediaTags");

            migrationBuilder.DropColumn(
                name: "SpeakerSegmentsJson",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "TranscribeJobName",
                table: "MediaAssets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MappedSpeakerLabel",
                table: "MediaTags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoiceSegmentsJson",
                table: "MediaTags",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpeakerSegmentsJson",
                table: "MediaAssets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TranscribeJobName",
                table: "MediaAssets",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }
    }
}
