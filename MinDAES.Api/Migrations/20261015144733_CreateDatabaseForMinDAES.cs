using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MinDAES.Api.Migrations
{
    /// <inheritdoc />
    public partial class CreateDatabaseForMinDAES : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APPLICANTS",
                columns: table => new
                {
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApplicantCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APPLICANTS", x => x.ApplicantId);
                });

            migrationBuilder.CreateTable(
                name: "EMPLOYEES",
                columns: table => new
                {
                    EmpId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EMPLOYEES", x => x.EmpId);
                });

            migrationBuilder.CreateTable(
                name: "USER_TYPES",
                columns: table => new
                {
                    UserTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserTypeDesc = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USER_TYPES", x => x.UserTypeId);
                });

            migrationBuilder.CreateTable(
                name: "USERS",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Password = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    DatePasswordLastSet = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MustChangePasswordOnFirstLogin = table.Column<bool>(type: "bit", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RefreshTokenExpiryTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UserTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmpId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_USERS", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_USERS_EMPLOYEES_EmpId",
                        column: x => x.EmpId,
                        principalTable: "EMPLOYEES",
                        principalColumn: "EmpId");
                    table.ForeignKey(
                        name: "FK_USERS_USER_TYPES_UserTypeId",
                        column: x => x.UserTypeId,
                        principalTable: "USER_TYPES",
                        principalColumn: "UserTypeId");
                });

            migrationBuilder.CreateTable(
                name: "EXAMS",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamTitle = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    PassingScore = table.Column<int>(type: "int", nullable: false),
                    Instructions = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AccessType = table.Column<int>(type: "int", nullable: false),
                    AccessCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAMS", x => x.ExamId);
                    table.ForeignKey(
                        name: "FK_EXAMS_USERS_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "USERS",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EXAM_ENROLLMENTS",
                columns: table => new
                {
                    ExamEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    InvitedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EXAM_ENROLLMENTS", x => x.ExamEnrollmentId);
                    table.ForeignKey(
                        name: "FK_EXAM_ENROLLMENTS_APPLICANTS_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "APPLICANTS",
                        principalColumn: "ApplicantId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EXAM_ENROLLMENTS_EXAMS_ExamId",
                        column: x => x.ExamId,
                        principalTable: "EXAMS",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QUESTIONS",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUESTIONS", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_QUESTIONS_EXAMS_ExamId",
                        column: x => x.ExamId,
                        principalTable: "EXAMS",
                        principalColumn: "ExamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Answers",
                columns: table => new
                {
                    AnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamEnrollmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EssayText = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    PointsAwarded = table.Column<decimal>(type: "decimal(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Answers", x => x.AnswerId);
                    table.ForeignKey(
                        name: "FK_Answers_EXAM_ENROLLMENTS_ExamEnrollmentId",
                        column: x => x.ExamEnrollmentId,
                        principalTable: "EXAM_ENROLLMENTS",
                        principalColumn: "ExamEnrollmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Answers_QUESTIONS_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "QUESTIONS",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QUESTION_OPTIONS",
                columns: table => new
                {
                    QuestionOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    OrderIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QUESTION_OPTIONS", x => x.QuestionOptionId);
                    table.ForeignKey(
                        name: "FK_QUESTION_OPTIONS_QUESTIONS_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "QUESTIONS",
                        principalColumn: "QuestionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ANSWER_OPTIONS",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ANSWER_OPTIONS", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ANSWER_OPTIONS_Answers_AnswerId",
                        column: x => x.AnswerId,
                        principalTable: "Answers",
                        principalColumn: "AnswerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ANSWER_OPTIONS_QUESTION_OPTIONS_QuestionOptionId",
                        column: x => x.QuestionOptionId,
                        principalTable: "QUESTION_OPTIONS",
                        principalColumn: "QuestionOptionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ANSWER_OPTIONS_AnswerId",
                table: "ANSWER_OPTIONS",
                column: "AnswerId");

            migrationBuilder.CreateIndex(
                name: "IX_ANSWER_OPTIONS_QuestionOptionId",
                table: "ANSWER_OPTIONS",
                column: "QuestionOptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_ExamEnrollmentId",
                table: "Answers",
                column: "ExamEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Answers_QuestionId",
                table: "Answers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_ENROLLMENTS_ApplicantId",
                table: "EXAM_ENROLLMENTS",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAM_ENROLLMENTS_ExamId",
                table: "EXAM_ENROLLMENTS",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_EXAMS_CreatedByUserId",
                table: "EXAMS",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QUESTION_OPTIONS_QuestionId",
                table: "QUESTION_OPTIONS",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QUESTIONS_ExamId",
                table: "QUESTIONS",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_USERS_EmpId",
                table: "USERS",
                column: "EmpId",
                unique: true,
                filter: "[EmpId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_USERS_UserTypeId",
                table: "USERS",
                column: "UserTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ANSWER_OPTIONS");

            migrationBuilder.DropTable(
                name: "Answers");

            migrationBuilder.DropTable(
                name: "QUESTION_OPTIONS");

            migrationBuilder.DropTable(
                name: "EXAM_ENROLLMENTS");

            migrationBuilder.DropTable(
                name: "QUESTIONS");

            migrationBuilder.DropTable(
                name: "APPLICANTS");

            migrationBuilder.DropTable(
                name: "EXAMS");

            migrationBuilder.DropTable(
                name: "USERS");

            migrationBuilder.DropTable(
                name: "EMPLOYEES");

            migrationBuilder.DropTable(
                name: "USER_TYPES");
        }
    }
}
