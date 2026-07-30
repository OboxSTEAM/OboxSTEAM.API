using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMentorSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MentorSkills",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "MentorSkills",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "MentorSkills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MentorSkillEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentorSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CredentialId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_MentorSkillEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorSkillEvidences_MentorSkills_MentorSkillId",
                        column: x => x.MentorSkillId,
                        principalTable: "MentorSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorSkillEvidences_MentorSkillId",
                table: "MentorSkillEvidences",
                column: "MentorSkillId",
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorSkillEvidences");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MentorSkills");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "MentorSkills");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "MentorSkills");
        }
    }
}
