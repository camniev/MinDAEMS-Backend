using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicantSerialToExamTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EXAM_ENROLLMENTS_ExamId",
                table: "EXAM_ENROLLMENTS");

            migrationBuilder.AddColumn<int>(
                name: "LastApplicantSerial",
                table: "EXAMS",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "ApplicantCode",
                table: "EXAM_ENROLLMENTS",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_ENROLLMENTS_ExamId_ApplicantCode",
                table: "EXAM_ENROLLMENTS",
                columns: new[] { "ExamId", "ApplicantCode" },
                unique: true);

            migrationBuilder.Sql(@"UPDATE EXAMS SET LastApplicantSerial = (SELECT COUNT(*) FROM EXAM_ENROLLMENTS e WHERE e.ExamId = EXAMS.ExamId)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EXAM_ENROLLMENTS_ExamId_ApplicantCode",
                table: "EXAM_ENROLLMENTS");

            migrationBuilder.DropColumn(
                name: "LastApplicantSerial",
                table: "EXAMS");

            migrationBuilder.AlterColumn<string>(
                name: "ApplicantCode",
                table: "EXAM_ENROLLMENTS",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_ENROLLMENTS_ExamId",
                table: "EXAM_ENROLLMENTS",
                column: "ExamId");
        }
    }
}
