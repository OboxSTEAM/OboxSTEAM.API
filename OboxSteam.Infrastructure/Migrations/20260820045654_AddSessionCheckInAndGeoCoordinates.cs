using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCheckInAndGeoCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckInCode",
                table: "ClassSessions",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CheckInToken",
                table: "ClassSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CheckInTokenExpiresAt",
                table: "ClassSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "ClassSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "ClassSessions",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckInCode",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "CheckInToken",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "CheckInTokenExpiresAt",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ClassSessions");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ClassSessions");
        }
    }
}
