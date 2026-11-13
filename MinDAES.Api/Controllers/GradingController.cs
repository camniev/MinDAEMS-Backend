using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

// ASSUMPTIONS — adjust to your project (I haven't seen these):
//   * the DbContext type is MinDAEsDbContext (use whatever your other controllers inject)
//   * QuestionType has a member named Essay; the "type" string sent to the UI must match
//     what GetExamSchedules already sends (the UI checks for 'essay' and 'checkbox')
//   * EssayHtml is the helper from EssayHtml.cs (used here to detect an empty essay)
// TODO: protect the whole controller, e.g. [Authorize(Roles = "...")]. It exposes the
//       answer key and changes grades.

namespace MinDAES.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GradingController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAESDbContext;

        public GradingController(MinDAESDbContext minDAESDbContext) => _minDAESDbContext = minDAESDbContext;

        private static string TypeName(QuestionType t) => t.ToString().ToLowerInvariant();

        // GET api/Grading/{examId}/Roster
        [HttpGet("{examId:guid}/Roster")]
        public async Task<ActionResult> GetRoster(Guid examId)
        {
            var exam = await _minDAESDbContext.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });

            // Enum -> string is done after the query so the SQL translation stays simple.
            var rows = await _minDAESDbContext.Set<ExamEnrollment>().AsNoTracking()
                .Where(en => en.ExamId == examId)
                .OrderBy(en => en.Applicant!.FullName)
                .Select(en => new
                {
                    en.ExamEnrollmentId,
                    en.Applicant!.FullName,
                    en.Applicant.Email,
                    en.ApplicantCode,
                    en.Status,
                    en.SubmittedAt,
                    en.Score
                })
                .ToListAsync();

            var enrollments = rows.Select(r => new RosterItemDto(
                r.ExamEnrollmentId, r.FullName, r.Email, r.ApplicantCode,
                r.Status.ToString(), r.SubmittedAt, r.Score));

            return Ok(new { passingScore = exam.PassingScore, enrollments });
        }

        // GET api/Grading/Enrollments/{enrollmentId}
        [HttpGet("Enrollments/{enrollmentId:guid}")]
        public async Task<ActionResult> GetSubmission(Guid enrollmentId)
        {
            var en = await _minDAESDbContext.Set<ExamEnrollment>().AsNoTracking()
                .Include(e => e.Applicant)
                .Include(e => e.Exam).ThenInclude(x => x!.Questions).ThenInclude(q => q.Options)
                .Include(e => e.Answers).ThenInclude(a => a.SelectedOptions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.ExamEnrollmentId == enrollmentId);

            if (en?.Exam == null || en.Applicant == null)
                return NotFound(new { message = "Enrollment not found." });

            var questions = en.Exam.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new QuestionDto(
                    q.QuestionId, TypeName(q.Type), q.Text, q.Points, q.IsRequired, q.OrderIndex,
                    q.Options.OrderBy(o => o.OrderIndex)
                        .Select(o => new OptionDto(o.QuestionOptionId, o.Text, o.IsCorrect, o.OrderIndex))
                        .ToList()))
                .ToList();

            var answers = en.Answers
                .Select(a => new AnswerDto(
                    a.QuestionId, a.EssayText,
                    a.SelectedOptions.Select(s => s.QuestionOptionId).ToList(),
                    a.PointsAwarded))
                .ToList();

            return Ok(new
            {
                examEnrollmentId = en.ExamEnrollmentId,
                applicantName = en.Applicant.FullName,
                email = en.Applicant.Email,
                applicantCode = en.ApplicantCode,
                enrollmentStatus = en.Status.ToString(),
                submittedAt = en.SubmittedAt,
                score = en.Score,
                passingScore = en.Exam.PassingScore,
                questions,
                answers
            });
        }

        // PUT api/Grading/Enrollments/{enrollmentId}/Grade
        // Saves checker marks onto Answer.PointsAwarded. When Finalize is true it also
        // computes ExamEnrollment.Score (percentage, 0-100).
        [HttpPut("Enrollments/{enrollmentId:guid}/Grade")]
        public async Task<ActionResult> SaveGrade(Guid enrollmentId, [FromBody] GradeRequest? request)
        {
            if (request == null)
                return BadRequest(new { message = "Request body is required." });

            var en = await _minDAESDbContext.Set<ExamEnrollment>()
                .Include(e => e.Exam).ThenInclude(x => x!.Questions).ThenInclude(q => q.Options)
                .Include(e => e.Answers).ThenInclude(a => a.SelectedOptions)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.ExamEnrollmentId == enrollmentId);

            if (en?.Exam == null)
                return NotFound(new { message = "Enrollment not found." });
            if (en.SubmittedAt == null)
                return Conflict(new { message = "This applicant has not submitted the exam yet." });

            var questions = en.Exam.Questions.ToDictionary(q => q.QuestionId);

            // Last mark wins if a question appears twice.
            var marks = request.Marks
                .GroupBy(m => m.QuestionId)
                .ToDictionary(g => g.Key, g => g.Last().PointsAwarded);

            if (marks.Keys.Any(id => !questions.ContainsKey(id)))
                return BadRequest(new { message = "A mark refers to a question that is not in this exam." });

            await using var transaction = await _minDAESDbContext.Database.BeginTransactionAsync();
            try
            {
                foreach (var a in en.Answers)
                {
                    var q = questions[a.QuestionId];

                    if (marks.TryGetValue(a.QuestionId, out var points))
                    {
                        // Never trust the client beyond the question's own maximum.
                        a.PointsAwarded = Math.Round(Math.Clamp(points, 0m, q.Points), 2);
                    }
                    else if (q.Type != QuestionType.Essay && a.PointsAwarded == null)
                    {
                        a.PointsAwarded = AutoScore(q, a); // stays null if the question has no answer key
                    }
                }

                if (request.Finalize)
                {
                    var pending = en.Answers.Count(a =>
                        questions[a.QuestionId].Type == QuestionType.Essay
                        && a.PointsAwarded == null
                        && EssayHtml.ToPlainText(a.EssayText).Length > 0);
                    if (pending > 0)
                        return Conflict(new { message = $"{pending} essay answer(s) still need marking." });

                    var possible = (decimal)questions.Values.Sum(q => q.Points);
                    var earned = en.Answers.Sum(a => a.PointsAwarded ?? 0m);
                    en.Score = possible > 0
                        ? Math.Round(Math.Min(earned / possible * 100m, 100m), 2)
                        : 0m;
                }

                await _minDAESDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    score = en.Score,
                    totalAwarded = en.Answers.Sum(a => a.PointsAwarded ?? 0m),
                    totalPossible = questions.Values.Sum(q => q.Points)
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error saving grades", error = ex.Message });
            }
        }

        // Objective questions: full points only for an exact match with the answer key.
        private static decimal? AutoScore(Question q, Answer a)
        {
            var correct = q.Options.Where(o => o.IsCorrect).Select(o => o.QuestionOptionId).ToHashSet();
            if (correct.Count == 0) return null;

            var picked = a.SelectedOptions.Select(s => s.QuestionOptionId).ToHashSet();
            return picked.SetEquals(correct) ? q.Points : 0m;
        }
    }
}