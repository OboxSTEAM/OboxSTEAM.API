using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OboxSteam.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentRecoveryAndClassRedelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssessmentRecoveryRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StudentMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MentorNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExtraAttemptsGranted = table.Column<int>(type: "integer", nullable: false),
                    PersonalDueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PersonalAvailableUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AssessmentRecoveryRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssessmentRecoveryRequests_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRecoveryRequests_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssessmentRecoveryRequests_ModuleEnrollments_ModuleEnrollme~",
                        column: x => x.ModuleEnrollmentId,
                        principalTable: "ModuleEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssessmentRecoveryRequests_Users_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssessmentRecoveryRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassRedeliveryRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    TargetClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RetakeModuleEnrollmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_ClassRedeliveryRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Classes_SourceClassId",
                        column: x => x.SourceClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Classes_TargetClassId",
                        column: x => x.TargetClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_ModuleEnrollments_ModuleEnrollmentId",
                        column: x => x.ModuleEnrollmentId,
                        principalTable: "ModuleEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_ModuleEnrollments_RetakeModuleEnrol~",
                        column: x => x.RetakeModuleEnrollmentId,
                        principalTable: "ModuleEnrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Users_DecidedBy",
                        column: x => x.DecidedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassRedeliveryRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecoveryRequests_AssignmentId",
                table: "AssessmentRecoveryRequests",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecoveryRequests_ClassId",
                table: "AssessmentRecoveryRequests",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecoveryRequests_DecidedBy",
                table: "AssessmentRecoveryRequests",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecoveryRequests_ModuleEnrollmentId_AssignmentId",
                table: "AssessmentRecoveryRequests",
                columns: new[] { "ModuleEnrollmentId", "AssignmentId" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AssessmentRecoveryRequests_StudentId_AssignmentId_Status",
                table: "AssessmentRecoveryRequests",
                columns: new[] { "StudentId", "AssignmentId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_DecidedBy",
                table: "ClassRedeliveryRequests",
                column: "DecidedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_ModuleEnrollmentId",
                table: "ClassRedeliveryRequests",
                column: "ModuleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_ModuleId_Status",
                table: "ClassRedeliveryRequests",
                columns: new[] { "ModuleId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_PaymentId",
                table: "ClassRedeliveryRequests",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_RequestedByUserId",
                table: "ClassRedeliveryRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_RetakeModuleEnrollmentId",
                table: "ClassRedeliveryRequests",
                column: "RetakeModuleEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_SourceClassId",
                table: "ClassRedeliveryRequests",
                column: "SourceClassId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_StudentId_Status",
                table: "ClassRedeliveryRequests",
                columns: new[] { "StudentId", "Status" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_ClassRedeliveryRequests_TargetClassId",
                table: "ClassRedeliveryRequests",
                column: "TargetClassId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssessmentRecoveryRequests");

            migrationBuilder.DropTable(
                name: "ClassRedeliveryRequests");
        }
    }
}
