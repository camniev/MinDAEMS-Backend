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
    public class ApplicantController : ControllerBase
    {
        private readonly MinDAESDbContext _mindaEsDbContext;
        public ApplicantController(MinDAESDbContext mindaEsDbContext)
        {
            _mindaEsDbContext = mindaEsDbContext;
        }

        // add applicants
        [HttpPost("{examId:guid}/EnrollApplicants")]
        public async Task<ActionResult> EnrollApplicants(Guid examId, [FromBody] EnrollApplicantsDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var examExists = await _mindaEsDbContext.Exams.AnyAsync(e => e.ExamId == examId);
            if (!examExists)
                return NotFound(new { message = "Exam schedule not found." });

            if (data.Applicants.Count == 0)
                return BadRequest(new { message = "At least one applicant is required." });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                var incoming = data.Applicants
                    .Select(a => new
                    {
                        Email = a.Email.Trim(),
                        EmailKey = a.Email.Trim().ToLowerInvariant(),
                        Name = a.Name.Trim(),
                        a.ApplicantCode,
                    })
                    .GroupBy(a => a.EmailKey)
                    .Select(g => g.First())
                    .ToList();

                var emailKeys = incoming.Select(a => a.EmailKey).ToList();

                // One query for every matching existing applicant, keyed by email
                // for in-memory lookup — applicants are a directory independent of
                // any one exam, so someone enrolled elsewhere should be reused, not
                // duplicated. Note: ApplicantCode is intentionally NOT read from or
                // written to this record anymore — it's not a property of the
                // person, it's a property of this specific application.
                var existingApplicants = await _mindaEsDbContext.Applicants
                    .Where(a => emailKeys.Contains(a.Email.ToLower()))
                    .ToListAsync();
                var applicantsByEmail = existingApplicants
                    .ToDictionary(a => a.Email.ToLowerInvariant(), a => a);

                // Applicants already on THIS exam's roster, so re-importing the same
                // person for the same exam is a no-op rather than a duplicate
                // enrollment row. This check is already scoped to `examId`, so a
                // second enrollment for a DIFFERENT exam was never blocked — the
                // bug was purely that its ApplicantCode had nowhere correct to land.
                var existingApplicantIds = existingApplicants.Select(a => a.ApplicantId).ToList();
                var alreadyEnrolledIds = (await _mindaEsDbContext.ExamEnrollments
                        .Where(en => en.ExamId == examId && existingApplicantIds.Contains(en.ApplicantId))
                        .Select(en => en.ApplicantId)
                        .ToListAsync())
                    .ToHashSet();

                var enrolledEmails = new List<string>();
                var skippedEmails = new List<string>();

                foreach (var entry in incoming)
                {
                    if (!applicantsByEmail.TryGetValue(entry.EmailKey, out var applicant))
                    {
                        applicant = new Applicant
                        {
                            ApplicantId = Guid.NewGuid(),
                            FullName = entry.Name,
                            Email = entry.Email,
                            // ApplicantCode removed from here — it now lives on the
                            // ExamEnrollment created below, since it's specific to
                            // this application, not to the person.
                            CreatedAt = TimeHelper.GetPhilippineTime(),
                        };
                        _mindaEsDbContext.Applicants.Add(applicant);
                        // Visible to later entries in this same batch, since nothing
                        // is flushed to the database until SaveChangesAsync below.
                        applicantsByEmail[entry.EmailKey] = applicant;
                    }

                    if (alreadyEnrolledIds.Contains(applicant.ApplicantId))
                    {
                        skippedEmails.Add(entry.Email);
                        continue;
                    }

                    _mindaEsDbContext.ExamEnrollments.Add(new ExamEnrollment
                    {
                        ExamEnrollmentId = Guid.NewGuid(),
                        ExamId = examId,
                        ApplicantId = applicant.ApplicantId,
                        // This is the actual fix: every enrollment gets its own
                        // code, regardless of whether the Applicant record itself
                        // was just created or already existed from a prior exam.
                        ApplicantCode = entry.ApplicantCode,
                        Status = EnrollmentStatus.Invited,
                        InvitedAt = TimeHelper.GetPhilippineTime(),
                    });
                    enrolledEmails.Add(entry.Email);
                    alreadyEnrolledIds.Add(applicant.ApplicantId); // guards a same-batch duplicate too
                }

                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = skippedEmails.Count > 0
                        ? $"Enrolled {enrolledEmails.Count} applicant(s); {skippedEmails.Count} were already enrolled and skipped."
                        : $"Enrolled {enrolledEmails.Count} applicant(s).",
                    enrolledCount = enrolledEmails.Count,
                    skippedCount = skippedEmails.Count,
                    skippedEmails,
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "Error enrolling applicants",
                    error = ex.Message,
                });
            }
        }

        // remove applicants from roster
        [HttpDelete("{examId:guid}/Enrollments/{enrollmentId:guid}")]
        public async Task<ActionResult> RemoveEnrollment(Guid examId, Guid enrollmentId)
        {
            var enrollment = await _mindaEsDbContext.ExamEnrollments
                .FirstOrDefaultAsync(en => en.ExamEnrollmentId == enrollmentId && en.ExamId == examId);

            if (enrollment == null)
                return NotFound(new { message = "Enrollment not found." });

            if (enrollment.Status == EnrollmentStatus.Completed || enrollment.SubmittedAt != null)
                return Conflict(new { message = "Cannot remove an applicant who has already completed the exam." });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                _mindaEsDbContext.ExamEnrollments.Remove(enrollment);
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Applicant removed from exam roster." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error removing applicant from roster", error = ex.Message });
            }
        }

        [HttpGet("MySchedules")]
        public async Task<ActionResult<List<ApplicantExamScheduleResponseDto>>> GetMySchedules(
            [FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email is required." });
        
            var now = TimeHelper.GetPhilippineTime();
            var emailKey = email.Trim().ToLowerInvariant();
        
            // Pull this applicant's enrollments (across any exam) along with the
            // Exam each one belongs to, so status can be computed per exam.
            var enrollments = await _mindaEsDbContext.ExamEnrollments
                .AsNoTracking()
                .Include(en => en.Exam)
                .Include(en => en.Applicant)
                .Where(en => en.Applicant != null && en.Applicant.Email.ToLower() == emailKey)
                .Where(en => en.Exam != null && !en.Exam.IsArchived)
                .ToListAsync();
        
            var result = enrollments
                .Select(en => new { en, status = ExamStatusHelper.ComputeDisplayStatus(en.Exam!, now) })
                // Per the spec: applicants only see exams that are currently Ongoing.
                .Where(x => x.status == "Ongoing")
                .Select(x => new ApplicantExamScheduleResponseDto
                {
                    ExamId = x.en.ExamId,
                    ExamEnrollmentId = x.en.ExamEnrollmentId,
                    ExamTitle = x.en.Exam!.ExamTitle,
                    ExamDate = x.en.Exam.ExamDate,
                    StartTime = x.en.Exam.StartTime,
                    DurationMinutes = x.en.Exam.DurationMinutes,
                    Instructions = x.en.Exam.Instructions,
                    Status = x.status,
                    EnrollmentStatus = x.en.Status.ToString(),
                    Score = x.en.Score,
                })
                .OrderBy(e => e.ExamDate)
                    .ThenBy(e => e.StartTime)
                .ToList();
        
            return Ok(result);
        }

        // ---- Paste these members inside ApplicantController ----
        // Identity is the same temporary email check as MySchedules: every call must
        // name an email that owns the enrollment. Not real authentication.

        // POST api/Applicant/MyExam/{enrollmentId}/Start
        // Begins the attempt (records StartedAt the first time), or resumes it if
        // already started — calling it again never restarts the clock.
        [HttpPost("MyExam/{enrollmentId:guid}/Start")]
        public async Task<ActionResult<ExamSessionResponseDto>> StartMyExam(
            Guid enrollmentId, [FromBody] StartExamDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var en = await FindOwnedEnrollmentAsync(enrollmentId, data.Email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            var exam = en.Exam;
            var now = TimeHelper.GetPhilippineTime();

            if (en.Status == EnrollmentStatus.Disqualified)
                return Conflict(new { message = "You have been disqualified from this exam." });
            if (en.SubmittedAt != null || en.Status == EnrollmentStatus.Completed)
                return Conflict(new { message = "This exam has already been submitted." });

            if (en.StartedAt == null)
            {
                // First start: the exam has to be open right now.
                if (ExamStatusHelper.ComputeDisplayStatus(exam, now) != "Ongoing")
                    return Conflict(new { message = "This exam isn't open." });

                en.StartedAt = now;
                await _mindaEsDbContext.SaveChangesAsync();
            }
            else
            {
                // Resuming: only continue if the attempt is still live.
                var over = await RejectIfAttemptOverAsync(en, now, graceSeconds: 0);
                if (over != null) return over;
            }

            var saved = await _mindaEsDbContext.Answers
                .AsNoTracking()
                .Include(a => a.SelectedOptions)
                .Where(a => a.ExamEnrollmentId == en.ExamEnrollmentId)
                .ToListAsync();

            return Ok(new ExamSessionResponseDto
            {
                ExamEnrollmentId = en.ExamEnrollmentId,
                ExamTitle = exam.ExamTitle,
                DurationMinutes = exam.DurationMinutes,
                SecondsRemaining = ExamAttemptRules.SecondsRemaining(en.StartedAt!.Value, exam.DurationMinutes, now),
                Questions = exam.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new ApplicantQuestionDto
                    {
                        Id = q.QuestionId,
                        Type = ExamAttemptRules.ToClientType(q.Type),
                        Text = q.Text,
                        Points = q.Points,
                        Required = q.IsRequired,
                        Options = q.Options
                            .OrderBy(o => o.OrderIndex)
                            .Select(o => new ApplicantOptionDto { Id = o.QuestionOptionId, Text = o.Text })
                            .ToList(),
                    })
                    .ToList(),
                Answers = saved
                    .Select(a => new SavedAnswerDto
                    {
                        QuestionId = a.QuestionId,
                        SelectedOptionIds = a.SelectedOptions.Select(o => o.QuestionOptionId).ToList(),
                        EssayText = a.EssayText,
                    })
                    .ToList(),
            });
        }

        // PUT api/Applicant/MyExam/{enrollmentId}/Answers
        // Autosave. Replaces the applicant's whole saved answer set with what's sent,
        // so a question that was cleared on screen is cleared here too.
        [HttpPut("MyExam/{enrollmentId:guid}/Answers")]
        public async Task<ActionResult> SaveMyExamAnswers(
            Guid enrollmentId, [FromBody] ExamAnswersDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var en = await FindOwnedEnrollmentAsync(enrollmentId, data.Email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            var now = TimeHelper.GetPhilippineTime();
            var over = await RejectIfAttemptOverAsync(en, now, ExamAttemptRules.SubmissionGraceSeconds);
            if (over != null) return over;

            var error = ExamAttemptRules.Validate(en.Exam.Questions, data.Answers);
            if (error != null)
                return BadRequest(new { message = error });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                await ReplaceAnswersAsync(en, data.Answers);
                await transaction.CommitAsync();
                return Ok(new { savedAt = now });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error saving answers", error = ex.Message });
            }
        }

        // POST api/Applicant/MyExam/{enrollmentId}/Submit
        // Saves the final answers and closes the attempt in one transaction.
        // Required-question checks are the UI's job: the server can't refuse a
        // submit when time has run out.
        [HttpPost("MyExam/{enrollmentId:guid}/Submit")]
        public async Task<ActionResult> SubmitMyExam(
            Guid enrollmentId, [FromBody] ExamAnswersDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var en = await FindOwnedEnrollmentAsync(enrollmentId, data.Email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            var now = TimeHelper.GetPhilippineTime();
            var over = await RejectIfAttemptOverAsync(en, now, ExamAttemptRules.SubmissionGraceSeconds);
            if (over != null) return over;

            var error = ExamAttemptRules.Validate(en.Exam.Questions, data.Answers);
            if (error != null)
                return BadRequest(new { message = error });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                await ReplaceAnswersAsync(en, data.Answers);
                en.SubmittedAt = now;
                en.Status = EnrollmentStatus.Completed;
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Your exam has been submitted." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error submitting exam", error = ex.Message });
            }
        }

        // ---- helpers ----

        // Loads the enrollment only if it belongs to this email, with everything the
        // rules need (exam, its questions and options).
        private async Task<ExamEnrollment?> FindOwnedEnrollmentAsync(Guid enrollmentId, string email)
        {
            var key = email.Trim().ToLowerInvariant();
            return await _mindaEsDbContext.ExamEnrollments
                .Include(en => en.Exam)
                    .ThenInclude(e => e!.Questions)
                        .ThenInclude(q => q.Options)
                .Include(en => en.Applicant)
                .FirstOrDefaultAsync(en =>
                    en.ExamEnrollmentId == enrollmentId &&
                    en.Applicant != null &&
                    en.Applicant.Email.ToLower() == key);
        }

        // Returns a response to send back if this attempt can't accept more answers
        // (null = fine to continue). If time ran out or HR closed/archived the exam,
        // the attempt is finalized with whatever was autosaved, so it doesn't sit
        // "in progress" forever. This happens lazily, the next time the applicant's
        // browser touches the attempt — there's no background job.
        private async Task<ActionResult?> RejectIfAttemptOverAsync(
            ExamEnrollment en, DateTime now, int graceSeconds)
        {
            var exam = en.Exam!;

            if (en.SubmittedAt != null)
                return Conflict(new { message = "This exam has already been submitted." });
            if (en.StartedAt == null)
                return Conflict(new { message = "This exam hasn't been started." });

            if (ExamAttemptRules.IsPastDeadline(en.StartedAt.Value, exam.DurationMinutes, now, graceSeconds))
            {
                await FinalizeAttemptAsync(en, ExamAttemptRules.Deadline(en.StartedAt.Value, exam.DurationMinutes));
                return StatusCode(StatusCodes.Status410Gone,
                    new { message = "Time is up. Your saved answers have been submitted." });
            }

            // Deliberately checks the STORED status, not the display status: the
            // display status auto-closes at the scheduled end, which would cut off
            // someone who started late and still has their own time left. Only an
            // explicit Close or Archive by HR ends a running attempt early.
            if (exam.IsArchived || exam.Status == ExamStatus.Closed)
            {
                await FinalizeAttemptAsync(en, now);
                return StatusCode(StatusCodes.Status410Gone,
                    new { message = "This exam was closed. Your saved answers have been submitted." });
            }

            return null;
        }

        private async Task FinalizeAttemptAsync(ExamEnrollment en, DateTime submittedAt)
        {
            en.SubmittedAt = submittedAt;
            en.Status = EnrollmentStatus.Completed;
            await _mindaEsDbContext.SaveChangesAsync();
        }

        // Replace-all, like SaveQuestions. Deletes are saved before the inserts so a
        // unique (enrollment, question) index can't trip over a row that's being
        // removed and re-added in the same save. Runs inside the caller's transaction.
        //
        // ASSUMPTION: Answer has AnswerId, ExamEnrollmentId, QuestionId, EssayText and
        // SelectedOptions; AnswerOption has AnswerOptionId and QuestionOptionId; and
        // the DbContext exposes them as Answers / AnswerOptions. Adjust names to your
        // actual entities.
        private async Task ReplaceAnswersAsync(ExamEnrollment en, IEnumerable<SavedAnswerDto> answers)
        {
            var existing = await _mindaEsDbContext.Answers
                .Include(a => a.SelectedOptions)
                .Where(a => a.ExamEnrollmentId == en.ExamEnrollmentId)
                .ToListAsync();
            _mindaEsDbContext.Answers.RemoveRange(existing);
            await _mindaEsDbContext.SaveChangesAsync();

            foreach (var a in answers.Where(ExamAttemptRules.IsAnswered))
            {
                _mindaEsDbContext.Answers.Add(new Answer
                {
                    AnswerId = Guid.NewGuid(),
                    ExamEnrollmentId = en.ExamEnrollmentId,
                    QuestionId = a.QuestionId,
                    EssayText = string.IsNullOrWhiteSpace(a.EssayText) ? null : a.EssayText,
                    SelectedOptions = (a.SelectedOptionIds ?? new List<Guid>())
                        .Distinct()
                        .Select(id => new AnswerOption
                        {
                            AnswerOptionId = Guid.NewGuid(),
                            QuestionOptionId = id,
                        })
                        .ToList(),
                });
            }
            await _mindaEsDbContext.SaveChangesAsync();
        }
    }
}