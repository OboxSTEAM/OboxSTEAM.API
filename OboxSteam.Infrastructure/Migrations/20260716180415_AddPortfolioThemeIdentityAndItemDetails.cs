using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioThemeIdentityAndItemDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Portfolios_Subdomain",
                table: "Portfolios");

            migrationBuilder.AlterColumn<string>(
                name: "Subdomain",
                table: "Portfolios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                table: "Portfolios",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "Portfolios",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Headline",
                table: "Portfolios",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Links",
                table: "Portfolios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Summary",
                table: "Portfolios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tagline",
                table: "Portfolios",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThemeConfig",
                table: "Portfolios",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "PortfolioCustomItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUrl",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Organization",
                table: "PortfolioCustomItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "PortfolioCustomItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "PortfolioCustomItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_Subdomain",
                table: "Portfolios",
                column: "Subdomain",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Subdomain\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Portfolios_Subdomain",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Headline",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Links",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Summary",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "Tagline",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "ThemeConfig",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "ExternalUrl",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "Organization",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "PortfolioCustomItems");

            migrationBuilder.AlterColumn<string>(
                name: "Subdomain",
                table: "Portfolios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublic",
                table: "Portfolios",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_Subdomain",
                table: "Portfolios",
                column: "Subdomain",
                unique: true);
        }
    }
}
