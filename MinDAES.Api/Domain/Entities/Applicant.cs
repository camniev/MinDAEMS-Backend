using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    public class Applicant
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ApplicantId")]
        public Guid ApplicantId { get; set; } = Guid.NewGuid();

        [Column("FullName")]
        public string FullName { get; set; } = string.Empty;

        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Column("ApplicantCode")]
        public string ApplicantCode { get; set; } = string.Empty;

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ExamEnrollment> Enrollments { get; set; } = new List<ExamEnrollment>();
    }
}
