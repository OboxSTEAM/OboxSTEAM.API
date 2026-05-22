using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalVideoFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PersonalVideoJobRef",
                table: "HighlightVideos",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PersonalVideoRequestedAt",
                table: "HighlightVideos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PersonalVideoStatus",
                table: "HighlightVideos",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PersonalVideoJobRef",
                table: "HighlightVideos");

            migrationBuilder.DropColumn(
                name: "PersonalVideoRequestedAt",
                table: "HighlightVideos");

            migrationBuilder.DropColumn(
                name: "PersonalVideoStatus",
                table: "HighlightVideos");
        }
    }
}
