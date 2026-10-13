using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Domain.Entities
{
    [Table("EXAM_ENROLLMENTS")]
    public class ExamEnrollment
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ExamEnrollmentId")]
        public Guid ExamEnrollmentId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ExamId")]
        [ForeignKey(nameof(Exam))]
        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        [Required]
        [Column("ApplicantId")]
        [ForeignKey(nameof(Applicant))]
        public Guid ApplicantId { get; set; }
        public Applicant? Applicant { get; set; }

        [Required]
        [Column("Status")]
        public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Invited;

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal? Score { get; set; }

        [Column("InvitedAt")]
        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

        [Column("StartedAt")]
        public DateTime? StartedAt { get; set; }

        [Column("SubmittedAt")]
        public DateTime? SubmittedAt { get; set; }

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
