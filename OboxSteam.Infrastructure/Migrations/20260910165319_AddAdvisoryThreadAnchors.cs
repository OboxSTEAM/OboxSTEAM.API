using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisoryThreadAnchors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnchorField",
                table: "ProgramAdvisoryThreads",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnchorKind",
                table: "ProgramAdvisoryThreads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnchorQuote",
                table: "ProgramAdvisoryThreads",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorField",
                table: "ProgramAdvisoryThreads");

            migrationBuilder.DropColumn(
                name: "AnchorKind",
                table: "ProgramAdvisoryThreads");

            migrationBuilder.DropColumn(
                name: "AnchorQuote",
                table: "ProgramAdvisoryThreads");
        }
    }
}
