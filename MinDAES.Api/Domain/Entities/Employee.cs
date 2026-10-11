using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    [Table("EMPLOYEES")]
    public class Employee
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("EmpId")]
        public Guid EmpId { get; set; }

        [Column("EmpName")]
        [MaxLength(255)]
        public string? EmpName { get; set; }

        [Column("Position")]
        [MaxLength(50)]
        public string? Position { get; set; }

        [Column("EmailAddress")]
        [MaxLength(50)]
        public string? EmailAddress { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        // Navigation properties
        public User? User { get; set; }
        public ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();
    }
}
