using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MinDAES.Api.Domain.Entities
{
    //add table attribute to specify the table name (users)
    [Table("USERS")]
    public record User
    {
        [Key]
        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("UserId")]
        public Guid UserId { get; set; }
        [Column("Username")]
        [MaxLength(50)]
        public string? UserName {  get; set; }
        [Column("Password")]
        [MaxLength(255)]
        public string? PassWord { get; set; }

        [Column("DatePasswordLastSet")]
        public DateTime? DatePasswordLastSet { get; set; }

        [Column("MustChangePasswordOnFirstLogin")]
        public bool? MustChangePasswordOnFirstLogin { get; set; }

        [Column("RefreshToken")]
        [MaxLength(255)]
        public string? RefreshToken { get; set; }

        [Column("RefreshTokenExpiryTime")]
        public DateTime? RefreshTokenExpiryTime { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        [ForeignKey("UserType")]
        public Guid? UserTypeId { get; set; }
        public UserType? UserType { get; set; }

        [ForeignKey("Employee")]
        public Guid? EmpId {  get; set; }
        public Employee? Employee { get; set; }

        //navigation properties
        public ICollection<Exam> CreatedExams { get; set; } = new List<Exam>();
    }
}
