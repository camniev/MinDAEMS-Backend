using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "QUESTIONS",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EXAM_ATTACHMENTS",
                columns: table => new
                {
                    ExamAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAM_ATTACHMENTS", x => x.ExamAttachmentId);
                    table.ForeignKey(
                        name: "FK_EXAM_ATTACHMENTS_EXAMS_ExamId",
                        column: x => x.ExamId,
                        principalTable: "EXAMS",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QUESTIONS_AttachmentId",
                table: "QUESTIONS",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_ATTACHMENTS_ExamId",
                table: "EXAM_ATTACHMENTS",
                column: "ExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_QUESTIONS_EXAM_ATTACHMENTS_AttachmentId",
                table: "QUESTIONS",
                column: "AttachmentId",
                principalTable: "EXAM_ATTACHMENTS",
                principalColumn: "ExamAttachmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QUESTIONS_EXAM_ATTACHMENTS_AttachmentId",
                table: "QUESTIONS");

            migrationBuilder.DropTable(
                name: "EXAM_ATTACHMENTS");

            migrationBuilder.DropIndex(
                name: "IX_QUESTIONS_AttachmentId",
                table: "QUESTIONS");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "QUESTIONS");
        }
    }
}
