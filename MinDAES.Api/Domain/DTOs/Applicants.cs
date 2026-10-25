using System.ComponentModel.DataAnnotations;

namespace MinDAES.Api.Domain.DTOs
{
    public class EnrollApplicantsDto
    {
        [Required]
        public List<ApplicantEnrollmentDto> Applicants { get; set; } = new();
    }

    public class ApplicantEnrollmentDto
    {
        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(256), EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? ApplicantCode { get; set; }
    }
}