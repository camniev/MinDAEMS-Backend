using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Domain.Entities;

// ASSUMPTIONS — adjust to your project (I haven't seen these):
//   * the DbContext type is MinDAEsDbContext, and TimeHelper lives where your other code finds it
//   * PDFs are stored IN THE DATABASE (varbinary(max)). That keeps them in the same backups
//     and transactions as the exam, which is simplest at exam-reference scale (<= 10 MB,
//     a few per exam). If you'd rather store files on disk or blob storage, only the
//     Content column and the two places that read/write it change.

// =====================================================================================
// 1) ENTITY  (new file Domain/Entities/ExamAttachment.cs)
// =====================================================================================
namespace MinDAES.Api.Domain.Entities
{
    [Table("EXAM_ATTACHMENTS")]
    public class ExamAttachment
    {
        [Key]
        [Column("ExamAttachmentId")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("ExamId")]
        [ForeignKey(nameof(Exam))]
        public Guid ExamId { get; set; }
        public Exam? Exam { get; set; }

        [Required, MaxLength(255)]
        [Column("FileName")]
        public string FileName { get; set; } = string.Empty;

        [Column("SizeBytes")]
        public long SizeBytes { get; set; }

        [Required]
        [Column("Content")] // varbinary(max)
        public byte[] Content { get; set; } = Array.Empty<byte>();

        [Column("UploadedAt")]
        public DateTime UploadedAt { get; set; }
    }
}