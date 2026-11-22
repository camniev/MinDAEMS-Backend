using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;
using System.Linq.Expressions;
using System.Security.Claims;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApplicantController : ControllerBase
    {
        private readonly MinDAESDbContext _mindaEsDbContext;
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ApplicantAccountService _accounts;
        public ApplicantController(MinDAESDbContext mindaEsDbContext, IHubContext<NotificationHub> hubContext, ApplicantAccountService accounts)
        {
            _mindaEsDbContext = mindaEsDbContext;
            _hubContext = hubContext;
            _accounts = accounts;
        }

        // add applicants
        [HttpPost("{examId:guid}/EnrollApplicants")]
        public async Task<ActionResult> EnrollApplicants(Guid examId, [FromBody] EnrollApplicantsDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (data.Applicants.Count == 0)
                return BadRequest(new { message = "At least one applicant is required." });

            var applicantTypeId = await _accounts.GetApplicantUserTypeIdAsync();
            if (applicantTypeId == null)
                return StatusCode(500, new { message = "The 'Applicant' user type was not found." });

            for (var attempt = 1; ; attempt++)
            {
                await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
                try
                {
                    // Tracked (no AsNoTracking): its serial counter is updated below.
                    var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
                    if (exam == null)
                        return NotFound(new { message = "Exam schedule not found." });

                    // New: a closed exam's logins are removed when it closes, so adding people to it
                    // would create accounts nothing ever cleans up. Delete this check if you disagree.
                    if (exam.IsArchived || (exam.Status != ExamStatus.Upcoming && exam.Status != ExamStatus.Ongoing))
                        return Conflict(new { message = "Applicants can only be added to an upcoming or ongoing exam." });

                    if (string.IsNullOrWhiteSpace(exam.ExamCode))
                        return Conflict(new { message = "This exam has no exam code, so applicant codes cannot be generated." });

                    var incoming = data.Applicants
                        .Select(a => new { Email = a.Email.Trim(), EmailKey = a.Email.Trim().ToLowerInvariant(), Name = a.Name.Trim() })
                        .GroupBy(a => a.EmailKey)
                        .Select(g => g.First())
                        .ToList();
                    var emailKeys = incoming.Select(a => a.EmailKey).ToList();

                    // Applicants are a directory independent of any one exam: reuse, don't duplicate.
                    var existingApplicants = await _mindaEsDbContext.Applicants
                        .Where(a => emailKeys.Contains(a.Email.ToLower()))
                        .ToListAsync();
                    var applicantsByEmail = existingApplicants.ToDictionary(a => a.Email.ToLowerInvariant(), a => a);

                    // Already on THIS exam's roster: re-importing the same person is a no-op.
                    var existingApplicantIds = existingApplicants.Select(a => a.ApplicantId).ToList();
                    var alreadyEnrolledIds = (await _mindaEsDbContext.ExamEnrollments
                            .Where(en => en.ExamId == examId && existingApplicantIds.Contains(en.ApplicantId))
                            .Select(en => en.ApplicantId)
                            .ToListAsync())
                        .ToHashSet();

                    var skippedEmails = new List<string>();
                    var toEnroll = new List<(Applicant Applicant, string Email, string Name)>();

                    foreach (var entry in incoming)
                    {
                        if (!applicantsByEmail.TryGetValue(entry.EmailKey, out var applicant))
                        {
                            applicant = new Applicant
                            {
                                ApplicantId = Guid.NewGuid(),
                                FullName = entry.Name,
                                Email = entry.Email,
                                CreatedAt = TimeHelper.GetPhilippineTime(),
                            };
                            _mindaEsDbContext.Applicants.Add(applicant);
                            applicantsByEmail[entry.EmailKey] = applicant;
                        }

                        if (alreadyEnrolledIds.Contains(applicant.ApplicantId))
                        {
                            skippedEmails.Add(entry.Email);
                            continue;
                        }

                        toEnroll.Add((applicant, entry.Email, entry.Name));
                        alreadyEnrolledIds.Add(applicant.ApplicantId); // guards a same-batch duplicate too
                    }

                    // Reserve the serials and give each applicant their code, in the order sent.
                    var firstSerial = exam.LastApplicantSerial + 1;
                    if (toEnroll.Count > 0) exam.LastApplicantSerial += toEnroll.Count;

                    var accountRequests = new List<ApplicantAccountRequest>();
                    for (var i = 0; i < toEnroll.Count; i++)
                    {
                        var code = ApplicantCodes.Format(exam.ExamCode, firstSerial + i);
                        _mindaEsDbContext.ExamEnrollments.Add(new ExamEnrollment
                        {
                            ExamEnrollmentId = Guid.NewGuid(),
                            ExamId = examId,
                            ApplicantId = toEnroll[i].Applicant.ApplicantId,
                            ApplicantCode = code,
                            Status = EnrollmentStatus.Invited,
                            InvitedAt = TimeHelper.GetPhilippineTime(),
                        });
                        accountRequests.Add(new ApplicantAccountRequest(toEnroll[i].Name, toEnroll[i].Email, code, exam.ExamTitle));
                    }

                    // Logins with the same codes, in the same transaction.
                    var plan = await _accounts.StageAccountsAsync(accountRequests, applicantTypeId.Value);

                    await _mindaEsDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Only now that everything is committed: email each applicant their login details.
                    await _accounts.QueueEmailsAsync(plan);

                    return Ok(new
                    {
                        message = skippedEmails.Count > 0
                            ? $"Enrolled {toEnroll.Count} applicant(s); {skippedEmails.Count} were already enrolled and skipped."
                            : $"Enrolled {toEnroll.Count} applicant(s).",
                        enrolledCount = toEnroll.Count,
                        skippedCount = skippedEmails.Count,
                        skippedEmails,
                        emailsQueued = plan.Created.Count + plan.PasswordUpdated.Count,
                        accountConflicts = plan.Conflicts, // emails that belong to a staff account: enrolled, but no login created
                    });
                }
                catch (DbUpdateConcurrencyException) when (attempt < 3)
                {
                    // Someone else enrolled applicants at the same moment and moved the counter.
                    // Start over with the fresh value so the two batches get different numbers.
                    await transaction.RollbackAsync();
                    _mindaEsDbContext.ChangeTracker.Clear();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new { message = "Error enrolling applicants", error = ex.Message });
                }
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

        private const string ApplicantRole = "Applicant";

        private string? CurrentApplicantEmail()
        {
            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
            return string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        }

        private ObjectResult NoEmailOnAccount() =>
            StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Your account has no email address, so your exams can't be found. Please contact the administrator."
            });

        // GET api/Applicant/MySchedules
        // The signed-in applicant's own exams that are open right now.
        [Authorize(Roles = ApplicantRole)]
        [HttpGet("MySchedules")]
        public async Task<ActionResult<List<ApplicantExamScheduleResponseDto>>> GetMySchedules()
        {
            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            var now = TimeHelper.GetPhilippineTime();

            var enrollments = await _mindaEsDbContext.ExamEnrollments
                .AsNoTracking()
                .Include(en => en.Exam)
                .Include(en => en.Applicant)
                .Where(en => en.Applicant != null && en.Applicant.Email.ToLower() == email)
                .Where(en => en.Exam != null && !en.Exam.IsArchived)
                .ToListAsync();

            var result = enrollments
                .Select(en => new { en, status = ExamStatusHelper.ComputeDisplayStatus(en.Exam!, now) })
                .Where(x => x.status == "Ongoing")   // applicants only see exams that are open
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

        // POST api/Applicant/MyExam/{enrollmentId}/Events
        [Authorize(Roles = ApplicantRole)]
        [HttpPost("MyExam/{enrollmentId:guid}/Events")]
        public async Task<IActionResult> ReportExamEvent(Guid enrollmentId, [FromBody] ExamEventRequest? body)
        {
            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            if (body?.Type == null || !ExamIncidentAlert.AllowedTypes.Contains(body.Type))
                return BadRequest(new { message = "Unknown event type." });

            // Same "is this my enrollment" rule as FindOwnedEnrollmentAsync, without loading the
            // whole question set on every event.
            var en = await _mindaEsDbContext.ExamEnrollments
                .Include(e => e.Exam)
                .Include(e => e.Applicant)
                .FirstOrDefaultAsync(e => e.ExamEnrollmentId == enrollmentId
                                        && e.Applicant != null
                                        && e.Applicant.Email.ToLower() == email);
            if (en?.Exam == null || en.Applicant == null)
                return NotFound(new { message = "Exam enrollment not found." });

            // Only while the attempt is actually in progress.
            if (en.StartedAt == null || en.SubmittedAt != null)
                return Conflict(new { message = "This exam is not in progress." });

            var now = TimeHelper.GetPhilippineTime();

            // A misbehaving page (or someone calling the API by hand) must not flood the proctors.
            var recent = await _mindaEsDbContext.Set<ExamIncident>()
                .CountAsync(i => i.ExamEnrollmentId == enrollmentId && i.OccurredAt >= now.AddMinutes(-10));
            if (recent >= 120)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { message = "Too many events." });

            var incident = new ExamIncident
            {
                ExamEnrollmentId = enrollmentId,
                Type = body.Type,
                AwaySeconds = body.AwaySeconds is >= 0 and <= 86_400 ? body.AwaySeconds : null,
                OccurredAt = now
            };
            _mindaEsDbContext.Set<ExamIncident>().Add(incident);
            await _mindaEsDbContext.SaveChangesAsync();

            // Live push to every logged-in staff member. A hub hiccup must not fail the report,
            // which is already saved.
            try
            {
                await _hubContext.Clients.Group(NotificationHub.ProctorsGroup).SendAsync("ReceiveNotification",
                    new ExamIncidentAlert("ExamIncident", incident.Id, en.ExamId, en.Exam.ExamTitle, en.ExamEnrollmentId,
                        en.ApplicantCode, en.Applicant.FullName, incident.Type,
                        ExamIncidentAlert.SeverityOf(incident.Type), incident.AwaySeconds, incident.OccurredAt));
            }
            catch (Exception)
            {
                // log it; the incident is in the database and shows up in the proctor's history
            }

            return NoContent();
        }

        // GET api/Applicant/MyExam/{enrollmentId}/Attachments/{attachmentId}
        // A reference PDF linked to one of this exam's questions.
        [Authorize(Roles = ApplicantRole)]
        [HttpGet("MyExam/{enrollmentId:guid}/Attachments/{attachmentId:guid}")]
        public async Task<IActionResult> GetMyExamAttachment(Guid enrollmentId, Guid attachmentId)
        {
            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            var en = await FindOwnedEnrollmentAsync(enrollmentId, email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            // Reference material is only available once the attempt has started and while it is open.
            if (en.StartedAt == null)
                return Conflict(new { message = "The exam has not been started." });
            var now = TimeHelper.GetPhilippineTime();
            var over = await RejectIfAttemptOverAsync(en, now, 0);
            if (over != null) return over;
        
            // Only a PDF that one of THIS exam's questions actually links to.
            var allowed = await _mindaEsDbContext.Set<Question>()
                .AnyAsync(q => q.ExamId == en.ExamId && q.AttachmentId == attachmentId);
            if (!allowed)
                return NotFound(new { message = "Attachment not found." });
        
            var file = await _mindaEsDbContext.Set<ExamAttachment>().AsNoTracking()
                .Where(a => a.Id == attachmentId)
                .Select(a => new { a.FileName, a.Content })
                .FirstOrDefaultAsync();
            if (file == null)
                return NotFound(new { message = "Attachment not found." });
        
            return PdfResponse.Inline(this, file.FileName, file.Content);
        }

        // POST api/Applicant/MyExam/{enrollmentId}/Start
        // Begins the attempt (records StartedAt the first time), or resumes it if
        // already started — calling it again never restarts the clock.
        [Authorize(Roles = ApplicantRole)]
        [HttpPost("MyExam/{enrollmentId:guid}/Start")]
        public async Task<ActionResult<ExamSessionResponseDto>> StartMyExam(Guid enrollmentId)
        {
            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            var en = await FindOwnedEnrollmentAsync(enrollmentId, email);
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

            // Reference PDFs: metadata only. Never Include() the attachment here, or every
            // PDF's bytes would be loaded just to list file names.
            var attachments = await _mindaEsDbContext.Set<ExamAttachment>()
                .AsNoTracking()
                .Where(a => a.ExamId == exam.ExamId)
                .Select(a => new ApplicantAttachmentDto { Id = a.Id, FileName = a.FileName, SizeBytes = a.SizeBytes })
                .ToDictionaryAsync(a => a.Id);

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
                        Attachment = q.AttachmentId != null && attachments.TryGetValue(q.AttachmentId.Value, out var att)
                            ? att
                            : null,
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
        [Authorize(Roles = ApplicantRole)]
        [HttpPut("MyExam/{enrollmentId:guid}/Answers")]
        public async Task<ActionResult> SaveMyExamAnswers(
            Guid enrollmentId, [FromBody] ExamAnswersDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            var en = await FindOwnedEnrollmentAsync(enrollmentId, email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            var now = TimeHelper.GetPhilippineTime();
            var over = await RejectIfAttemptOverAsync(en, now, ExamAttemptRules.SubmissionGraceSeconds);
            if (over != null) return over;

            var essayError = SanitizeEssays(data);
            if (essayError != null) return BadRequest(new { message = essayError });

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
        [Authorize(Roles = ApplicantRole)]
        [HttpPost("MyExam/{enrollmentId:guid}/Submit")]
        public async Task<ActionResult> SubmitMyExam(
            Guid enrollmentId, [FromBody] ExamAnswersDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = CurrentApplicantEmail();
            if (email == null) return NoEmailOnAccount();

            var en = await FindOwnedEnrollmentAsync(enrollmentId, email);
            if (en?.Exam == null)
                return NotFound(new { message = "Exam enrollment not found." });

            var now = TimeHelper.GetPhilippineTime();
            var over = await RejectIfAttemptOverAsync(en, now, ExamAttemptRules.SubmissionGraceSeconds);
            if (over != null) return over;

            var essayError = SanitizeEssays(data);
            if (essayError != null) return BadRequest(new { message = essayError });

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

        // Loads the enrollment only if it belongs to the signed-in applicant (their
        // normalized email), with everything the rules need.
        private async Task<ExamEnrollment?> FindOwnedEnrollmentAsync(Guid enrollmentId, string normalizedEmail)
        {
            return await _mindaEsDbContext.ExamEnrollments
                .Include(en => en.Exam)
                    .ThenInclude(e => e!.Questions)
                        .ThenInclude(q => q.Options)
                .Include(en => en.Applicant)
                .FirstOrDefaultAsync(en =>
                    en.ExamEnrollmentId == enrollmentId &&
                    en.Applicant != null &&
                    en.Applicant.Email.ToLower() == normalizedEmail);
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

        private static string? SanitizeEssays(ExamAnswersDto data)
        {
            foreach (var a in data.Answers ?? new())
            {
                if (a.EssayText is { Length: > EssayHtml.MaxHtmlLength })
                    return "An essay answer is too long.";
                a.EssayText = EssayHtml.Clean(a.EssayText);
            }
            return null;
        }
    }
}