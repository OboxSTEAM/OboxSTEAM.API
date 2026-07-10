using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillCatalogAndStudentSkillEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy free-text StudentSkills rows cannot map to SkillId; table was unused by app services.
            migrationBuilder.Sql("DELETE FROM \"StudentSkills\";");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentSkills_Users_StudentId",
                table: "StudentSkills");

            migrationBuilder.DropIndex(
                name: "IX_StudentSkills_StudentId",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "SkillName",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "SkillType",
                table: "StudentSkills");

            migrationBuilder.AlterColumn<string>(
                name: "ProficiencyLevel",
                table: "StudentSkills",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<decimal>(
                name: "ConfidenceScore",
                table: "StudentSkills",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceSummary",
                table: "StudentSkills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAssessedAt",
                table: "StudentSkills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reasoning",
                table: "StudentSkills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SkillId",
                table: "StudentSkills",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "StudentSkills",
                type: "text",
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "StudentSkills",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedBy",
                table: "StudentSkills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    Subcategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentSkillEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_StudentSkillEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSkillEvidences_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentSkillEvidences_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentSkillEvidences_StudentSkills_StudentSkillId",
                        column: x => x.StudentSkillId,
                        principalTable: "StudentSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentSkillEvidences_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkills_SkillId",
                table: "StudentSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkills_StudentId_SkillId",
                table: "StudentSkills",
                columns: new[] { "StudentId", "SkillId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkills_VerifiedBy",
                table: "StudentSkills",
                column: "VerifiedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Code",
                table: "Skills",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_CertificateId",
                table: "StudentSkillEvidences",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_MediaAssetId",
                table: "StudentSkillEvidences",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_StudentSkillId_CertificateId",
                table: "StudentSkillEvidences",
                columns: new[] { "StudentSkillId", "CertificateId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"CertificateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_StudentSkillId_MediaAssetId",
                table: "StudentSkillEvidences",
                columns: new[] { "StudentSkillId", "MediaAssetId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"MediaAssetId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_StudentSkillId_SubmissionId",
                table: "StudentSkillEvidences",
                columns: new[] { "StudentSkillId", "SubmissionId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"SubmissionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkillEvidences_SubmissionId",
                table: "StudentSkillEvidences",
                column: "SubmissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentSkills_Skills_SkillId",
                table: "StudentSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentSkills_Users_StudentId",
                table: "StudentSkills",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentSkills_Users_VerifiedBy",
                table: "StudentSkills",
                column: "VerifiedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentSkills_Skills_SkillId",
                table: "StudentSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentSkills_Users_StudentId",
                table: "StudentSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentSkills_Users_VerifiedBy",
                table: "StudentSkills");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "StudentSkillEvidences");

            migrationBuilder.DropIndex(
                name: "IX_StudentSkills_SkillId",
                table: "StudentSkills");

            migrationBuilder.DropIndex(
                name: "IX_StudentSkills_StudentId_SkillId",
                table: "StudentSkills");

            migrationBuilder.DropIndex(
                name: "IX_StudentSkills_VerifiedBy",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "ConfidenceScore",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "EvidenceSummary",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "LastAssessedAt",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "Reasoning",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "SkillId",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "StudentSkills");

            migrationBuilder.DropColumn(
                name: "VerifiedBy",
                table: "StudentSkills");

            migrationBuilder.AlterColumn<int>(
                name: "ProficiencyLevel",
                table: "StudentSkills",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "SkillName",
                table: "StudentSkills",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkillType",
                table: "StudentSkills",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSkills_StudentId",
                table: "StudentSkills",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentSkills_Users_StudentId",
                table: "StudentSkills",
                column: "StudentId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
