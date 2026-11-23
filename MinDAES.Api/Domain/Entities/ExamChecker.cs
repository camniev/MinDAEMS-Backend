using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    [Table("EXAM_CHECKERS")]
    public class ExamChecker
    {
        [Column("ExamId")]
        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        [Column("UserId")]
        public Guid UserId { get; set; }
        public User? User { get; set; }

        [Column("AssignedAt")]
        public DateTime AssignedAt { get; set; }

        [Column("AssignedByUserId")]
        public Guid? AssignedByUserId { get; set; }
    }
}
