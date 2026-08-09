using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioMediaSourceHighlightVideoItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaAssets_PortfolioId_SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets",
                columns: new[] { "PortfolioId", "SourceHighlightVideoItemId" },
                unique: true,
                filter: "\"SourceHighlightVideoItemId\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaAssets_SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets",
                column: "SourceHighlightVideoItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioMediaAssets_HighlightVideoItems_SourceHighlightVid~",
                table: "PortfolioMediaAssets",
                column: "SourceHighlightVideoItemId",
                principalTable: "HighlightVideoItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioMediaAssets_HighlightVideoItems_SourceHighlightVid~",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioMediaAssets_PortfolioId_SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioMediaAssets_SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropColumn(
                name: "SourceHighlightVideoItemId",
                table: "PortfolioMediaAssets");
        }
    }
}
