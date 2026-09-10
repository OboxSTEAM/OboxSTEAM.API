using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Vouchers",
                type: "text",
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Status",
                table: "Vouchers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_Status",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Vouchers");
        }
    }
}
