using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassIdToMediaAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "MediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ClassId",
                table: "MediaAssets",
                column: "ClassId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Classes_ClassId",
                table: "MediaAssets",
                column: "ClassId",
                principalTable: "Classes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Classes_ClassId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ClassId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "MediaAssets");
        }
    }
}
