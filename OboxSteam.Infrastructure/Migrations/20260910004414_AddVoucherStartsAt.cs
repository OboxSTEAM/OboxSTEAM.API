using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherStartsAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartsAt",
                table: "Vouchers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vouchers_StartsAtBeforeExpiryAt",
                table: "Vouchers",
                sql: "\"StartsAt\" IS NULL OR \"ExpiryAt\" IS NULL OR \"StartsAt\" < \"ExpiryAt\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vouchers_StartsAtBeforeExpiryAt",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "StartsAt",
                table: "Vouchers");
        }
    }
}
