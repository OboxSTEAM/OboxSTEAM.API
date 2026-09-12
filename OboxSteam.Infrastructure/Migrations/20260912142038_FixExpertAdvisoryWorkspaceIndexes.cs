using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixExpertAdvisoryWorkspaceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_EventId_RecipientUserId",
                table: "ProgramAdvisoryNotificationIntents");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumReviewRequirements_ProgramId_ThreadId",
                table: "CurriculumReviewRequirements");

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
                name: "IX_ProgramAdvisoryNotificationIntents_EventId",
                table: "ProgramAdvisoryNotificationIntents",
                column: "EventId",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_ProgramId_ThreadId",
                table: "CurriculumReviewRequirements",
                columns: new[] { "ProgramId", "ThreadId" },
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_EventId",
                table: "ProgramAdvisoryNotificationIntents");

            migrationBuilder.DropIndex(
                name: "IX_CurriculumReviewRequirements_ProgramId_ThreadId",
                table: "CurriculumReviewRequirements");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryStreamReads_ProgramId_UserId_StreamType_Thre~",
                table: "ProgramAdvisoryStreamReads",
                columns: new[] { "ProgramId", "UserId", "StreamType", "ThreadId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryNotificationIntents_EventId_RecipientUserId",
                table: "ProgramAdvisoryNotificationIntents",
                columns: new[] { "EventId", "RecipientUserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumReviewRequirements_ProgramId_ThreadId",
                table: "CurriculumReviewRequirements",
                columns: new[] { "ProgramId", "ThreadId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
