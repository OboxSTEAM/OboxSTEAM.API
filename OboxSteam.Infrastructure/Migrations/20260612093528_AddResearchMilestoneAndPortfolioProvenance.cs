using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResearchMilestoneAndPortfolioProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Portfolios_StudentId",
                table: "Portfolios");

            migrationBuilder.AddColumn<DateTime>(
                name: "GradedAt",
                table: "Submissions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResearchMilestoneId",
                table: "Submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ItemType",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "PortfolioCustomItems",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "MentorEndorsement",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleEnrollmentId",
                table: "PortfolioCustomItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                table: "PortfolioCustomItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramEnrollmentId",
                table: "PortfolioCustomItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProgramId",
                table: "PortfolioCustomItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: false,
                defaultValue: "AutoImported");

            migrationBuilder.Sql(
                """
                UPDATE "PortfolioCustomItems"
                SET "ItemType" = 'InternalCertificate'
                WHERE "ItemType" = 'InternalCert';
                """);

            migrationBuilder.AddColumn<string>(
                name: "StudentEditedBody",
                table: "PortfolioCustomItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmissionId",
                table: "PortfolioCustomItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PortfolioItemSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PortfolioCustomItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionTitle = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_PortfolioItemSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioItemSubmissions_PortfolioCustomItems_PortfolioCust~",
                        column: x => x.PortfolioCustomItemId,
                        principalTable: "PortfolioCustomItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortfolioItemSubmissions_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResearchMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    MilestoneOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCapstone = table.Column<bool>(type: "boolean", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ResearchMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchMilestones_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResearchMilestones_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResearchMilestoneActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResearchMilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequiredForSubmission = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ResearchMilestoneActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResearchMilestoneActivities_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResearchMilestoneActivities_ResearchMilestones_ResearchMile~",
                        column: x => x.ResearchMilestoneId,
                        principalTable: "ResearchMilestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Submissions_ResearchMilestoneId",
                table: "Submissions",
                column: "ResearchMilestoneId");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_StudentId",
                table: "Portfolios",
                column: "StudentId",
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ParentPortfolioId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCustomItems_ModuleEnrollmentId_ItemType",
                table: "PortfolioCustomItems",
                columns: new[] { "ModuleEnrollmentId", "ItemType" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"ModuleEnrollmentId\" IS NOT NULL AND \"ItemType\" = 'CapstoneProject'");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCustomItems_ModuleId",
                table: "PortfolioCustomItems",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCustomItems_ProgramEnrollmentId",
                table: "PortfolioCustomItems",
                column: "ProgramEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCustomItems_ProgramId",
                table: "PortfolioCustomItems",
                column: "ProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioCustomItems_SubmissionId",
                table: "PortfolioCustomItems",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItemSubmissions_PortfolioCustomItemId_SubmissionId",
                table: "PortfolioItemSubmissions",
                columns: new[] { "PortfolioCustomItemId", "SubmissionId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItemSubmissions_SubmissionId",
                table: "PortfolioItemSubmissions",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchMilestoneActivities_ActivityId",
                table: "ResearchMilestoneActivities",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchMilestoneActivities_ResearchMilestoneId_ActivityId",
                table: "ResearchMilestoneActivities",
                columns: new[] { "ResearchMilestoneId", "ActivityId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchMilestones_AssignmentId",
                table: "ResearchMilestones",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResearchMilestones_Code",
                table: "ResearchMilestones",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ResearchMilestones_ModuleId_MilestoneOrder",
                table: "ResearchMilestones",
                columns: new[] { "ModuleId", "MilestoneOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCustomItems_ModuleEnrollments_ModuleEnrollmentId",
                table: "PortfolioCustomItems",
                column: "ModuleEnrollmentId",
                principalTable: "ModuleEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCustomItems_Modules_ModuleId",
                table: "PortfolioCustomItems",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCustomItems_ProgramEnrollments_ProgramEnrollmentId",
                table: "PortfolioCustomItems",
                column: "ProgramEnrollmentId",
                principalTable: "ProgramEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCustomItems_Programs_ProgramId",
                table: "PortfolioCustomItems",
                column: "ProgramId",
                principalTable: "Programs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PortfolioCustomItems_Submissions_SubmissionId",
                table: "PortfolioCustomItems",
                column: "SubmissionId",
                principalTable: "Submissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Submissions_ResearchMilestones_ResearchMilestoneId",
                table: "Submissions",
                column: "ResearchMilestoneId",
                principalTable: "ResearchMilestones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCustomItems_ModuleEnrollments_ModuleEnrollmentId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCustomItems_Modules_ModuleId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCustomItems_ProgramEnrollments_ProgramEnrollmentId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCustomItems_Programs_ProgramId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PortfolioCustomItems_Submissions_SubmissionId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Submissions_ResearchMilestones_ResearchMilestoneId",
                table: "Submissions");

            migrationBuilder.DropTable(
                name: "PortfolioItemSubmissions");

            migrationBuilder.DropTable(
                name: "ResearchMilestoneActivities");

            migrationBuilder.DropTable(
                name: "ResearchMilestones");

            migrationBuilder.DropIndex(
                name: "IX_Submissions_ResearchMilestoneId",
                table: "Submissions");

            migrationBuilder.DropIndex(
                name: "IX_Portfolios_StudentId",
                table: "Portfolios");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCustomItems_ModuleEnrollmentId_ItemType",
                table: "PortfolioCustomItems");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCustomItems_ModuleId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCustomItems_ProgramEnrollmentId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCustomItems_ProgramId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropIndex(
                name: "IX_PortfolioCustomItems_SubmissionId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "GradedAt",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "ResearchMilestoneId",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "MentorEndorsement",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "ModuleEnrollmentId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "ProgramEnrollmentId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "ProgramId",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "StudentEditedBody",
                table: "PortfolioCustomItems");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "PortfolioCustomItems");

            migrationBuilder.AlterColumn<string>(
                name: "ItemType",
                table: "PortfolioCustomItems",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateIndex(
                name: "IX_Portfolios_StudentId",
                table: "Portfolios",
                column: "StudentId");
        }
    }
}
