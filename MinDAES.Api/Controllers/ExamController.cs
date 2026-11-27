using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;
using System.Linq.Expressions;
using System.Text.Json;

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
                return BadRequest(new { message = "Request header is required." });

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
                return Ok(new { message = "New exam schedule added successfully." });
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

        // TODO: the controller's other actions are open (no token). This one is protected; the client
        // sends the login token for it. Change the roles to whoever may edit schedules.
        [Authorize(Roles = AppRoles.Assigners)]
        [HttpPut("{examId:guid}")]
        public async Task<ActionResult> UpdateExamSchedule(Guid examId, [FromBody] UpdateExamScheduleDto? data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });

            if (exam.IsArchived)
                return Conflict(new { message = "An archived exam schedule can't be edited. Restore it first." });
            if (exam.Status != ExamStatus.Upcoming && exam.Status != ExamStatus.Ongoing)
                return Conflict(new { message = $"A {exam.Status} exam schedule can't be edited." });

            // ---- validate and parse the new values ----
            var errors = new List<string>();
            if (!DateOnly.TryParse(data.ExamDate, out var examDate)) errors.Add("Exam date is not a valid date.");
            if (!TimeOnly.TryParse(data.StartTime, out var startTime)) errors.Add("Start time is not a valid time.");
            if (data.DurationMinutes is < 1 or > 1440) errors.Add("Duration must be between 1 and 1440 minutes.");
            if (data.PassingScore is < 1 or > 100) errors.Add("Passing score must be between 1 and 100.");
            if (!Enum.TryParse<ExamAccessType>(data.AccessType, ignoreCase: true, out var accessType))
                errors.Add("Access type must be Private or Public.");
            var title = data.ExamTitle!.Trim();
            var code = data.ExamCode!.Trim();
            if (title.Length == 0) errors.Add("Exam title is required.");
            if (code.Length == 0 || code.Any(char.IsWhiteSpace)) errors.Add("Exam code is required and can't contain spaces.");
            if (errors.Count > 0)
                return BadRequest(new { message = string.Join(" ", errors), errors });

            var instructions = string.IsNullOrWhiteSpace(data.Instructions) ? null : data.Instructions.Trim();

            // ---- what is locked for this status ----
            var locked = new List<string>();
            if (exam.Status == ExamStatus.Ongoing)
            {
                if (code != exam.ExamCode) locked.Add("exam code");
                if (examDate != exam.ExamDate) locked.Add("exam date");
                if (startTime != exam.StartTime) locked.Add("start time");
                if (data.DurationMinutes != exam.DurationMinutes) locked.Add("duration");
                if (data.PassingScore != exam.PassingScore) locked.Add("passing score");
                if (accessType != exam.AccessType) locked.Add("access type");
            }
            else if (code != exam.ExamCode && await _mindaEsDbContext.ExamEnrollments.AnyAsync(en => en.ExamId == examId))
            {
                // Applicant codes already issued begin with the old exam code.
                locked.Add("exam code");
            }
            if (locked.Count > 0)
            {
                var why = exam.Status == ExamStatus.Ongoing
                    ? "can't be changed while the exam is ongoing"
                    : "can't be changed once applicants are enrolled";
                return Conflict(new { message = $"The {string.Join(", ", locked)} {why}." });
            }

            // ---- apply ----
            exam.ExamTitle = title;
            exam.Instructions = instructions;
            if (exam.Status == ExamStatus.Upcoming)
            {
                exam.ExamCode = code;
                exam.ExamDate = examDate;
                exam.StartTime = startTime;
                exam.DurationMinutes = data.DurationMinutes;
                exam.PassingScore = data.PassingScore;
                exam.AccessType = accessType;
            }
            exam.UpdatedAt = TimeHelper.GetPhilippineTime();

            try
            {
                await _mindaEsDbContext.SaveChangesAsync();
                return Ok(new { message = "Exam schedule updated." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating exam schedule", error = ex.Message });
            }
        }

        [HttpGet("GetExamSchedules")]
        public async Task<ActionResult<List<ExamScheduleResponseDto>>> GetExamSchedules(
            [FromQuery] bool includeArchived = false)
        {
            var now = TimeHelper.GetPhilippineTime();

            var query = _mindaEsDbContext.Exams.AsNoTracking();
            if (!includeArchived)
                query = query.Where(e => !e.IsArchived);

            var exams = await query
                .Include(e => e.Questions)
                    .ThenInclude(q => q.Options)
                .Include(e => e.Enrollments)
                    .ThenInclude(en => en.Applicant)
                .OrderByDescending(e => e.ExamDate)
                    .ThenByDescending(e => e.StartTime)
                .ToListAsync();

            // PDF: metadata only, in a separate query. Do NOT .Include(e => e.Attachments) above �
            // that would load every PDF's bytes just to list file names.
            var examIds = exams.Select(e => e.ExamId).ToList();
            var attachmentRows = await _mindaEsDbContext.Set<ExamAttachment>()
                .AsNoTracking()
                .Where(a => examIds.Contains(a.ExamId))
                .OrderBy(a => a.UploadedAt)
                .Select(a => new { a.ExamId, a.Id, a.FileName, a.SizeBytes })
                .ToListAsync();
            var attachmentsByExam = attachmentRows
                .GroupBy(a => a.ExamId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(a => new ExamAttachmentResponseDto
                    {
                        Id = a.Id,
                        FileName = a.FileName,
                        SizeBytes = a.SizeBytes
                    }).ToList());

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
                Status = exam.Status.ToString(),

                // PDF: the exam's uploaded reference PDFs
                Attachments = attachmentsByExam.TryGetValue(exam.ExamId, out var examAttachments)
                    ? examAttachments
                    : new List<ExamAttachmentResponseDto>(),

                // ORDER: Include() gives no guaranteed order, and question ids are random Guids, so the
                // saved order could come back shuffled. OrderIndex is what SaveQuestions writes.
                Questions = exam.Questions?.OrderBy(q => q.OrderIndex).Select(q => new ExamQuestionResponseDto
                {
                    Id = q.QuestionId,
                    Type = MapQuestionType(q.Type),
                    Points = q.Points,
                    Required = q.IsRequired,
                    Text = q.Text,
                    AttachmentId = q.AttachmentId, // PDF: which uploaded PDF this question links to
                    Options = q.Options?.OrderBy(o => o.OrderIndex).Select(o => new ExamQuestionOptionResponseDto
                    {
                        Text = o.Text,
                        IsCorrect = o.IsCorrect,
                    }).ToList() ?? new List<ExamQuestionOptionResponseDto>(),
                }).ToList() ?? new List<ExamQuestionResponseDto>(),

                Applicants = exam.Enrollments?.Select(en => new ExamApplicantResponseDto
                {
                    ExamEnrollmentId = en.ExamEnrollmentId,
                    ApplicantId = en.ApplicantId,
                    Name = en.Applicant?.FullName,
                    Email = en.Applicant?.Email,
                    ApplicantCode = en.ApplicantCode, // now read from the enrollment, not the applicant
                    Status = en.Status.ToString(),
                    Score = en.Score,
                }).ToList() ?? new List<ExamApplicantResponseDto>(),
            }).ToList();

            return Ok(result);
        }

        [HttpPatch("{examId:guid}/Open")]
        public async Task<ActionResult> OpenExamSchedule(Guid examId)
        {
            var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });

            if (exam.IsArchived)
                return Conflict(new { message = "Cannot open an archived exam schedule. Restore it first." });

            if (exam.Status != ExamStatus.Upcoming)
                return Conflict(new { message = $"Cannot open an exam schedule that is already {exam.Status}." });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                exam.Status = ExamStatus.Ongoing;
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Exam schedule opened." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error opening exam schedule", error = ex.Message });
            }
        }

        // ---- Close ----

        //[HttpPatch("{examId:guid}/Close")]
        //public async Task<ActionResult> CloseExamSchedule(Guid examId)
        //{
        //    var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
        //    if (exam == null)
        //        return NotFound(new { message = "Exam schedule not found." });

        //    if (exam.IsArchived)
        //        return Conflict(new { message = "Cannot close an archived exam schedule." });

        //    if (exam.Status != ExamStatus.Ongoing)
        //        return Conflict(new { message = $"Cannot close an exam schedule that is {exam.Status}, not Ongoing." });

        //    await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
        //    try
        //    {
        //        exam.Status = ExamStatus.Closed;
        //        await _mindaEsDbContext.SaveChangesAsync();
        //        await transaction.CommitAsync();
        //        return Ok(new { message = "Exam schedule closed." });
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return StatusCode(500, new { message = "Error closing exam schedule", error = ex.Message });
        //    }
        //}

        [HttpPatch("{examId:guid}/Close")]
        public async Task<ActionResult> CloseExamSchedule(Guid examId)
        {
            var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });

            if (exam.IsArchived)
                return Conflict(new { message = "Cannot close an archived exam schedule." });

            if (exam.Status != ExamStatus.Ongoing)
                return Conflict(new { message = $"Cannot close an exam schedule that is {exam.Status}, not Ongoing." });

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                exam.Status = ExamStatus.Closed;

                // Same transaction: the exam is closed and the logins are removed, or neither happens.
                var accountsDeleted = await RemoveApplicantAccountsAsync(examId);

                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new
                {
                    message = accountsDeleted > 0
                        ? $"Exam schedule closed. {accountsDeleted} applicant account(s) removed."
                        : "Exam schedule closed.",
                    accountsDeleted
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error closing exam schedule", error = ex.Message });
            }
        }

        // Hard-deletes the login accounts (Users rows plus their linked Employee rows) of this
        // exam's roster, with two guards:
        //  1. Only users of type "Applicant" � an applicant's email can match a staff account's
        //     username, and that must never be deleted.
        //  2. Skips anyone still enrolled in another exam that is Upcoming or Ongoing. One account
        //     is shared across every exam an applicant is enrolled in (RegisterApplicants reuses it),
        //     so deleting it here would lock them out of their other exams.
        // Call it from EVERY code path that sets an exam to Closed (including your auto-close when
        // the scheduled window passes), and call SaveChangesAsync afterwards.
        private async Task<int> RemoveApplicantAccountsAsync(Guid examId)
        {
            var applicantTypeId = await _mindaEsDbContext.UserTypes
                .Where(ut => ut.UserTypeDesc == "Applicant")
                .Select(ut => (Guid?)ut.UserTypeId)
                .SingleOrDefaultAsync();
            if (applicantTypeId == null) return 0;

            var rosterEmails = await _mindaEsDbContext.ExamEnrollments
                .Where(en => en.ExamId == examId && en.Applicant.Email != null)
                .Select(en => en.Applicant.Email!)
                .Distinct()
                .ToListAsync();
            if (rosterEmails.Count == 0) return 0;

            var stillNeeded = await _mindaEsDbContext.ExamEnrollments
                .Where(en => en.ExamId != examId
                          && rosterEmails.Contains(en.Applicant.Email!)
                          && !en.Exam.IsArchived
                          && (en.Exam.Status == ExamStatus.Upcoming || en.Exam.Status == ExamStatus.Ongoing))
                .Select(en => en.Applicant.Email!)
                .Distinct()
                .ToListAsync();

            var toDelete = rosterEmails.Except(stillNeeded, StringComparer.OrdinalIgnoreCase).ToList();
            if (toDelete.Count == 0) return 0;

            var users = await _mindaEsDbContext.Users
                .Include(u => u.Employee)
                .Where(u => u.UserTypeId == applicantTypeId
                         && u.UserName != null
                         && toDelete.Contains(u.UserName))
                .ToListAsync();

            // The Employee row created alongside each account goes with it. It is found through the
            // user's own EmpId (not by email), and only if it is an "Applicant" row, so a staff
            // Employee record can never be removed here. EF deletes the User before its Employee.
            var employees = users
                .Select(u => u.Employee)
                .Where(e => e != null && e.Position == "Applicant")
                .Select(e => e!)
                .ToList();

            _mindaEsDbContext.Users.RemoveRange(users);
            _mindaEsDbContext.Employees.RemoveRange(employees);

            return users.Count;
        }

        private static string MapQuestionType(QuestionType type) => type switch
        {
            QuestionType.MultipleChoice => "mcq",
            QuestionType.Checkbox => "checkbox",
            QuestionType.TrueFalse => "truefalse",
            QuestionType.Essay => "essay",
            _ => "mcq",
        };

        // private static string ComputeExamStatus(
        //     bool isArchived, DateOnly examDate, TimeOnly startTime, int durationMinutes, DateTime nowPh)
        // {
        //     if (isArchived) return "Archived";
        
        //     var examStart = examDate.ToDateTime(startTime);
        //     var examEnd = examStart.AddMinutes(durationMinutes);
        //     if (nowPh < examStart) return "Upcoming";
        //     if (nowPh <= examEnd) return "Ongoing";
        //     return "Closed";
        // }

        // ---- Archive ----
        [HttpPatch("{examId:guid}/Archive")]
        public async Task<ActionResult> ArchiveExamSchedule(Guid examId, [FromBody] ArchiveExamDto data)
        {
            var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });
        
            if (exam.IsArchived)
                return Ok(new { message = "Exam schedule is already archived." });
        
            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                exam.IsArchived = true;
                exam.ArchivedAt = TimeHelper.GetPhilippineTime();
                exam.ArchivedByUserId = data?.UserId;
        
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
        
                return Ok(new { message = "Exam schedule archived." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error archiving exam schedule", error = ex.Message });
            }
        }
        
        // ---- Restore ----
        
        [HttpPatch("{examId:guid}/Restore")]
        public async Task<ActionResult> RestoreExamSchedule(Guid examId)
        {
            var exam = await _mindaEsDbContext.Exams.FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });
        
            if (!exam.IsArchived)
                return Ok(new { message = "Exam schedule is not archived." });
        
            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();
            try
            {
                exam.IsArchived = false;
                exam.ArchivedAt = null;
                exam.ArchivedByUserId = null;
        
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
        
                return Ok(new { message = "Exam schedule restored." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error restoring exam schedule", error = ex.Message });
            }
        }
    }
}