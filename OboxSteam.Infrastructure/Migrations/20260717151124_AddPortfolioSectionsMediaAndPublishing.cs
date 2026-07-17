using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioSectionsMediaAndPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "Portfolios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Portfolios",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasUnpublishedChanges",
                table: "Portfolios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPublishedAt",
                table: "Portfolios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedSnapshot",
                table: "Portfolios",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "PortfolioCustomItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                table: "PortfolioCustomItems",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Span",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortfolioMediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioMediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioMediaAssets_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ContentHtml = table.Column<string>(type: "text", nullable: true),
                    SettingsJson = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioSections_Portfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "Portfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioMediaPlacements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioMediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioCustomItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    PortfolioSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Caption = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioMediaPlacements", x => x.Id);
                    table.CheckConstraint("CK_PortfolioMediaPlacements_SingleOwner", "(\"PortfolioCustomItemId\" IS NOT NULL) <> (\"PortfolioSectionId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PortfolioMediaPlacements_PortfolioCustomItems_PortfolioCust~",
                        column: x => x.PortfolioCustomItemId,
                        principalTable: "PortfolioCustomItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortfolioMediaPlacements_PortfolioMediaAssets_PortfolioMedi~",
                        column: x => x.PortfolioMediaAssetId,
                        principalTable: "PortfolioMediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PortfolioMediaPlacements_PortfolioSections_PortfolioSection~",
                        column: x => x.PortfolioSectionId,
                        principalTable: "PortfolioSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaAssets_PortfolioId",
                table: "PortfolioMediaAssets",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaPlacements_PortfolioCustomItemId",
                table: "PortfolioMediaPlacements",
                column: "PortfolioCustomItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaPlacements_PortfolioMediaAssetId",
                table: "PortfolioMediaPlacements",
                column: "PortfolioMediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioMediaPlacements_PortfolioSectionId",
                table: "PortfolioMediaPlacements",
                column: "PortfolioSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSections_PortfolioId_DisplayOrder",
                table: "PortfolioSections",
                columns: new[] { "PortfolioId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSections_PortfolioId_Kind",
                table: "PortfolioSections",
                columns: new[] { "PortfolioId", "Kind" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Kind\" IN ('ProjectsGroup', 'ActivitiesGroup', 'LinksGroup')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioMediaPlacements");

            migrationBuilder.DropTable(
                name: "PortfolioMediaAssets");

            migrationBuilder.DropTable(
                name: "PortfolioSections");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "HasUnpublishedChanges",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "LastPublishedAt",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "PublishedSnapshot",
                table: "Portfolios");

            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "Span",
                table: "PortfolioCustomItems");
        }
    }
}
