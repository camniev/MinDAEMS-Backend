using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MinDAES.Api.Domain.Entities
{
    [Table("USER_TYPES")]
    public class UserType
    {
        [Key][Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("UserTypeId")]
        public Guid UserTypeId { get; set; }

        [Column("UserTypeDesc")]
        [MaxLength(50)]
        public string? UserTypeDesc { get; set; }

        [Column("IsActive")]
        public bool IsActive { get; set; }

        // Navigation properties
        public ICollection<User>? User { get; set; }
    }
}
