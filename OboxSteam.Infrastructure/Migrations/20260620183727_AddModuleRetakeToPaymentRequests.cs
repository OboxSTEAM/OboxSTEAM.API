using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleRetakeToPaymentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramEnrollmentId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleEnrollmentId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModuleId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_ModuleEnrollmentId",
                table: "PaymentRequests",
                column: "ModuleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_ModuleId",
                table: "PaymentRequests",
                column: "ModuleId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_ModuleEnrollments_ModuleEnrollmentId",
                table: "PaymentRequests",
                column: "ModuleEnrollmentId",
                principalTable: "ModuleEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_Modules_ModuleId",
                table: "PaymentRequests",
                column: "ModuleId",
                principalTable: "Modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_ModuleEnrollments_ModuleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_Modules_ModuleId",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_ModuleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_ModuleId",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "ModuleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "ModuleId",
                table: "PaymentRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProgramEnrollmentId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
