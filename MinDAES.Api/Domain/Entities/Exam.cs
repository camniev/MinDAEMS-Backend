using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Domain.Entities;

namespace MinDAES.Api.Domain.Entities
{
    [Table("EXAMS")]
    public class Exam
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ExamId")]
        public Guid ExamId { get; set; } = Guid.NewGuid();

        [Column("ExamTitle")]
        [MaxLength(255)]
        public string ExamTitle { get; set; } = string.Empty;

        [Column("ExamCode")] // Exam Code
        [MaxLength(50)]
        public string ExamCode { get; set; } = string.Empty;

        [Column("ExamDate")]
        public DateOnly ExamDate { get; set; }

        [Column("StartTime")]
        public TimeOnly StartTime { get; set; }

        [Column("DurationMinutes")]
        public int DurationMinutes { get; set; }

        [Column("PassingScore")]
        public int PassingScore { get; set; } // percentage, 1-100

        [Column("Instructions")]
        [MaxLength(4000)]
        public string? Instructions { get; set; }

        [Column("AccessType")]
        public ExamAccessType AccessType { get; set; } = ExamAccessType.Private;

        [Column("Status")]
        public ExamStatus Status { get; set; } = ExamStatus.Upcoming;

        [Column("CreatedByUserId")]
        [ForeignKey(nameof(CreatedBy))]
        public Guid CreatedByUserId { get; set; }
        public User? CreatedBy { get; set; } //rel with users table

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<ExamEnrollment> Enrollments { get; set; } = new List<ExamEnrollment>();
    }
}
