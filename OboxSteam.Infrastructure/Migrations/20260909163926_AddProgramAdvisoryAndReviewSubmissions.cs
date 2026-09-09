using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramAdvisoryAndReviewSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CriterionDescriptionSnapshot",
                table: "ReviewCriterionScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CriterionNameSnapshot",
                table: "ReviewCriterionScores",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceGuidanceSnapshot",
                table: "ReviewCriterionScores",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxScoreSnapshot",
                table: "ReviewCriterionScores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SnapshotAvailable",
                table: "CurriculumReviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "CurriculumReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryReads_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramReviewSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAdvisorExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurriculumSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    RubricSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyVersion = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ProgramReviewSubmissions", x => x.Id);
                    table.CheckConstraint("CK_ProgramReviewSubmissions_NumberPositive", "\"SubmissionNumber\" > 0");
                    table.ForeignKey(
                        name: "FK_ProgramReviewSubmissions_Experts_AssignedAdvisorExpertId",
                        column: x => x.AssignedAdvisorExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramReviewSubmissions_ProgramFrameworkVersions_Framework~",
                        column: x => x.FrameworkVersionId,
                        principalTable: "ProgramFrameworkVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramReviewSubmissions_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramReviewSubmissions_Users_SubmittedByManagerId",
                        column: x => x.SubmittedByManagerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetContext = table.Column<string>(type: "text", nullable: true),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreads_ProgramReviewSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "ProgramReviewSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreads_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreads_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramReviewDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvisorExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoresJson = table.Column<string>(type: "text", nullable: false),
                    OverallComment = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ProgramReviewDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramReviewDrafts_Experts_AdvisorExpertId",
                        column: x => x.AdvisorExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramReviewDrafts_ProgramReviewSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "ProgramReviewSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryMessages_ProgramAdvisoryThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ProgramAdvisoryThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryMessages_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_SubmissionId",
                table: "CurriculumReviews",
                column: "SubmissionId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"SubmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryMessages_AuthorUserId",
                table: "ProgramAdvisoryMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryMessages_ThreadId_CreatedAt",
                table: "ProgramAdvisoryMessages",
                columns: new[] { "ThreadId", "CreatedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReads_ProgramId_UserId",
                table: "ProgramAdvisoryReads",
                columns: new[] { "ProgramId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReads_UserId",
                table: "ProgramAdvisoryReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_AuthorUserId",
                table: "ProgramAdvisoryThreads",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_LastMessageAt",
                table: "ProgramAdvisoryThreads",
                columns: new[] { "ProgramId", "LastMessageAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_Type_Status",
                table: "ProgramAdvisoryThreads",
                columns: new[] { "ProgramId", "Type", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_SubmissionId",
                table: "ProgramAdvisoryThreads",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewDrafts_AdvisorExpertId",
                table: "ProgramReviewDrafts",
                column: "AdvisorExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewDrafts_SubmissionId_AdvisorExpertId",
                table: "ProgramReviewDrafts",
                columns: new[] { "SubmissionId", "AdvisorExpertId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewSubmissions_AssignedAdvisorExpertId",
                table: "ProgramReviewSubmissions",
                column: "AssignedAdvisorExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewSubmissions_FrameworkVersionId",
                table: "ProgramReviewSubmissions",
                column: "FrameworkVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewSubmissions_ProgramId",
                table: "ProgramReviewSubmissions",
                column: "ProgramId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewSubmissions_ProgramId_SubmissionNumber",
                table: "ProgramReviewSubmissions",
                columns: new[] { "ProgramId", "SubmissionNumber" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramReviewSubmissions_SubmittedByManagerId",
                table: "ProgramReviewSubmissions",
                column: "SubmittedByManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CurriculumReviews_ProgramReviewSubmissions_SubmissionId",
                table: "CurriculumReviews",
                column: "SubmissionId",
                principalTable: "ProgramReviewSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CurriculumReviews_ProgramReviewSubmissions_SubmissionId",
                table: "CurriculumReviews");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryMessages");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryReads");

            migrationBuilder.DropTable(
                name: "ProgramReviewDrafts");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryThreads");

            migrationBuilder.DropTable(
                name: "ProgramReviewSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumReviews_SubmissionId",
                table: "CurriculumReviews");

            migrationBuilder.DropColumn(
                name: "CriterionDescriptionSnapshot",
                table: "ReviewCriterionScores");

            migrationBuilder.DropColumn(
                name: "CriterionNameSnapshot",
                table: "ReviewCriterionScores");

            migrationBuilder.DropColumn(
                name: "EvidenceGuidanceSnapshot",
                table: "ReviewCriterionScores");

            migrationBuilder.DropColumn(
                name: "MaxScoreSnapshot",
                table: "ReviewCriterionScores");

            migrationBuilder.DropColumn(
                name: "SnapshotAvailable",
                table: "CurriculumReviews");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "CurriculumReviews");
        }
    }
}
