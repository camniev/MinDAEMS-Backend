using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExamController : ControllerBase
    {
        private readonly MinDAESDbContext _mindaEsDbContext;
        public ExamController(MinDAESDbContext mindaEsDbContext)
        {
            _mindaEsDbContext = mindaEsDbContext;
        }

        // add new exam schedules
        [HttpPost("AddNewExamSchedules")]
        public async Task<ActionResult> AddNewExamSchedules([FromBody] ExamDto data)
        {
            if (data == null)
                return BadRequest(new { message = "Request header is required."});

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await using var transaction = await _mindaEsDbContext.Database.BeginTransactionAsync();

            try
            {
                var exam = new Exam
                {
                    ExamId = Guid.NewGuid(),
                    ExamTitle = data.ExamTitle,
                    ExamCode = data.ExamCode,
                    ExamDate = data.ExamDate,
                    StartTime = data.StartTime,
                    DurationMinutes = data.DurationMinutes,
                    PassingScore = data.PassingScore,
                    Instructions = data.Instructions,
                    AccessType = data.AccessType == "Private"
                        ? ExamAccessType.Private
                        : ExamAccessType.Public,
                    CreatedByUserId = data.UserId,
                    CreatedAt = TimeHelper.GetPhilippineTime()
                };

                _mindaEsDbContext.Exams.Add(exam);
                await _mindaEsDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "New exam schedule added successfully."});
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new
                {
                    message = "Error adding new exam schedule",
                    error = ex.Message
                });
            }
        }
    }
}