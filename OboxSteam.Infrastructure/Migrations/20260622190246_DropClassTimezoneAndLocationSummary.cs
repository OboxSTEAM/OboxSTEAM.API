using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropClassTimezoneAndLocationSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy prod DBs may still have these columns from before e47f70e rewrote
            // AddClassDeliveryAndSchedulingFields; fresh DBs never created them.
            migrationBuilder.Sql("""
                ALTER TABLE "Classes" DROP COLUMN IF EXISTS "Timezone";
                ALTER TABLE "Classes" DROP COLUMN IF EXISTS "LocationSummary";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Timezone",
                table: "Classes",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Asia/Ho_Chi_Minh");

            migrationBuilder.AddColumn<string>(
                name: "LocationSummary",
                table: "Classes",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
