using System.ComponentModel.DataAnnotations;

namespace MinDAES.Api.Domain.DTOs
{
    public class EnrollApplicantsDto
    {
        [Required]
        public List<ApplicantEnrollmentDto> Applicants { get; set; } = new();
    }

    public class ApplicantEnrollmentDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(256), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ApplicantCode { get; set; }
    }

    public class ApplicantExamScheduleResponseDto
    {
        public Guid ExamId { get; set; }
        public Guid ExamEnrollmentId { get; set; }
        public string ExamTitle { get; set; } = string.Empty;
        public DateOnly ExamDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public string? Instructions { get; set; }
        public string Status { get; set; } = string.Empty; // the exam's own status — will always be "Ongoing" here
        public string EnrollmentStatus { get; set; } = string.Empty; // this applicant's own roster status
        public decimal? Score { get; set; } // this applicant's own score only
    }
}