using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Domain.Entities
{
    [Table("QUESTIONS")]
    public class Question
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("QuestionId")]
        public Guid QuestionId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ExamId")]
        [ForeignKey(nameof(Exam))]
        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        [Required]
        [Column("QuestionType")]
        public QuestionType Type { get; set; }

        [Required, MaxLength(2000)]
        [Column("Text")]
        public string Text { get; set; } = string.Empty;

        [Range(1, 1000)]
        [Column("Points")]
        public int Points { get; set; } = 5;

        [Column("IsRequired")]
        public bool IsRequired { get; set; } = true;

        [Column("OrderIndex")]
        public int OrderIndex { get; set; }

        public ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }

    [Table("QUESTION_OPTIONS")]
    public class QuestionOption
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("QuestionOptionId")]
        public Guid QuestionOptionId { get; set; } = Guid.NewGuid();

        [Required]
        [Column("QuestionId")]
        [ForeignKey(nameof(Question))]
        public Guid QuestionId { get; set; }
        public Question? Question { get; set; }

        [Required, MaxLength(1000)]
        [Column("Text")]
        public string Text { get; set; } = string.Empty;

        [Column("IsCorrect")]
        public bool IsCorrect { get; set; }

        [Column("OrderIndex")]
        public int OrderIndex { get; set; }
    }
}
