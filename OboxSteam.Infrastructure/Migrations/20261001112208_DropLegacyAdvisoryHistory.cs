using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyAdvisoryHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove legacy rows whose enum strings or columns disappear below.
            migrationBuilder.Sql("""
                DELETE FROM "ProgramAdvisoryDiscussionMessageReferences" mr
                USING "ProgramAdvisoryReferences" r
                WHERE mr."ReferenceId" = r."Id"
                  AND (r."Context" = 'Submission' OR r."TargetType" = 'RubricCriterion');
                """);
            migrationBuilder.Sql("""
                DELETE FROM "ProgramAdvisoryReferences" r
                WHERE r."Context" = 'Submission'
                   OR r."TargetType" = 'RubricCriterion'
                   OR NOT EXISTS (
                       SELECT 1 FROM "ProgramAdvisoryDiscussionMessageReferences" mr
                       WHERE mr."ReferenceId" = r."Id");
                """);
            migrationBuilder.Sql("""
                DELETE FROM "ProgramAdvisoryStreamReads"
                WHERE "StreamType" <> 'Discussion' OR "ThreadId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramAdvisoryReferences_ProgramReviewSubmissions_Submissi~",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgramAdvisoryStreamReads_ProgramAdvisoryThreads_ThreadId",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropTable(
                name: "CurriculumReviewRequirements");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryMessages");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryNotificationIntents");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryReads");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryThreadEvents");

            migrationBuilder.DropTable(
                name: "ProgramReviewDrafts");

            migrationBuilder.DropTable(
                name: "CurriculumReviews");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryThreads");

            migrationBuilder.DropTable(
                name: "ProgramReviewSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ThreadId",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_Context_SubmissionId_Ta~",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_SubmissionId",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryReferences_SubmissionId",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.DropColumn(
                name: "StreamType",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropColumn(
                name: "Context",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId",
                table: "ProgramAdvisoryStreamReads",
                columns: new[] { "ProgramId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_TargetType_TargetId",
                table: "ProgramAdvisoryReferences",
                columns: new[] { "ProgramId", "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_TargetType_TargetId",
                table: "ProgramAdvisoryReferences");

            migrationBuilder.AddColumn<string>(
                name: "StreamType",
                table: "ProgramAdvisoryStreamReads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ThreadId",
                table: "ProgramAdvisoryStreamReads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Context",
                table: "ProgramAdvisoryReferences",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "ProgramAdvisoryReferences",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryNotificationIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramAdvisoryNotificationIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryNotificationIntents_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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
                    AssignedAdvisorExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByManagerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumSnapshotJson = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewRoundIntent = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    SubmissionNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "CurriculumReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientOperationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "text", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: false),
                    SnapshotAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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
                        name: "FK_CurriculumReviews_ProgramReviewSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "ProgramReviewSubmissions",
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
                name: "ProgramAdvisoryThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnchorField = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AnchorKind = table.Column<string>(type: "text", nullable: true),
                    AnchorQuote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestActivitySequence = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetContext = table.Column<string>(type: "text", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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
                    AdvisorExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConcurrencyVersion = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    LastSavedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OverallComment = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "CurriculumReviewRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurriculumReviewRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumReviewRequirements_CurriculumReviews_CurriculumRe~",
                        column: x => x.CurriculumReviewId,
                        principalTable: "CurriculumReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CurriculumReviewRequirements_ProgramAdvisoryThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ProgramAdvisoryThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumReviewRequirements_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    StreamSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryThreadEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    VerifiedAgainstSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionReferenceIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: true),
                    OperationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PriorStatus = table.Column<string>(type: "text", nullable: true),
                    ResolutionKind = table.Column<string>(type: "text", nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramAdvisoryThreadEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreadEvents_ProgramAdvisoryThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ProgramAdvisoryThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreadEvents_ProgramReviewSubmissions_Verifi~",
                        column: x => x.VerifiedAgainstSubmissionId,
                        principalTable: "ProgramReviewSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreadEvents_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryThreadEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType",
                table: "ProgramAdvisoryStreamReads",
                columns: new[] { "ProgramId", "UserId", "StreamType" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ThreadId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads",
                columns: new[] { "ProgramId", "UserId", "StreamType", "ThreadId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ThreadId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ThreadId",
                table: "ProgramAdvisoryStreamReads",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_Context_SubmissionId_Ta~",
                table: "ProgramAdvisoryReferences",
                columns: new[] { "ProgramId", "Context", "SubmissionId", "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReferences_ProgramId_SubmissionId",
                table: "ProgramAdvisoryReferences",
                columns: new[] { "ProgramId", "SubmissionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryReferences_SubmissionId",
                table: "ProgramAdvisoryReferences",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_CurriculumReviewId_ThreadId",
                table: "CurriculumReviewRequirements",
                columns: new[] { "CurriculumReviewId", "ThreadId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_ProgramId_ThreadId",
                table: "CurriculumReviewRequirements",
                columns: new[] { "ProgramId", "ThreadId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_ThreadId",
                table: "CurriculumReviewRequirements",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ExpertId",
                table: "CurriculumReviews",
                column: "ExpertId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ProgramId_ClientOperationId",
                table: "CurriculumReviews",
                columns: new[] { "ProgramId", "ClientOperationId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ClientOperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviews_ProgramId_Round",
                table: "CurriculumReviews",
                columns: new[] { "ProgramId", "Round" },
                unique: true,
                filter: "\"IsDeleted\" = false");

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
                name: "IX_ProgramAdvisoryMessages_ThreadId_StreamSequence",
                table: "ProgramAdvisoryMessages",
                columns: new[] { "ThreadId", "StreamSequence" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"StreamSequence\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_EventId",
                table: "ProgramAdvisoryNotificationIntents",
                column: "EventId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_ProgramId",
                table: "ProgramAdvisoryNotificationIntents",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_Status_NextAttemptAt",
                table: "ProgramAdvisoryNotificationIntents",
                columns: new[] { "Status", "NextAttemptAt" },
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
                name: "IX_ProgramAdvisoryThreadEvents_ActorUserId",
                table: "ProgramAdvisoryThreadEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreadEvents_OperationId",
                table: "ProgramAdvisoryThreadEvents",
                column: "OperationId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"OperationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreadEvents_ProgramId",
                table: "ProgramAdvisoryThreadEvents",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreadEvents_ThreadId_Sequence",
                table: "ProgramAdvisoryThreadEvents",
                columns: new[] { "ThreadId", "Sequence" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreadEvents_VerifiedAgainstSubmissionId",
                table: "ProgramAdvisoryThreadEvents",
                column: "VerifiedAgainstSubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_AuthorUserId",
                table: "ProgramAdvisoryThreads",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_OneGeneralPerProgram",
                table: "ProgramAdvisoryThreads",
                column: "ProgramId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Type\" = 'General'");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_LastMessageAt",
                table: "ProgramAdvisoryThreads",
                columns: new[] { "ProgramId", "LastMessageAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_SubmissionId_LastMessageAt",
                table: "ProgramAdvisoryThreads",
                columns: new[] { "ProgramId", "SubmissionId", "LastMessageAt" },
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
                name: "FK_ProgramAdvisoryReferences_ProgramReviewSubmissions_Submissi~",
                table: "ProgramAdvisoryReferences",
                column: "SubmissionId",
                principalTable: "ProgramReviewSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgramAdvisoryStreamReads_ProgramAdvisoryThreads_ThreadId",
                table: "ProgramAdvisoryStreamReads",
                column: "ThreadId",
                principalTable: "ProgramAdvisoryThreads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
