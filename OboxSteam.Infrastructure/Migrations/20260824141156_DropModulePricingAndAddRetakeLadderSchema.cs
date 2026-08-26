using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropModulePricingAndAddRetakeLadderSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "RetakeFee",
                table: "Modules");

            migrationBuilder.DropColumn(
                name: "AssignmentFailureCount",
                table: "ModuleEnrollments");

            migrationBuilder.AddColumn<DateTime>(
                name: "IntensivePaceAcceptedAt",
                table: "ClassRedeliveryRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolutionType",
                table: "ClassRedeliveryRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Classes",
                type: "text",
                nullable: false,
                defaultValue: "Standard");

            migrationBuilder.AddColumn<Guid>(
                name: "RemedialModuleId",
                table: "Classes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "ClassEnrollments",
                type: "text",
                nullable: false,
                defaultValueSql: "'Primary'");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_RemedialModuleId",
                table: "Classes",
                column: "RemedialModuleId",
                filter: "\"IsDeleted\" = false AND \"RemedialModuleId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Modules_RemedialModuleId",
                table: "Classes",
                column: "RemedialModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Modules_RemedialModuleId",
                table: "Classes");

            migrationBuilder.DropIndex(
                name: "IX_Classes_RemedialModuleId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "IntensivePaceAcceptedAt",
                table: "ClassRedeliveryRequests");

            migrationBuilder.DropColumn(
                name: "ResolutionType",
                table: "ClassRedeliveryRequests");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "RemedialModuleId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ClassEnrollments");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Modules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetakeFee",
                table: "Modules",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentFailureCount",
                table: "ModuleEnrollments",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
