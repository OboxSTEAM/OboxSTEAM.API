using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PortfolioMediaVideoAndSourceMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceMediaAssetId",
                table: "PortfolioMediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaAssets_PortfolioId_SourceMediaAssetId",
                table: "PortfolioMediaAssets",
                columns: new[] { "PortfolioId", "SourceMediaAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaAssets_SourceMediaAssetId",
                table: "PortfolioMediaAssets",
                column: "SourceMediaAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioMediaAssets_MediaAssets_SourceMediaAssetId",
                table: "PortfolioMediaAssets",
                column: "SourceMediaAssetId",
                principalTable: "MediaAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioMediaAssets_MediaAssets_SourceMediaAssetId",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioMediaAssets_PortfolioId_SourceMediaAssetId",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioMediaAssets_SourceMediaAssetId",
                table: "PortfolioMediaAssets");

            migrationBuilder.DropColumn(
                name: "SourceMediaAssetId",
                table: "PortfolioMediaAssets");
        }
    }
}
