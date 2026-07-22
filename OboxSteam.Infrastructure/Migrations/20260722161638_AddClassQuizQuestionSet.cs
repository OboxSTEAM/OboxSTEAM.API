using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClassQuizQuestionSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassQuizQuestionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PulledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ClassQuizQuestionSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassQuizQuestionSets_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClassQuizQuestionSets_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassQuizQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassQuizQuestionSetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuestionText = table.Column<string>(type: "text", nullable: false),
                    QuestionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Points = table.Column<decimal>(type: "numeric", nullable: false),
                    DifficultyLevel = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_ClassQuizQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassQuizQuestions_ClassQuizQuestionSets_ClassQuizQuestionS~",
                        column: x => x.ClassQuizQuestionSetId,
                        principalTable: "ClassQuizQuestionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassQuizQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassQuizQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionText = table.Column<string>(type: "text", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_ClassQuizQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassQuizQuestionOptions_ClassQuizQuestions_ClassQuizQuesti~",
                        column: x => x.ClassQuizQuestionId,
                        principalTable: "ClassQuizQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassQuizQuestionOptions_ClassQuizQuestionId",
                table: "ClassQuizQuestionOptions",
                column: "ClassQuizQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassQuizQuestions_ClassQuizQuestionSetId",
                table: "ClassQuizQuestions",
                column: "ClassQuizQuestionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassQuizQuestionSets_AssignmentId",
                table: "ClassQuizQuestionSets",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassQuizQuestionSets_ClassId_AssignmentId",
                table: "ClassQuizQuestionSets",
                columns: new[] { "ClassId", "AssignmentId" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassQuizQuestionOptions");

            migrationBuilder.DropTable(
                name: "ClassQuizQuestions");

            migrationBuilder.DropTable(
                name: "ClassQuizQuestionSets");
        }
    }
}
