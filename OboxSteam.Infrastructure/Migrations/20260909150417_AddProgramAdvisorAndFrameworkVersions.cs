using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAdvisorAndFrameworkVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                table: "FrameworkRubricCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs");

            migrationBuilder.AddColumn<Guid>(
                name: "AdvisorExpertId",
                table: "Programs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FrameworkVersionId",
                table: "Programs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "ProgramFrameworks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ProgramFrameworks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceGuidance",
                table: "FrameworkRubricCriteria",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FrameworkVersionId",
                table: "FrameworkRubricCriteria",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramFrameworkVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AcademicGuidance = table.Column<string>(type: "text", nullable: true),
                    MinModules = table.Column<int>(type: "integer", nullable: true),
                    MinOfflineSessions = table.Column<int>(type: "integer", nullable: true),
                    MinLiveSessions = table.Column<int>(type: "integer", nullable: true),
                    RequireCapstoneResearchMilestone = table.Column<bool>(type: "boolean", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ProgramFrameworkVersions", x => x.Id);
                    table.CheckConstraint("CK_ProgramFrameworkVersions_MinLiveSessionsPositive", "\"MinLiveSessions\" IS NULL OR \"MinLiveSessions\" > 0");
                    table.CheckConstraint("CK_ProgramFrameworkVersions_MinModulesPositive", "\"MinModules\" IS NULL OR \"MinModules\" > 0");
                    table.CheckConstraint("CK_ProgramFrameworkVersions_MinOfflineSessionsPositive", "\"MinOfflineSessions\" IS NULL OR \"MinOfflineSessions\" > 0");
                    table.CheckConstraint("CK_ProgramFrameworkVersions_VersionPositive", "\"VersionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_ProgramFrameworkVersions_ProgramFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "ProgramFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Programs_AdvisorExpertId",
                table: "Programs",
                column: "AdvisorExpertId",
                filter: "\"IsDeleted\" = false AND \"AdvisorExpertId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkVersionId",
                table: "Programs",
                column: "FrameworkVersionId",
                filter: "\"IsDeleted\" = false AND \"FrameworkVersionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFrameworks_IsArchived",
                table: "ProgramFrameworks",
                column: "IsArchived",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRubricCriteria_FrameworkVersionId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkVersionId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFrameworkVersions_FrameworkId",
                table: "ProgramFrameworkVersions",
                column: "FrameworkId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"IsPublished\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFrameworkVersions_FrameworkId_VersionNumber",
                table: "ProgramFrameworkVersions",
                columns: new[] { "FrameworkId", "VersionNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworkVersions_FrameworkV~",
                table: "FrameworkRubricCriteria",
                column: "FrameworkVersionId",
                principalTable: "ProgramFrameworkVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkId",
                principalTable: "ProgramFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_Experts_AdvisorExpertId",
                table: "Programs",
                column: "AdvisorExpertId",
                principalTable: "Experts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_ProgramFrameworkVersions_FrameworkVersionId",
                table: "Programs",
                column: "FrameworkVersionId",
                principalTable: "ProgramFrameworkVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                principalTable: "ProgramFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworkVersions_FrameworkV~",
                table: "FrameworkRubricCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                table: "FrameworkRubricCriteria");

            migrationBuilder.DropForeignKey(
                name: "FK_Programs_Experts_AdvisorExpertId",
                table: "Programs");

            migrationBuilder.DropForeignKey(
                name: "FK_Programs_ProgramFrameworkVersions_FrameworkVersionId",
                table: "Programs");

            migrationBuilder.DropForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs");

            migrationBuilder.DropTable(
                name: "ProgramFrameworkVersions");

            migrationBuilder.DropIndex(
                name: "IX_Programs_AdvisorExpertId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkVersionId",
                table: "Programs");

            migrationBuilder.DropIndex(
                name: "IX_ProgramFrameworks_IsArchived",
                table: "ProgramFrameworks");

            migrationBuilder.DropIndex(
                name: "IX_FrameworkRubricCriteria_FrameworkVersionId",
                table: "FrameworkRubricCriteria");

            migrationBuilder.DropColumn(
                name: "AdvisorExpertId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "FrameworkVersionId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ProgramFrameworks");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ProgramFrameworks");

            migrationBuilder.DropColumn(
                name: "EvidenceGuidance",
                table: "FrameworkRubricCriteria");

            migrationBuilder.DropColumn(
                name: "FrameworkVersionId",
                table: "FrameworkRubricCriteria");

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkId",
                principalTable: "ProgramFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                principalTable: "ProgramFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
