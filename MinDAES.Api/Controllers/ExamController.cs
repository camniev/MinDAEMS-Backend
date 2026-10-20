using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;
using System.Linq.Expressions;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExamController : ControllerBase
    {
        private readonly MinDAESDbContext _mindaEsDbContext;
        public ExamController(MinDAESDbContext mindaEsDbContext)
        {
            _mindaEsDbContext = mindaEsDbContext;
        }

        // add new exam schedules
        [HttpPost("AddNewExamSchedules")]
        public async Task<ActionResult> AddNewExamSchedules([FromBody] ExamDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request header is required."});

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();

            try
            {
                var exam = new Exam
                {
                    ExamId = Guid.NewGuid(),
                    ExamTitle = data.ExamTitle,
                    ExamCode = data.ExamCode,
                    ExamDate = data.ExamDate,
                    StartTime = data.StartTime,
                    DurationMinutes = data.DurationMinutes,
                    PassingScore = data.PassingScore,
                    Instructions = data.Instructions,
                    AccessType = data.AccessType == "Private"
                        ? ExamAccessType.Private
                        : ExamAccessType.Public,
                    CreatedByUserId = data.UserId,
                    CreatedAt = TimeHelper.GetPhilippineTime()
                };

                _mindaEsDbContext.Exams.Add(exam);
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "New exam schedule added successfully."});
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "Error adding new exam schedule",
                    error = ex.Message
                });
            }
        }

        [HttpGet("GetExamSchedules")]
        public async Task<ActionResult<List<ExamScheduleResponseDto>>> GetExamSchedules()
        {
            var now = TimeHelper.GetPhilippineTime();

            var exams = await _mindaEsDbContext.Exams
                .AsNoTracking()
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Options)
                .Include(e => e.Enrollments)
                    .ThenInclude(en => en.Applicant)
                .OrderByDescending(e => e.ExamDate)
                    .ThenByDescending(e => e.StartTime)
                .ToListAsync();

            var result = exams.Select(exam => new ExamScheduleResponseDto
            {
                ExamId = exam.ExamId,
                ExamTitle = exam.ExamTitle,
                ExamDate = exam.ExamDate,
                StartTime = exam.StartTime,
                DurationMinutes = exam.DurationMinutes,
                PassingScore = exam.PassingScore,
                Instructions = exam.Instructions,
                AccessType = exam.AccessType == ExamAccessType.Private ? "Private" : "Public",
                ExamCode = exam.ExamCode,
                Status = ComputeExamStatus(exam.ExamDate, exam.StartTime, exam.DurationMinutes, now),

                Questions = exam.Questions?.Select(q => new ExamQuestionResponseDto
                {
                    Id = q.QuestionId,
                    Type = MapQuestionType(q.Type),
                    Points = q.Points,
                    Required = q.IsRequired,
                    Text = q.Text,
                    Options = q.Options?.Select(o => new ExamQuestionOptionResponseDto
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect,
                    }).ToList() ?? new List<ExamQuestionOptionResponseDto>(),
                }).ToList() ?? new List<ExamQuestionResponseDto>(),

                // Empty list until anyone's been enrolled — exam.Enrollments is just
                // an empty collection for a freshly created exam, nothing special needed.
                Applicants = exam.Enrollments?.Select(en => new ExamApplicantResponseDto
                {
                    ExamEnrollmentId = en.ExamEnrollmentId,
                    ApplicantId = en.ApplicantId,
                    Name = en.Applicant?.FullName,
                    Email = en.Applicant?.Email,
                    Status = en.Status.ToString(),
                    Score = en.Score,
                }).ToList() ?? new List<ExamApplicantResponseDto>(),
            }).ToList();

            return Ok(result);
        }

        private static string MapQuestionType(QuestionType type) => type switch
        {
            QuestionType.MultipleChoice => "mcq",
            QuestionType.Checkbox => "checkbox",
            QuestionType.TrueFalse => "truefalse",
            QuestionType.Essay => "essay",
            _ => "mcq",
        };

        private static string ComputeExamStatus(DateOnly examDate, TimeOnly startTime, int durationMinutes, DateTime nowPh)
        {
            var examStart = examDate.ToDateTime(startTime);
            var examEnd = examStart.AddMinutes(durationMinutes);
            if (nowPh < examStart) return "Upcoming";
            if (nowPh <= examEnd) return "Active";
            return "Completed";
        }
    }
}