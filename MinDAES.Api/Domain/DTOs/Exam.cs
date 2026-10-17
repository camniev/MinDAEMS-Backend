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
}