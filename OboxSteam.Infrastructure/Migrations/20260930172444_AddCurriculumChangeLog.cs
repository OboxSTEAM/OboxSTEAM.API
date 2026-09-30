using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurriculumChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CurriculumChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    At = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeKind = table.Column<string>(type: "text", nullable: false),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ParentBefore = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentAfter = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentBeforeLabel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentAfterLabel = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderBefore = table.Column<int>(type: "integer", nullable: true),
                    OrderAfter = table.Column<int>(type: "integer", nullable: true),
                    LabelSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PathSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
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
                    table.PrimaryKey("PK_CurriculumChanges", x => x.Id);
                    table.CheckConstraint("CK_CurriculumChanges_VersionPositive", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_CurriculumChanges_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CurriculumChangeSeens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeenVersion = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_CurriculumChangeSeens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CurriculumChangeSeens_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CurriculumChangeSeens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumChanges_ProgramId_TargetType_TargetId",
                table: "CurriculumChanges",
                columns: new[] { "ProgramId", "TargetType", "TargetId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumChanges_ProgramId_Version",
                table: "CurriculumChanges",
                columns: new[] { "ProgramId", "Version" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumChangeSeens_ProgramId_UserId",
                table: "CurriculumChangeSeens",
                columns: new[] { "ProgramId", "UserId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_CurriculumChangeSeens_UserId",
                table: "CurriculumChangeSeens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CurriculumChanges");

            migrationBuilder.DropTable(
                name: "CurriculumChangeSeens");
        }
    }
}
