using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ProgramStatusEnum : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Align legacy free-text values with ProgramStatus before making the column required.
        migrationBuilder.Sql(
            """
            UPDATE "Programs"
            SET "Status" = CASE
                WHEN "Status" IS NULL OR btrim("Status") = '' THEN 'Draft'
                WHEN lower("Status") IN ('published', 'active') THEN 'Active'
                WHEN lower("Status") IN ('inactive', 'cancelled', 'canceled') THEN 'Inactive'
                WHEN lower("Status") = 'draft' THEN 'Draft'
                ELSE 'Draft'
            END;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Programs",
            type: "text",
            nullable: false,
            defaultValue: "Draft",
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "Programs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "text",
            oldDefaultValue: "Draft");
    }
}
