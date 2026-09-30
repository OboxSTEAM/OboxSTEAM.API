using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRubricAddFrameworkRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "ProgramAdvisoryDiscussionMessageReferences"
                WHERE "ReferenceId" IN (
                    SELECT "Id" FROM "ProgramAdvisoryReferences" WHERE "TargetType" = 'RubricCriterion');
                DELETE FROM "ProgramAdvisoryReferences" WHERE "TargetType" = 'RubricCriterion';
                """);

            migrationBuilder.DropTable(
                name: "ReviewCriterionScores");

            migrationBuilder.DropTable(
                name: "FrameworkRubricCriteria");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinLiveSessionsPositive",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinModulesPositive",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinOfflineSessionsPositive",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RubricSnapshotJson",
                table: "ProgramReviewSubmissions");

            migrationBuilder.DropColumn(
                name: "ScoresJson",
                table: "ProgramReviewDrafts");

            migrationBuilder.AddColumn<int>(
                name: "MaxActivityMinutes",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCoursesPerModule",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxModules",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxTotalHours",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinCoursesPerModule",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinDescriptionLength",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinLiveRatioPercent",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinMaterialsPerActivity",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinOfflineRatioPercent",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinSkillsGained",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinTotalHours",
                table: "ProgramFrameworkVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireActivityDuration",
                table: "ProgramFrameworkVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireAssignmentPassScore",
                table: "ProgramFrameworkVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireAssignmentPerModule",
                table: "ProgramFrameworkVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireCategoryMatch",
                table: "ProgramFrameworkVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireThumbnail",
                table: "ProgramFrameworkVersions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxActivityMinutesNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MaxActivityMinutes\" IS NULL OR \"MaxActivityMinutes\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxCoursesPerModuleNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MaxCoursesPerModule\" IS NULL OR \"MaxCoursesPerModule\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxModulesNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MaxModules\" IS NULL OR \"MaxModules\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxTotalHoursNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MaxTotalHours\" IS NULL OR \"MaxTotalHours\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinCoursesPerModuleNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinCoursesPerModule\" IS NULL OR \"MinCoursesPerModule\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinDescriptionLengthNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinDescriptionLength\" IS NULL OR \"MinDescriptionLength\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinLiveSessionsNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinLiveSessions\" IS NULL OR \"MinLiveSessions\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinMaterialsPerActivityNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinMaterialsPerActivity\" IS NULL OR \"MinMaterialsPerActivity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinModulesNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinModules\" IS NULL OR \"MinModules\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinOfflineSessionsNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinOfflineSessions\" IS NULL OR \"MinOfflineSessions\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinSkillsGainedNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinSkillsGained\" IS NULL OR \"MinSkillsGained\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinTotalHoursNonNegative",
                table: "ProgramFrameworkVersions",
                sql: "\"MinTotalHours\" IS NULL OR \"MinTotalHours\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_RatiosInRange",
                table: "ProgramFrameworkVersions",
                sql: "(\"MinOfflineRatioPercent\" IS NULL OR \"MinOfflineRatioPercent\" BETWEEN 0 AND 100) AND (\"MinLiveRatioPercent\" IS NULL OR \"MinLiveRatioPercent\" BETWEEN 0 AND 100)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxActivityMinutesNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxCoursesPerModuleNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxModulesNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MaxTotalHoursNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinCoursesPerModuleNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinDescriptionLengthNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinLiveSessionsNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinMaterialsPerActivityNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinModulesNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinOfflineSessionsNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinSkillsGainedNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinTotalHoursNonNegative",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramFrameworkVersions_RatiosInRange",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MaxActivityMinutes",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MaxCoursesPerModule",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MaxModules",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MaxTotalHours",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinCoursesPerModule",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinDescriptionLength",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinLiveRatioPercent",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinMaterialsPerActivity",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinOfflineRatioPercent",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinSkillsGained",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "MinTotalHours",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RequireActivityDuration",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RequireAssignmentPassScore",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RequireAssignmentPerModule",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RequireCategoryMatch",
                table: "ProgramFrameworkVersions");

            migrationBuilder.DropColumn(
                name: "RequireThumbnail",
                table: "ProgramFrameworkVersions");

            migrationBuilder.AddColumn<string>(
                name: "RubricSnapshotJson",
                table: "ProgramReviewSubmissions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ScoresJson",
                table: "ProgramReviewDrafts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "FrameworkRubricCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    EvidenceGuidance = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FrameworkRubricCriteria", x => x.Id);
                    table.CheckConstraint("CK_FrameworkRubricCriteria_MaxScorePositive", "\"MaxScore\" > 0");
                    table.ForeignKey(
                        name: "FK_FrameworkRubricCriteria_ProgramFrameworkVersions_FrameworkV~",
                        column: x => x.FrameworkVersionId,
                        principalTable: "ProgramFrameworkVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "ProgramFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkRubricCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionDescriptionSnapshot = table.Column<string>(type: "text", nullable: true),
                    CriterionNameSnapshot = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceGuidanceSnapshot = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    MaxScoreSnapshot = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCriterionScores", x => x.Id);
                    table.CheckConstraint("CK_ReviewCriterionScores_ScoreNonNegative", "\"Score\" >= 0");
                    table.ForeignKey(
                        name: "FK_ReviewCriterionScores_CurriculumReviews_CurriculumReviewId",
                        column: x => x.CurriculumReviewId,
                        principalTable: "CurriculumReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewCriterionScores_FrameworkRubricCriteria_FrameworkRubr~",
                        column: x => x.FrameworkRubricCriterionId,
                        principalTable: "FrameworkRubricCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinLiveSessionsPositive",
                table: "ProgramFrameworkVersions",
                sql: "\"MinLiveSessions\" IS NULL OR \"MinLiveSessions\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinModulesPositive",
                table: "ProgramFrameworkVersions",
                sql: "\"MinModules\" IS NULL OR \"MinModules\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramFrameworkVersions_MinOfflineSessionsPositive",
                table: "ProgramFrameworkVersions",
                sql: "\"MinOfflineSessions\" IS NULL OR \"MinOfflineSessions\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRubricCriteria_FrameworkId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRubricCriteria_FrameworkVersionId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkVersionId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCriterionScores_CurriculumReviewId_FrameworkRubricCri~",
                table: "ReviewCriterionScores",
                columns: new[] { "CurriculumReviewId", "FrameworkRubricCriterionId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCriterionScores_FrameworkRubricCriterionId",
                table: "ReviewCriterionScores",
                column: "FrameworkRubricCriterionId");
        }
    }
}
