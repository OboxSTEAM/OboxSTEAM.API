using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedFaceEmbeddingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "FaceEmbeddings");

            migrationBuilder.DropColumn(
                name: "SourceImageUrl",
                table: "FaceEmbeddings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Embedding",
                table: "FaceEmbeddings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceImageUrl",
                table: "FaceEmbeddings",
                type: "text",
                nullable: true);
        }
    }
}
