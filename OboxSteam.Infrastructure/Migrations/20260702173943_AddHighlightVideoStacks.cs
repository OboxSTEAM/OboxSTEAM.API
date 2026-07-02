using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHighlightVideoStacks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HighlightVideoStacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrengthDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
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
                    table.PrimaryKey("PK_HighlightVideoStacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HighlightVideoStacks_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HighlightVideoStacks_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HighlightVideoItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StackId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    GenerationKind = table.Column<string>(type: "text", nullable: false),
                    VideoUrl = table.Column<string>(type: "text", nullable: true),
                    OutputS3Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    PersonalVideoJobRef = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TrimDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TrimExcludeRangesJson = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_HighlightVideoItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HighlightVideoItems_HighlightVideoItems_ParentItemId",
                        column: x => x.ParentItemId,
                        principalTable: "HighlightVideoItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HighlightVideoItems_HighlightVideoStacks_StackId",
                        column: x => x.StackId,
                        principalTable: "HighlightVideoStacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideoItems_ParentItemId",
                table: "HighlightVideoItems",
                column: "ParentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideoItems_StackId",
                table: "HighlightVideoItems",
                column: "StackId");

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideoStacks_ProgramId_StudentId_StrengthDescription",
                table: "HighlightVideoStacks",
                columns: new[] { "ProgramId", "StudentId", "StrengthDescription" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_HighlightVideoStacks_StudentId",
                table: "HighlightVideoStacks",
                column: "StudentId");

            // Migrate legacy HighlightVideos rows into default (no-spec) stacks + initial items.
            migrationBuilder.Sql(
                """
                INSERT INTO "HighlightVideoStacks" (
                    "Id", "ProgramId", "StudentId", "StrengthDescription",
                    "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "DeletedAt", "DeletedBy")
                SELECT
                    gen_random_uuid(),
                    hv."ProgramId",
                    hv."StudentId",
                    '',
                    hv."IsDeleted",
                    hv."CreatedAt",
                    hv."CreatedBy",
                    hv."UpdatedAt",
                    hv."UpdatedBy",
                    hv."DeletedAt",
                    hv."DeletedBy"
                FROM "HighlightVideos" hv
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "HighlightVideoStacks" s
                    WHERE s."ProgramId" = hv."ProgramId"
                      AND s."StudentId" = hv."StudentId"
                      AND s."StrengthDescription" = ''
                      AND s."IsDeleted" = false)
                  AND hv."IsDeleted" = false;

                INSERT INTO "HighlightVideoItems" (
                    "Id", "StackId", "ParentItemId", "GenerationKind", "VideoUrl", "OutputS3Key", "DurationMs",
                    "PersonalVideoJobRef", "Status", "RequestedAt", "FailureReason", "TrimDescription", "TrimExcludeRangesJson",
                    "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "DeletedAt", "DeletedBy")
                SELECT
                    hv."Id",
                    s."Id",
                    NULL,
                    'Initial',
                    hv."VideoUrl",
                    NULL,
                    NULL,
                    hv."PersonalVideoJobRef",
                    hv."PersonalVideoStatus",
                    hv."PersonalVideoRequestedAt",
                    hv."PersonalVideoFailureReason",
                    NULL,
                    NULL,
                    hv."IsDeleted",
                    hv."CreatedAt",
                    hv."CreatedBy",
                    hv."UpdatedAt",
                    hv."UpdatedBy",
                    hv."DeletedAt",
                    hv."DeletedBy"
                FROM "HighlightVideos" hv
                INNER JOIN "HighlightVideoStacks" s
                    ON s."ProgramId" = hv."ProgramId"
                   AND s."StudentId" = hv."StudentId"
                   AND s."StrengthDescription" = ''
                   AND s."IsDeleted" = false
                WHERE NOT EXISTS (
                    SELECT 1 FROM "HighlightVideoItems" i WHERE i."Id" = hv."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HighlightVideoItems");

            migrationBuilder.DropTable(
                name: "HighlightVideoStacks");
        }
    }
}
