using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MediaAssetClassSessionScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ClassId is now required; remove media (and dependents) that cannot be scoped.
            migrationBuilder.Sql("""
                DELETE FROM "MediaTags"
                WHERE "MediaId" IN (SELECT "Id" FROM "MediaAssets" WHERE "ClassId" IS NULL);
                """);
            migrationBuilder.Sql("""
                DELETE FROM "SubmissionEvidences"
                WHERE "MediaId" IN (SELECT "Id" FROM "MediaAssets" WHERE "ClassId" IS NULL);
                """);
            migrationBuilder.Sql("""
                UPDATE "StudentSkillEvidences"
                SET "MediaAssetId" = NULL
                WHERE "MediaAssetId" IN (SELECT "Id" FROM "MediaAssets" WHERE "ClassId" IS NULL);
                """);
            migrationBuilder.Sql("""
                DELETE FROM "MediaAssets" WHERE "ClassId" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_Activities_ActivityId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ActivityId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ActivityId",
                table: "MediaAssets");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClassId",
                table: "MediaAssets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClassSessionId",
                table: "MediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ClassSessionId",
                table: "MediaAssets",
                column: "ClassSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_ClassSessions_ClassSessionId",
                table: "MediaAssets",
                column: "ClassSessionId",
                principalTable: "ClassSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_ClassSessions_ClassSessionId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_ClassSessionId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "ClassSessionId",
                table: "MediaAssets");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClassId",
                table: "MediaAssets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ActivityId",
                table: "MediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ActivityId",
                table: "MediaAssets",
                column: "ActivityId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_Activities_ActivityId",
                table: "MediaAssets",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id");
        }
    }
}
