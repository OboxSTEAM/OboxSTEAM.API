using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpertAdvisoryWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AdvisoryDiscussionSequence",
                table: "Programs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyVersion",
                table: "ProgramAdvisoryThreads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "LatestActivitySequence",
                table: "ProgramAdvisoryThreads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "StreamSequence",
                table: "ProgramAdvisoryMessages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "CurriculumReviewRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
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
                name: "ProgramAdvisoryDiscussionMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ClientMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryDiscussionMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionMessages_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionMessages_Users_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryNotificationIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationType = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProgramAdvisoryNotificationIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryNotificationIntents_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnchorKind = table.Column<string>(type: "text", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Quote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    QuotePrefix = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuoteSuffix = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CapturedLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CapturedExcerpt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryReferences_ProgramReviewSubmissions_Submissi~",
                        column: x => x.SubmissionId,
                        principalTable: "ProgramReviewSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryReferences_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryStreamReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamType = table.Column<string>(type: "text", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastReadSequence = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryStreamReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryStreamReads_ProgramAdvisoryThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ProgramAdvisoryThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryStreamReads_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryStreamReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryThreadEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriorStatus = table.Column<string>(type: "text", nullable: true),
                    NewStatus = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResolutionKind = table.Column<string>(type: "text", nullable: true),
                    VerifiedAgainstSubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionReferenceIdsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OperationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryDiscussionMessageReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryDiscussionMessageReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionMessageReferences_ProgramAdvisoryD~",
                        column: x => x.MessageId,
                        principalTable: "ProgramAdvisoryDiscussionMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionMessageReferences_ProgramAdvisoryR~",
                        column: x => x.ReferenceId,
                        principalTable: "ProgramAdvisoryReferences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_SubmissionId_LastMessageAt",
                table: "ProgramAdvisoryThreads",
                columns: new[] { "ProgramId", "SubmissionId", "LastMessageAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryMessages_ThreadId_StreamSequence",
                table: "ProgramAdvisoryMessages",
                columns: new[] { "ThreadId", "StreamSequence" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"StreamSequence\" > 0");

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
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_ThreadId",
                table: "CurriculumReviewRequirements",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessageReferences_MessageId_Ordinal",
                table: "ProgramAdvisoryDiscussionMessageReferences",
                columns: new[] { "MessageId", "Ordinal" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessageReferences_MessageId_Refere~",
                table: "ProgramAdvisoryDiscussionMessageReferences",
                columns: new[] { "MessageId", "ReferenceId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessageReferences_ReferenceId",
                table: "ProgramAdvisoryDiscussionMessageReferences",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_AuthorUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_AuthorUserId_Cl~",
                table: "ProgramAdvisoryDiscussionMessages",
                columns: new[] { "ProgramId", "AuthorUserId", "ClientMessageId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_Sequence",
                table: "ProgramAdvisoryDiscussionMessages",
                columns: new[] { "ProgramId", "Sequence" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_EventId_RecipientUserId",
                table: "ProgramAdvisoryNotificationIntents",
                columns: new[] { "EventId", "RecipientUserId" },
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
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads",
                columns: new[] { "ProgramId", "UserId", "StreamType", "ThreadId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ThreadId",
                table: "ProgramAdvisoryStreamReads",
                column: "ThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_UserId",
                table: "ProgramAdvisoryStreamReads",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurriculumReviewRequirements");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryDiscussionMessageReferences");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryNotificationIntents");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryThreadEvents");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropTable(
                name: "ProgramAdvisoryReferences");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryThreads_ProgramId_SubmissionId_LastMessageAt",
                table: "ProgramAdvisoryThreads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryMessages_ThreadId_StreamSequence",
                table: "ProgramAdvisoryMessages");

            migrationBuilder.DropColumn(
                name: "AdvisoryDiscussionSequence",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "ConcurrencyVersion",
                table: "ProgramAdvisoryThreads");

            migrationBuilder.DropColumn(
                name: "LatestActivitySequence",
                table: "ProgramAdvisoryThreads");

            migrationBuilder.DropColumn(
                name: "StreamSequence",
                table: "ProgramAdvisoryMessages");
        }
    }
}
