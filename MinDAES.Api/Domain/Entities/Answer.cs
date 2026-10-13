using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    public class Answer
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("AnswerId")]
        public Guid AnswerId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ExamEnrollmentId")]
        [ForeignKey(nameof(ExamEnrollment))]
        public Guid ExamEnrollmentId { get; set; }
        public ExamEnrollment? ExamEnrollment { get; set; }

        [Required]
        [Column("QuestionId")]
        [ForeignKey(nameof(Question))]
        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }

        [Column("EssayText")]
        [MaxLength(10000)]
        public string? EssayText { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? PointsAwarded { get; set; }

        public ICollection<AnswerOption> SelectedOptions { get; set; } = new List<AnswerOption>();
    }

    [Table("ANSWER_OPTIONS")]
    public class AnswerOption
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("AnswerId")]
        [ForeignKey(nameof(Answer))]
        public Guid AnswerId { get; set; }
        public Answer? Answer { get; set; }

        [Required]
        [Column("QuestionOptionId")]
        [ForeignKey(nameof(QuestionOption))]
        public Guid QuestionOptionId { get; set; }
        public QuestionOption? QuestionOption { get; set; }
    }
}
