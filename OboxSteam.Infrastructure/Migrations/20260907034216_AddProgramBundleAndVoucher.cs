using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProgramBundleAndVoucher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BundleEnrollmentId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Payments",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "VoucherId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BundleEnrollmentId",
                table: "PaymentRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BundleId",
                table: "Certificates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramBundles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "text", nullable: true),
                    Category = table.Column<string>(type: "text", nullable: false),
                    FrameworkId = table.Column<Guid>(type: "uuid", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramBundles", x => x.Id);
                    table.CheckConstraint("CK_ProgramBundles_PriceNonNegative", "\"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProgramBundles_ProgramFrameworks_FrameworkId",
                        column: x => x.FrameworkId,
                        principalTable: "ProgramFrameworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PercentOff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmountOff = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpiryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageLimit = table.Column<int>(type: "integer", nullable: true),
                    MaxUsagePerStudent = table.Column<int>(type: "integer", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.CheckConstraint("CK_Vouchers_AmountOffPositive", "\"AmountOff\" IS NULL OR \"AmountOff\" > 0");
                    table.CheckConstraint("CK_Vouchers_ExactlyOneDiscount", "(\"PercentOff\" IS NOT NULL AND \"AmountOff\" IS NULL) OR (\"PercentOff\" IS NULL AND \"AmountOff\" IS NOT NULL)");
                    table.CheckConstraint("CK_Vouchers_MaxUsagePerStudentPositive", "\"MaxUsagePerStudent\" IS NULL OR \"MaxUsagePerStudent\" > 0");
                    table.CheckConstraint("CK_Vouchers_PercentOffRange", "\"PercentOff\" IS NULL OR (\"PercentOff\" > 0 AND \"PercentOff\" <= 100)");
                    table.CheckConstraint("CK_Vouchers_UsageLimitPositive", "\"UsageLimit\" IS NULL OR \"UsageLimit\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "BundleEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleEnrollments", x => x.Id);
                    table.CheckConstraint("CK_BundleEnrollments_ProgressPercentRange", "\"ProgressPercent\" >= 0 AND \"ProgressPercent\" <= 100");
                    table.ForeignKey(
                        name: "FK_BundleEnrollments_ProgramBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "ProgramBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BundleEnrollments_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProgramBundleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BundleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RequiresPreviousCompletion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramBundleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgramBundleItems_ProgramBundles_BundleId",
                        column: x => x.BundleId,
                        principalTable: "ProgramBundles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProgramBundleItems_Programs_ProgramId",
                        column: x => x.ProgramId,
                        principalTable: "Programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BundleEnrollmentId",
                table: "Payments",
                column: "BundleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_VoucherId",
                table: "Payments",
                column: "VoucherId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_DiscountAmountNonNegative",
                table: "Payments",
                sql: "\"DiscountAmount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentRequests_BundleEnrollmentId",
                table: "PaymentRequests",
                column: "BundleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_BundleId",
                table: "Certificates",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_StudentId_BundleId",
                table: "Certificates",
                columns: new[] { "StudentId", "BundleId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"BundleId\" IS NOT NULL AND \"ProgramId\" IS NULL AND \"ModuleId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BundleEnrollments_BundleId",
                table: "BundleEnrollments",
                column: "BundleId");

            migrationBuilder.CreateIndex(
                name: "IX_BundleEnrollments_StudentId_BundleId",
                table: "BundleEnrollments",
                columns: new[] { "StudentId", "BundleId" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('PendingPayment', 'Active')");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramBundleItems_BundleId_ProgramId",
                table: "ProgramBundleItems",
                columns: new[] { "BundleId", "ProgramId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramBundleItems_BundleId_SortOrder",
                table: "ProgramBundleItems",
                columns: new[] { "BundleId", "SortOrder" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramBundleItems_ProgramId",
                table: "ProgramBundleItems",
                column: "ProgramId",
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ProgramBundles_Code",
                table: "ProgramBundles",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProgramBundles_FrameworkId",
                table: "ProgramBundles",
                column: "FrameworkId",
                filter: "\"IsDeleted\" = false AND \"FrameworkId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_Code",
                table: "Vouchers",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_ProgramBundles_BundleId",
                table: "Certificates",
                column: "BundleId",
                principalTable: "ProgramBundles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_BundleEnrollments_BundleEnrollmentId",
                table: "PaymentRequests",
                column: "BundleEnrollmentId",
                principalTable: "BundleEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_BundleEnrollments_BundleEnrollmentId",
                table: "Payments",
                column: "BundleEnrollmentId",
                principalTable: "BundleEnrollments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Vouchers_VoucherId",
                table: "Payments",
                column: "VoucherId",
                principalTable: "Vouchers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_ProgramBundles_BundleId",
                table: "Certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_BundleEnrollments_BundleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_BundleEnrollments_BundleEnrollmentId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Vouchers_VoucherId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "BundleEnrollments");

            migrationBuilder.DropTable(
                name: "ProgramBundleItems");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "ProgramBundles");

            migrationBuilder.DropIndex(
                name: "IX_Payments_BundleEnrollmentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_VoucherId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_DiscountAmountNonNegative",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_PaymentRequests_BundleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_BundleId",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_StudentId_BundleId",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "BundleEnrollmentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "VoucherId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "BundleEnrollmentId",
                table: "PaymentRequests");

            migrationBuilder.DropColumn(
                name: "BundleId",
                table: "Certificates");
        }
    }
}
