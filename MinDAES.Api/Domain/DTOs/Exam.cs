using Microsoft.Identity.Client;

namespace MinDAES.Api.Domain.DTOs
{
    public class ExamDto
    {
        public Guid? ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string ExamCode { get; set; }
        public DateOnly ExamDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PassingScore { get; set; }
        public string Instructions { get; set; }
        public string AccessType { get; set; }
        public Guid UserId { get; set; }
    }

    // ---- Response DTOs ----
    public class ExamScheduleResponseDto
    {
        public Guid ExamId { get; set; }
        public string ExamTitle { get; set; }
        public DateOnly ExamDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PassingScore { get; set; }
        public string Instructions { get; set; }
        public string AccessType { get; set; }
        public string ExamCode { get; set; }
        public string Status { get; set; } // computed: Upcoming / Active / Completed
        public List<ExamQuestionResponseDto> Questions { get; set; } = new();
        public List<ExamApplicantResponseDto> Applicants { get; set; } = new();
    }

    public class ExamQuestionResponseDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } // mcq / checkbox / truefalse / essay
        public int Points { get; set; }
        public bool Required { get; set; }
        public string Text { get; set; }
        public List<ExamQuestionOptionResponseDto> Options { get; set; } = new();
    }

    public class ExamQuestionOptionResponseDto
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
    }

    public class ExamApplicantResponseDto
    {
        // Id = the enrollment row's own id, not the applicant's — matters once you
        // add remove/resend-invite endpoints, since those act on the enrollment
        // (this exam + this applicant), not the applicant record globally.
        public Guid ExamEnrollmentId { get; set; }
        public Guid ApplicantId { get; set; }
        public string Name { get; set; }
        public string ApplicantCode { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public decimal? Score { get; set; }
    }

    public class ArchiveExamDto
    {
        public Guid? UserId { get; set; }
    }
}