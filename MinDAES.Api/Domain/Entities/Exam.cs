using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Domain.Entities
{
    public class Exam
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateOnly ExamDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public int PassingScore { get; set; } // percentage, 1-100
        public string? Instructions { get; set; }
        public ExamAccessType AccessType { get; set; } = ExamAccessType.Private;
        public string AccessCode { get; set; } = string.Empty;
        public ExamStatus Status { get; set; } = ExamStatus.Upcoming;

        //public Guid CreatedByUserId { get; set; }
        //public AppUser? CreatedBy { get; set; } //rel with users table

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<ExamEnrollment> Enrollments { get; set; } = new List<ExamEnrollment>();
    }
}
