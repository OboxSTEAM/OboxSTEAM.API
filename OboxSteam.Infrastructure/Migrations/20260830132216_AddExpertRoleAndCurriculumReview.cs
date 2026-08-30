using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertRoleAndCurriculumReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FrameworkId",
                table: "Programs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassSessionExperts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    MentorFeedback = table.Column<string>(type: "text", nullable: true),
                    MentorFeedbackRating = table.Column<int>(type: "integer", nullable: true),
                    MentorFeedbackAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_ClassSessionExperts", x => x.Id);
                    table.CheckConstraint("CK_ClassSessionExperts_MentorFeedbackRatingRange", "\"MentorFeedbackRating\" IS NULL OR (\"MentorFeedbackRating\" BETWEEN 1 AND 5)");
                    table.ForeignKey(
                        name: "FK_ClassSessionExperts_ClassSessions_ClassSessionId",
                        column: x => x.ClassSessionId,
                        principalTable: "ClassSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassSessionExperts_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_CurriculumReviews", x => x.Id);
                    table.CheckConstraint("CK_CurriculumReviews_RoundPositive", "\"Round\" > 0");
                    table.ForeignKey(
                        name: "FK_CurriculumReviews_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumReviews_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramFrameworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    MinModules = table.Column<int>(type: "integer", nullable: true),
                    MinOfflineSessions = table.Column<int>(type: "integer", nullable: true),
                    MinLiveSessions = table.Column<int>(type: "integer", nullable: true),
                    RequireFinalAssessment = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_ProgramFrameworks", x => x.Id);
                    table.CheckConstraint("CK_ProgramFrameworks_MinLiveSessionsPositive", "\"MinLiveSessions\" IS NULL OR \"MinLiveSessions\" > 0");
                    table.CheckConstraint("CK_ProgramFrameworks_MinModulesPositive", "\"MinModules\" IS NULL OR \"MinModules\" > 0");
                    table.CheckConstraint("CK_ProgramFrameworks_MinOfflineSessionsPositive", "\"MinOfflineSessions\" IS NULL OR \"MinOfflineSessions\" > 0");
                    table.ForeignKey(
                        name: "FK_ProgramFrameworks_Experts_ExpertId",
                        column: x => x.ExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FrameworkRubricCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_FrameworkRubricCriteria", x => x.Id);
                    table.CheckConstraint("CK_FrameworkRubricCriteria_MaxScorePositive", "\"MaxScore\" > 0");
                    table.ForeignKey(
                        name: "FK_FrameworkRubricCriteria_ProgramFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "ProgramFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewCriterionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkRubricCriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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

            migrationBuilder.CreateIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessionExperts_ClassSessionId_ExpertId",
                table: "ClassSessionExperts",
                columns: new[] { "ClassSessionId", "ExpertId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Invited', 'Accepted')");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSessionExperts_ExpertId_Status",
                table: "ClassSessionExperts",
                columns: new[] { "ExpertId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ExpertId",
                table: "CurriculumReviews",
                column: "ExpertId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ProgramId_Round",
                table: "CurriculumReviews",
                columns: new[] { "ProgramId", "Round" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_FrameworkRubricCriteria_FrameworkId",
                table: "FrameworkRubricCriteria",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFrameworks_Category",
                table: "ProgramFrameworks",
                column: "Category",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramFrameworks_ExpertId",
                table: "ProgramFrameworks",
                column: "ExpertId",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs",
                column: "FrameworkId",
                principalTable: "ProgramFrameworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Programs_ProgramFrameworks_FrameworkId",
                table: "Programs");

            migrationBuilder.DropTable(
                name: "ClassSessionExperts");

            migrationBuilder.DropTable(
                name: "ReviewCriterionScores");

            migrationBuilder.DropTable(
                name: "CurriculumReviews");

            migrationBuilder.DropTable(
                name: "FrameworkRubricCriteria");

            migrationBuilder.DropTable(
                name: "ProgramFrameworks");

            migrationBuilder.DropIndex(
                name: "IX_Programs_FrameworkId",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "FrameworkId",
                table: "Programs");
        }
    }
}
