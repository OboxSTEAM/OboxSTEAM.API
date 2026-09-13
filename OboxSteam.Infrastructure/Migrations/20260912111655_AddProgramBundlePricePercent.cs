using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramBundlePricePercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PricePercent",
                table: "ProgramBundles",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 85m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgramBundles_PricePercentRange",
                table: "ProgramBundles",
                sql: "\"PricePercent\" > 0 AND \"PricePercent\" < 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgramBundles_PricePercentRange",
                table: "ProgramBundles");

            migrationBuilder.DropColumn(
                name: "PricePercent",
                table: "ProgramBundles");
        }
    }
}
