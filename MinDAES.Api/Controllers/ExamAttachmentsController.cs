using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Helpers;

namespace MinDAES.Api.Controllers
{
    [ApiController]
    [Route("api/Exam/{examId:guid}/Attachments")]
    public class ExamAttachmentsController : ControllerBase
    {
        public const int MaxBytes = 10 * 1024 * 1024; // keep in step with MAX_ATTACHMENT_MB in ExamContext.jsx
        private const int MaxPerExam = 20;
        private static readonly byte[] PdfSignature = Encoding.ASCII.GetBytes("%PDF-");

        private readonly MinDAESDbContext _minDAESDbContext;

        public ExamAttachmentsController(MinDAESDbContext minDAESDbContext) => _minDAESDbContext = minDAESDbContext;

        // POST: multipart/form-data with a "file" field -> { id, fileName, sizeBytes }
        [HttpPost]
        [RequestSizeLimit(MaxBytes + 1024 * 1024)] // the file plus a little form overhead
        public async Task<ActionResult> Upload(Guid examId, IFormFile? file)
        {
            var exam = await _minDAESDbContext.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.ExamId == examId);
            if (exam == null)
                return NotFound(new { message = "Exam schedule not found." });
            if (exam.IsArchived)
                return Conflict(new { message = "Cannot change an archived exam schedule." });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Choose a PDF file to upload." });
            if (file.Length > MaxBytes)
                return BadRequest(new { message = $"The PDF is larger than {MaxBytes / (1024 * 1024)} MB." });

            var existing = await _minDAESDbContext.Set<ExamAttachment>().CountAsync(a => a.ExamId == examId);
            if (existing >= MaxPerExam)
                return Conflict(new { message = $"An exam can have at most {MaxPerExam} reference PDFs." });

            byte[] content;
            await using (var ms = new MemoryStream((int)file.Length))
            {
                await file.CopyToAsync(ms);
                content = ms.ToArray();
            }

            // The browser's Content-Type and the file name are user-controlled, so check the
            // file's own signature instead.
            if (content.Length < PdfSignature.Length
                || !content.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
                return BadRequest(new { message = "That file is not a valid PDF." });

            var attachment = new ExamAttachment
            {
                ExamId = examId,
                FileName = CleanFileName(file.FileName),
                SizeBytes = content.Length,
                Content = content,
                UploadedAt = TimeHelper.GetPhilippineTime()
            };
            _minDAESDbContext.Set<ExamAttachment>().Add(attachment);
            await _minDAESDbContext.SaveChangesAsync();

            return Ok(new { id = attachment.Id, fileName = attachment.FileName, sizeBytes = attachment.SizeBytes });
        }

        // GET: the PDF itself (opened by the Builder's "View" link)
        [HttpGet("{attachmentId:guid}")]
        public async Task<IActionResult> Get(Guid examId, Guid attachmentId)
        {
            // Select only what is needed so nothing else is pulled into memory.
            var file = await _minDAESDbContext.Set<ExamAttachment>().AsNoTracking()
                .Where(a => a.Id == attachmentId && a.ExamId == examId)
                .Select(a => new { a.FileName, a.Content })
                .FirstOrDefaultAsync();
            if (file == null)
                return NotFound(new { message = "Attachment not found." });

            return PdfResponse.Inline(this, file.FileName, file.Content);
        }

        // DELETE: removes the file and clears every question link to it, in one transaction.
        [HttpDelete("{attachmentId:guid}")]
        public async Task<ActionResult> Delete(Guid examId, Guid attachmentId)
        {
            var exists = await _minDAESDbContext.Set<ExamAttachment>()
                .AnyAsync(a => a.Id == attachmentId && a.ExamId == examId);
            if (!exists)
                return NotFound(new { message = "Attachment not found." });

            await using var transaction = await _minDAESDbContext.Database.BeginTransactionAsync();
            try
            {
                var linked = await _minDAESDbContext.Set<Question>()
                    .Where(q => q.AttachmentId == attachmentId)
                    .ToListAsync();
                foreach (var q in linked) q.AttachmentId = null;

                // A stub is enough to delete by key; this avoids loading the PDF bytes.
                _minDAESDbContext.Set<ExamAttachment>().Remove(new ExamAttachment { Id = attachmentId });

                await _minDAESDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Attachment removed." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error removing attachment", error = ex.Message });
            }
        }

        private static string CleanFileName(string? raw)
        {
            var name = Path.GetFileName(raw ?? string.Empty);
            name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
            if (name.Length == 0) name = "document.pdf";
            if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) name += ".pdf";
            return name.Length > 255 ? name[..251] + ".pdf" : name;
        }
    }

    public static class PdfResponse
    {
        // Inline so browsers display it; nosniff and no-store keep it from being reinterpreted or cached.
        public static IActionResult Inline(ControllerBase controller, string fileName, byte[] content)
        {
            controller.Response.Headers["Content-Disposition"] =
                $"inline; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
            controller.Response.Headers["X-Content-Type-Options"] = "nosniff";
            controller.Response.Headers["Cache-Control"] = "private, no-store";
            return controller.File(content, "application/pdf");
        }
    }
}