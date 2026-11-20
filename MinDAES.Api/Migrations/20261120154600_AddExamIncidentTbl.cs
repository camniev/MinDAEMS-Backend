using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamIncidentTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EXAM_INCIDENTS",
                columns: table => new
                {
                    ExamIncidentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AwaySeconds = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAM_INCIDENTS", x => x.ExamIncidentId);
                    table.ForeignKey(
                        name: "FK_EXAM_INCIDENTS_EXAM_ENROLLMENTS_ExamEnrollmentId",
                        column: x => x.ExamEnrollmentId,
                        principalTable: "EXAM_ENROLLMENTS",
                        principalColumn: "ExamEnrollmentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_INCIDENTS_ExamEnrollmentId",
                table: "EXAM_INCIDENTS",
                column: "ExamEnrollmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EXAM_INCIDENTS");
        }
    }
}
