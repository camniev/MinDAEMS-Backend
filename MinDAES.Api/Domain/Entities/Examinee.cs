namespace MinDAES.Api.Domain.Entities
{
    public class Examinee
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string? Department { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ExamEnrollment> Enrollments { get; set; } = new List<ExamEnrollment>();
    }
}
