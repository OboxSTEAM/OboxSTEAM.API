using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvisoryChatModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CurriculumVersion",
                table: "Programs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "AddressedAt",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AddressedByUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "text",
                nullable: false,
                defaultValue: "User");

            migrationBuilder.AddColumn<string>(
                name: "PinStatus",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PinnedByUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RemovedAt",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RemovedByUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResolvedByUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemEventCode",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemEventPayloadJson",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramAdvisoryDiscussionAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploaderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
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
                    table.PrimaryKey("PK_ProgramAdvisoryDiscussionAttachments", x => x.Id);
                    table.CheckConstraint("CK_ProgramAdvisoryDiscussionAttachments_SizeNonNegative", "\"SizeBytes\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionAttachments_ProgramAdvisoryDiscuss~",
                        column: x => x.MessageId,
                        principalTable: "ProgramAdvisoryDiscussionMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionAttachments_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramAdvisoryDiscussionAttachments_Users_UploaderUserId",
                        column: x => x.UploaderUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumVersion = table.Column<long>(type: "bigint", nullable: false),
                    FromVersion = table.Column<long>(type: "bigint", nullable: false),
                    FrameworkVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FrameworkCheckJson = table.Column<string>(type: "jsonb", nullable: false),
                    CurriculumSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    ApprovedByExpertId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokeReason = table.Column<string>(type: "text", nullable: true),
                    RevokeComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_ProgramApprovals", x => x.Id);
                    table.CheckConstraint("CK_ProgramApprovals_VersionRange", "\"CurriculumVersion\" >= 0 AND \"FromVersion\" >= 0 AND \"FromVersion\" <= \"CurriculumVersion\"");
                    table.ForeignKey(
                        name: "FK_ProgramApprovals_Experts_ApprovedByExpertId",
                        column: x => x.ApprovedByExpertId,
                        principalTable: "Experts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramApprovals_ProgramFrameworkVersions_FrameworkVersionId",
                        column: x => x.FrameworkVersionId,
                        principalTable: "ProgramFrameworkVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramApprovals_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProgramApprovals_Users_RevokedByUserId",
                        column: x => x.RevokedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_PinStatus",
                table: "ProgramAdvisoryDiscussionMessages",
                columns: new[] { "ProgramId", "PinStatus" },
                filter: "\"IsDeleted\" = false AND \"PinStatus\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_SystemEventCode~",
                table: "ProgramAdvisoryDiscussionMessages",
                columns: new[] { "ProgramId", "SystemEventCode", "CreatedAt" },
                filter: "\"IsDeleted\" = false AND \"SystemEventCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionAttachments_MessageId",
                table: "ProgramAdvisoryDiscussionAttachments",
                column: "MessageId",
                filter: "\"IsDeleted\" = false AND \"MessageId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionAttachments_ProgramId",
                table: "ProgramAdvisoryDiscussionAttachments",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionAttachments_Unsent",
                table: "ProgramAdvisoryDiscussionAttachments",
                column: "CreatedAt",
                filter: "\"IsDeleted\" = false AND \"MessageId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramAdvisoryDiscussionAttachments_UploaderUserId",
                table: "ProgramAdvisoryDiscussionAttachments",
                column: "UploaderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramApprovals_ApprovedByExpertId",
                table: "ProgramApprovals",
                column: "ApprovedByExpertId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramApprovals_FrameworkVersionId",
                table: "ProgramApprovals",
                column: "FrameworkVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramApprovals_OneActivePerProgram",
                table: "ProgramApprovals",
                column: "ProgramId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramApprovals_ProgramId_ApprovedAt",
                table: "ProgramApprovals",
                columns: new[] { "ProgramId", "ApprovedAt" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramApprovals_RevokedByUserId",
                table: "ProgramApprovals",
                column: "RevokedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramAdvisoryDiscussionAttachments");

            migrationBuilder.DropTable(
                name: "ProgramApprovals");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_PinStatus",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropIndex(
                name: "IX_ProgramAdvisoryDiscussionMessages_ProgramId_SystemEventCode~",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "CurriculumVersion",
                table: "Programs");

            migrationBuilder.DropColumn(
                name: "AddressedAt",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "AddressedByUserId",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "PinStatus",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "PinnedByUserId",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "RemovedAt",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "RemovedByUserId",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "SystemEventCode",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.DropColumn(
                name: "SystemEventPayloadJson",
                table: "ProgramAdvisoryDiscussionMessages");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorUserId",
                table: "ProgramAdvisoryDiscussionMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
