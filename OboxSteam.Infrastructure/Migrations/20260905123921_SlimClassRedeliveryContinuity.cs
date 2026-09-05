using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SlimClassRedeliveryContinuity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Remap open ladder statuses to AwaitingClassSelection so students can pick
        // from the continuity catalog. Terminal rows are left unchanged.
        migrationBuilder.Sql(
            """
            UPDATE "ClassRedeliveryRequests"
            SET "Status" = 'AwaitingClassSelection'
            WHERE "Status" IN ('PendingManager', 'PendingAutoMatch', 'AwaitingIntensiveConsent')
              AND "IsDeleted" = FALSE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible data remap — cannot restore previous ladder statuses.
    }
}
