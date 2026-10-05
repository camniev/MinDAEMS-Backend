using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Domain.Entities
{
    public class ExamEnrollment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        public Guid ExamineeId { get; set; }
        public Examinee? Examinee { get; set; }

        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Invited;
        public decimal? Score { get; set; }

        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
