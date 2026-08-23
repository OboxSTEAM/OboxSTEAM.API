using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations;

/// <inheritdoc />
public partial class RenameSessionKindLessonFieldTripToLiveOnlineOffline : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "ClassSessions"
            SET "SessionKind" = 'LiveOnline'
            WHERE "SessionKind" = 'Lesson';

            UPDATE "ClassSessions"
            SET "SessionKind" = 'Offline'
            WHERE "SessionKind" = 'FieldTrip';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "ClassSessions"
            SET "SessionKind" = 'Lesson'
            WHERE "SessionKind" = 'LiveOnline';

            UPDATE "ClassSessions"
            SET "SessionKind" = 'FieldTrip'
            WHERE "SessionKind" = 'Offline';
            """);
    }
}
