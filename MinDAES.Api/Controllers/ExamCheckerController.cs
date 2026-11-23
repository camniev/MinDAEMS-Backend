using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;
using System.Security.Claims;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = AppRoles.Assigners)]
    public class ExamCheckerController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAESDbContext;
        public ExamCheckerController(MinDAESDbContext minDAESDbContext)
        {
            _minDAESDbContext = minDAESDbContext;
        }

        public class AssignCheckersRequest
        {
            public List<Guid> UserIds { get; set; } = new();
        }

        private IQueryable<User> ActiveCheckers() =>
            _minDAESDbContext.Users.AsNoTracking().Where(u => u.IsActive && u.UserType != null && u.UserType.UserTypeDesc == AppRoles.Checker);

        [HttpGet("Checkers")]
        public async Task<ActionResult> GetAvailableCheckers()
        {
            var rows = await ActiveCheckers()
                .OrderBy(u => u.Employee!.EmpName)
                .Select(u => new 
                { 
                    userId = u.UserId, 
                    name = u.Employee != null ? 
                    u.Employee.EmpName : u.UserName, username = u.UserName,
                    position = u.Employee.Position
                })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpGet("Exam/{examId:guid}/Checkers")]
        public async Task<ActionResult> GetAssigned(Guid examId)
        {
            if (!await _minDAESDbContext.Exams.AnyAsync(e => e.ExamId == examId))
                return NotFound(new { message = "Exam schedule not found." });

            var rows = await _minDAESDbContext.Set<ExamChecker>().AsNoTracking()
                .Where(c => c.ExamId == examId)
                .Select(c => new { userId = c.UserId })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpPut("Exam/{examId:guid}/Checkers")]
        public async Task<ActionResult> ReplaceAssigned(Guid examId, [FromBody] AssignCheckersRequest? body)
        {
            if (body == null)
                return BadRequest(new { message = "Request body is required." });
            if (!await _minDAESDbContext.Exams.AnyAsync(e => e.ExamId == examId))
                return NotFound(new { message = "Exam schedule not found." });

            var wanted = body.UserIds.Distinct().ToList();

            // Only real, active Checker accounts can be assigned.
            var valid = await ActiveCheckers().Where(u => wanted.Contains(u.UserId)).Select(u => u.UserId).ToListAsync();
            if (valid.Count != wanted.Count)
                return BadRequest(new { message = "Only active Checker accounts can be assigned." });

            Guid? me = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

            await using var transaction = await _minDAESDbContext.Database.BeginTransactionAsync();
            try
            {
                var current = await _minDAESDbContext.Set<ExamChecker>().Where(c => c.ExamId == examId).ToListAsync();
                _minDAESDbContext.Set<ExamChecker>().RemoveRange(current.Where(c => !wanted.Contains(c.UserId)));

                var already = current.Select(c => c.UserId).ToHashSet();
                foreach (var userId in wanted.Where(u => !already.Contains(u)))
                {
                    _minDAESDbContext.Set<ExamChecker>().Add(new ExamChecker
                    {
                        ExamId = examId,
                        UserId = userId,
                        AssignedAt = TimeHelper.GetPhilippineTime(),
                        AssignedByUserId = me
                    });
                }

                await _minDAESDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { message = "Checkers updated.", count = wanted.Count });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Error saving checker assignments", error = ex.Message });
            }
        }
    }
}
