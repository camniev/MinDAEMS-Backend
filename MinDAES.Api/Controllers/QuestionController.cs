using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuestionController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAESDbContext;
        public QuestionController(MinDAESDbContext minDAESDbContext)
        {
            _minDAESDbContext = minDAESDbContext;
        }

        [HttpPost("{examId:guid}/SaveQuestions")]
        public async Task<ActionResult> SaveExamQuestions(Guid examId, [FromBody] SaveExamQuestionsDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request body is required." });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var examExists = await _minDAESDbContext.Exams.AnyAsync(e => e.ExamId == examId);
            if (!examExists)
                return NotFound(new { message = "Exam schedule not found." });

            // Validate question types up front so a typo gives a clean 400 instead
            // of blowing up mid-transaction.
            var typeErrors = new List<string>();
            var parsedTypes = new QuestionType[data.Questions.Count];
            for (var i = 0; i < data.Questions.Count; i++)
            {
                if (!TryParseQuestionType(data.Questions[i].Type, out parsedTypes[i]))
                    typeErrors.Add($"Question {i + 1}: unknown question type \"{data.Questions[i].Type}\".");
            }
            if (typeErrors.Count > 0)
                return BadRequest(new { message = "Invalid question data.", errors = typeErrors });

            await using var transaction = await _minDAESDbContext.Database.BeginTransactionAsync();
            try
            {
                // Load existing questions WITH their options so EF's change tracker
                // cascades the option deletes too (Options.QuestionId is a required
                // FK, so EF Core cascades by convention — but only for tracked
                // entities, hence the Include).
                var existingQuestions = await _minDAESDbContext.Questions
                    .Include(q => q.Options)
                    .Where(q => q.ExamId == examId)
                    .ToListAsync();
                _minDAESDbContext.Questions.RemoveRange(existingQuestions);

                var newQuestions = data.Questions.Select((q, index) => new Question
                {
                    QuestionId = Guid.NewGuid(),
                    ExamId = examId,
                    Type = parsedTypes[index],
                    Text = q.Text,
                    Points = q.Points,
                    IsRequired = q.Required,
                    OrderIndex = index,
                    Options = q.Options.Select((o, oIndex) => new QuestionOption
                    {
                        QuestionOptionId = Guid.NewGuid(),
                        Text = o.Text,
                        IsCorrect = o.IsCorrect,
                        OrderIndex = oIndex,
                    }).ToList(),
                }).ToList();

                await _minDAESDbContext.Questions.AddRangeAsync(newQuestions);
                await _minDAESDbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Exam questions saved successfully." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "Error saving exam questions",
                    error = ex.Message
                });
            }
        }

        private static bool TryParseQuestionType(string type, out QuestionType result)
        {
            switch (type)
            {
                case "mcq": result = QuestionType.MultipleChoice; return true;
                case "checkbox": result = QuestionType.Checkbox; return true;
                case "truefalse": result = QuestionType.TrueFalse; return true;
                case "essay": result = QuestionType.Essay; return true;
                default: result = default; return false;
            }
        }
    }
}
