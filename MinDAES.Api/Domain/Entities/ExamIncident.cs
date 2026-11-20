using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    [Table("EXAM_INCIDENTS")]
    public class ExamIncident
    {
        [Key]
        [Column("ExamIncidentId")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ExamEnrollmentId")]
        [ForeignKey(nameof(ExamEnrollment))]
        public Guid ExamEnrollmentId { get; set; }
        public ExamEnrollment? ExamEnrollment { get; set; }

        [Required, MaxLength(30)]
        [Column("Type")]
        public string Type { get; set; } = string.Empty;

        // For the "…Restored" types: how long the applicant was away (measured by their browser).
        [Column("AwaySeconds")]
        public int? AwaySeconds { get; set; }

        // Server time (Philippine time, like SubmittedAt). The browser's clock is not trusted.
        [Column("OccurredAt")]
        public DateTime OccurredAt { get; set; }
    }
}