using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "QuizQuestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "BankQuestionId",
                table: "QuizQuestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EasyPercent",
                table: "Assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HardPercent",
                table: "Assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "Assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediumPercent",
                table: "Assignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "QuestionBankId",
                table: "Assignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuestionCount",
                table: "Assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShuffleOptions",
                table: "Assignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitMinutes",
                table: "Assignments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionBanks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_QuestionBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionBankId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_BankQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankQuestions_QuestionBanks_QuestionBankId",
                        column: x => x.QuestionBankId,
                        principalTable: "QuestionBanks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BankQuestionOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_BankQuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankQuestionOptions_BankQuestions_BankQuestionId",
                        column: x => x.BankQuestionId,
                        principalTable: "BankQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuizQuestions_BankQuestionId",
                table: "QuizQuestions",
                column: "BankQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_QuestionBankId",
                table: "Assignments",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_BankQuestionOptions_BankQuestionId",
                table: "BankQuestionOptions",
                column: "BankQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_BankQuestions_QuestionBankId",
                table: "BankQuestions",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_CourseId_Name",
                table: "QuestionBanks",
                columns: new[] { "CourseId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_QuestionBanks_QuestionBankId",
                table: "Assignments",
                column: "QuestionBankId",
                principalTable: "QuestionBanks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizQuestions_BankQuestions_BankQuestionId",
                table: "QuizQuestions",
                column: "BankQuestionId",
                principalTable: "BankQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_QuestionBanks_QuestionBankId",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizQuestions_BankQuestions_BankQuestionId",
                table: "QuizQuestions");

            migrationBuilder.DropTable(
                name: "BankQuestionOptions");

            migrationBuilder.DropTable(
                name: "BankQuestions");

            migrationBuilder.DropTable(
                name: "QuestionBanks");

            migrationBuilder.DropIndex(
                name: "IX_QuizQuestions_BankQuestionId",
                table: "QuizQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_QuestionBankId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "BankQuestionId",
                table: "QuizQuestions");

            migrationBuilder.DropColumn(
                name: "EasyPercent",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "HardPercent",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "MediumPercent",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "QuestionBankId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "QuestionCount",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "ShuffleOptions",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "TimeLimitMinutes",
                table: "Assignments");
        }
    }
}
