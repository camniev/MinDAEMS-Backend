using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Helpers;

namespace MinDAES.Api.Controllers
{
    // What the proctor's screen receives, live (SignalR "ReceiveNotification") and from history.
    public record ExamIncidentAlert(
        string Kind,            // always "ExamIncident" — lets one hub method carry other notification kinds later
        Guid IncidentId,
        Guid ExamId,
        string ExamTitle,
        Guid ExamEnrollmentId,
        string ApplicantCode,
        string ApplicantName,   // proctors may see the name; printouts for checkers deliberately don't
        string Type,
        string Severity,        // "warning" (left) or "info" (came back)
        int? AwaySeconds,
        DateTime OccurredAt)
    {
        public static readonly string[] AllowedTypes =
            { "FullscreenExited", "FullscreenRestored", "FocusLost", "FocusRestored" };

        public static string SeverityOf(string type) =>
            type is "FullscreenExited" or "FocusLost" ? "warning" : "info";
    }

    // ===================================================================================
    // 2) PROCTOR ENDPOINT:  GET api/Monitoring/Incidents?examId=&hours=12&limit=100
    //    History for the alerts panel (first load, and after the live connection comes back).
    // ===================================================================================
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MonitoringController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAESDbContext;
        public MonitoringController(MinDAESDbContext db) => _minDAESDbContext = db;

        [HttpGet("Incidents")]
        public async Task<ActionResult> GetIncidents([FromQuery] Guid? examId, [FromQuery] int hours = 12, [FromQuery] int limit = 100)
        {
            // Staff only. TODO: swap for [Authorize(Roles = "...")] with your HR roles.
            if (User.IsInRole("Applicant")) return Forbid();

            var since = TimeHelper.GetPhilippineTime().AddHours(-Math.Clamp(hours, 1, 72));
            var query = _minDAESDbContext.Set<ExamIncident>().AsNoTracking().Where(i => i.OccurredAt >= since);
            if (examId != null)
                query = query.Where(i => i.ExamEnrollment!.ExamId == examId);

            var rows = await query
                .OrderByDescending(i => i.OccurredAt)
                .Take(Math.Clamp(limit, 1, 500))
                .Select(i => new
                {
                    i.Id,
                    i.ExamEnrollment!.ExamId,
                    i.ExamEnrollment.Exam!.ExamTitle,
                    i.ExamEnrollmentId,
                    i.ExamEnrollment.ApplicantCode,
                    ApplicantName = i.ExamEnrollment.Applicant!.FullName,
                    i.Type,
                    i.AwaySeconds,
                    i.OccurredAt
                })
                .ToListAsync();

            return Ok(rows.Select(r => new ExamIncidentAlert(
                "ExamIncident", r.Id, r.ExamId, r.ExamTitle, r.ExamEnrollmentId, r.ApplicantCode,
                r.ApplicantName, r.Type, ExamIncidentAlert.SeverityOf(r.Type), r.AwaySeconds, r.OccurredAt)));
        }
    }
}