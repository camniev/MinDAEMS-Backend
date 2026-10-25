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
                // De-dupe the incoming batch by email first. The frontend's bulk
                // import already does this, but a single-entry call (or any future
                // caller) might not, and two entries for the same email in one
                // request would otherwise race against each other below.
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
                // duplicated.
                var existingApplicants = await _mindaEsDbContext.Applicants
                    .Where(a => emailKeys.Contains(a.Email.ToLower()))
                    .ToListAsync();
                var applicantsByEmail = existingApplicants
                    .ToDictionary(a => a.Email.ToLowerInvariant(), a => a);

                // Applicants already on THIS exam's roster, so re-importing the same
                // person is a no-op rather than a duplicate enrollment row.
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
                            ApplicantCode = entry.ApplicantCode,
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
    }
}