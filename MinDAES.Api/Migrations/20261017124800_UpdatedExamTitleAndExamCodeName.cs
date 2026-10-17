using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpdatedExamTitleAndExamCodeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "AccessCode",
                table: "EXAMS",
                newName: "ExamCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExamCode",
                table: "EXAMS",
                newName: "AccessCode");
        }
    }
}
