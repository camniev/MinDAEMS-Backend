using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Helpers;
using MinDAES.Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

// ASSUMPTIONS — adjust to your project (I haven't seen these):
//   * QuestionType has a member named Essay (ExamAttemptRules.ToClientType maps it to the
//     'essay' / 'checkbox' strings the UI checks for)
//   * EssayHtml is the helper from EssayHtml.cs (used here to detect an empty essay)
// ACCESS: only Administrator, Proctor and Checker (AppRoles, in ExamCheckers.cs). Administrators and
// Proctors can work on every exam schedule; a Checker only on schedules assigned to them
// (EXAM_CHECKERS). Every action checks this and answers "not found" for someone else's exam.

namespace MinDAES.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = AppRoles.Grading)]
    public class GradingController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAESDbContext;

        public GradingController(MinDAESDbContext db) => _minDAESDbContext = db;

        // ---- who may see which exam ------------------------------------------------
        private Guid? CurrentUserId() =>
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private bool SeesAllExams => User.IsInRole(AppRoles.Administrator) || User.IsInRole(AppRoles.Proctor);

        private async Task<bool> CanGradeExamAsync(Guid examId)
        {
            if (SeesAllExams) return true;
            var userId = CurrentUserId();
            if (userId == null) return false;
            return await _minDAESDbContext.Set<ExamChecker>().AnyAsync(c => c.ExamId == examId && c.UserId == userId);
        }

        // GET api/Grading/Exams
        // The exams this user may check: all of them for an Administrator/Proctor, only the assigned
        // ones for a Checker. Replaces the Checking screen's use of GetExamSchedules, which returns
        // far more (the whole roster and every answer key) than a checker should receive.
        [HttpGet("Exams")]
        public async Task<ActionResult> GetGradableExams()
        {
            var query = _minDAESDbContext.Exams.AsNoTracking().Where(e => !e.IsArchived && e.Status != ExamStatus.Upcoming);

            if (!SeesAllExams)
            {
                var userId = CurrentUserId();
                query = query.Where(e => _minDAESDbContext.Set<ExamChecker>().Any(c => c.ExamId == e.ExamId && c.UserId == userId));
            }

            var rows = await query
                .OrderByDescending(e => e.ExamDate).ThenByDescending(e => e.StartTime)
                .Select(e => new { e.ExamId, e.ExamTitle, e.ExamCode, e.Status, e.ExamDate })
                .ToListAsync();

            return Ok(rows.Select(e => new
            {
                examId = e.ExamId,
                examTitle = e.ExamTitle,
                examCode = e.ExamCode,
                status = e.Status.ToString(),
                examDate = e.ExamDate.ToString("yyyy-MM-dd")
            }));
        }

        private const int MaxPrintBatch = 200; // one print job; keeps the page and the response manageable

        // Same mapping the applicant exam page already uses, so both screens agree on type names.
        private static string TypeName(QuestionType t) => ExamAttemptRules.ToClientType(t);

        // GET api/Grading/{examId}/Roster
        [HttpGet("{examId:guid}/Roster")]
        public async Task<ActionResult> GetRoster(Guid examId)
        {
            if (!await CanGradeExamAsync(examId))
                return NotFound(new { message = "Exam schedule not found." });

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

            if (en?.Exam == null || en.Applicant == null || !await CanGradeExamAsync(en.ExamId))
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

            if (en?.Exam == null || !await CanGradeExamAsync(en.ExamId))
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

        // POST api/Grading/{examId}/PrintData
        // Everything the print view needs, for one or many applicants of ONE exam. The questions
        // are sent once, the answers once per applicant. Deliberately NOT included: the answer
        // key (IsCorrect), awarded points, and each applicant's name/email — printouts show only
        // the applicant code, position applied for and exam date. Only submitted attempts are
        // returned (the rest are counted in "skipped").
        //
        // Needs ExamEnrollment.PositionAppliedFor (string?, nullable). Add to ExamEnrollment.cs:
        //     [Column("PositionAppliedFor")] [MaxLength(100)]
        //     public string? PositionAppliedFor { get; set; }
        // then: dotnet ef migrations add AddPositionAppliedFor
        [HttpPost("{examId:guid}/PrintData")]
        public async Task<ActionResult> GetPrintData(Guid examId, [FromBody] PrintRequest? request)
        {
            Console.WriteLine($"GetPrintData called for exam {examId}, request: {request?.ExamEnrollmentIds?.Count ?? 0} applicants");
            if (!await CanGradeExamAsync(examId))
                return NotFound(new { message = "Exam schedule not found." });

            var exam = await _minDAESDbContext.Exams.AsNoTracking()
                .Include(e => e.Questions).ThenInclude(q => q.Options)
                .AsSplitQuery()
                .FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });

            var ids = request?.ExamEnrollmentIds ?? new List<Guid>();

            var enrollmentsQuery = _minDAESDbContext.Set<ExamEnrollment>().AsNoTracking()
                .Where(en => en.ExamId == examId);
            if (ids.Count > 0)
                enrollmentsQuery = enrollmentsQuery.Where(en => ids.Contains(en.ExamEnrollmentId));

            var enrollments = await enrollmentsQuery
                .Include(en => en.Answers).ThenInclude(a => a.SelectedOptions)
                .AsSplitQuery()
                .OrderBy(en => en.ApplicantCode) // a predictable order for sorting the stack of printouts
                .ToListAsync();

            var submitted = enrollments.Where(en => en.SubmittedAt != null).ToList();
            if (submitted.Count > MaxPrintBatch)
                return BadRequest(new { message = $"Print at most {MaxPrintBatch} applicants at a time." });

            return Ok(new
            {
                examTitle = exam.ExamTitle,
                examDate = exam.ExamDate.ToString("yyyy-MM-dd"),
                questions = exam.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new
                    {
                        id = q.QuestionId,
                        type = TypeName(q.Type),
                        text = q.Text,
                        points = q.Points,
                        orderIndex = q.OrderIndex,
                        options = q.Options
                            .OrderBy(o => o.OrderIndex)
                            .Select(o => new { id = o.QuestionOptionId, text = o.Text, orderIndex = o.OrderIndex })
                    }),
                submissions = submitted.Select(en => new
                {
                    examEnrollmentId = en.ExamEnrollmentId,
                    applicantCode = en.ApplicantCode,
                    positionAppliedFor = exam.ExamTitle,
                    answers = en.Answers.Select(a => new
                    {
                        questionId = a.QuestionId,
                        essayText = a.EssayText,
                        selectedOptionIds = a.SelectedOptions.Select(s => s.QuestionOptionId)
                    })
                }),
                skipped = enrollments.Count - submitted.Count
            });
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