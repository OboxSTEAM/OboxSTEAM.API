using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MaterialActivityOnlyOneToOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove module/course-level materials and duplicate activity rows before tightening schema.
            migrationBuilder.Sql("""
                DELETE FROM "Materials" WHERE "ActivityId" IS NULL;
                DELETE FROM "Materials" m1
                USING "Materials" m2
                WHERE m1."ActivityId" = m2."ActivityId"
                  AND m1."Id" > m2."Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Activities_ActivityId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Courses_CourseId",
                table: "Materials");

            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Modules_ModuleId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ActivityId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_CourseId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ModuleId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "Materials");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActivityId",
                table: "Materials",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ActivityId",
                table: "Materials",
                column: "ActivityId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Activities_ActivityId",
                table: "Materials",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Materials_Activities_ActivityId",
                table: "Materials");

            migrationBuilder.DropIndex(
                name: "IX_Materials_ActivityId",
                table: "Materials");

            migrationBuilder.AlterColumn<Guid>(
                name: "ActivityId",
                table: "Materials",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "Materials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                table: "Materials",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ActivityId",
                table: "Materials",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_CourseId",
                table: "Materials",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_ModuleId",
                table: "Materials",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Activities_ActivityId",
                table: "Materials",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Courses_CourseId",
                table: "Materials",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Materials_Modules_ModuleId",
                table: "Materials",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
