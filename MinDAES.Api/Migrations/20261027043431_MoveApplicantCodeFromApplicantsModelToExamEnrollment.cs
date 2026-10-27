using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class MoveApplicantCodeFromApplicantsModelToExamEnrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicantCode",
                table: "APPLICANTS");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantCode",
                table: "EXAM_ENROLLMENTS",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicantCode",
                table: "EXAM_ENROLLMENTS");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantCode",
                table: "APPLICANTS",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
