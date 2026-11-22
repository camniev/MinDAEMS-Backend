// ===================================================================================
// 2) ACCOUNT SERVICE — one place that creates/updates applicant logins and queues their emails
//    (replaces EnsureApplicantAccountsAsync and RegisterApplicants in AuthController)
// ===================================================================================
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Helpers;
using MinDAES.Api.Utilities;

public record ApplicantAccountRequest(string Name, string Email, string Code, string ExamTitle);

public record AccountPlan(
    List<ApplicantAccountRequest> Created,         // new logins
    List<ApplicantAccountRequest> PasswordUpdated, // existing login, now using this exam's code
    List<string> Conflicts);                       // email already belongs to a NON-applicant account

public class ApplicantAccountService
{
    private readonly MinDAESDbContext _minDAESDbContext;
    private readonly EmailQueue _queue;
    private readonly IConfiguration _config;

    public ApplicantAccountService(MinDAESDbContext db, EmailQueue queue, IConfiguration config)
    {
        _minDAESDbContext = db;
        _queue = queue;
        _config = config;
    }

    public Task<Guid?> GetApplicantUserTypeIdAsync() =>
        _minDAESDbContext.UserTypes
            .Where(ut => ut.UserTypeDesc == "Applicant")
            .Select(ut => (Guid?)ut.UserTypeId)
            .SingleOrDefaultAsync();

    public string LoginUrl() =>
        $"{(_config["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/')}/#/login";

    public string HeaderImageUrl() => _config["Email:HeaderImageUrl"] ?? string.Empty;

    // Adds/changes tracked entities but does NOT save: the caller saves once, together with the
    // enrollments, so an applicant never ends up enrolled without a login (or the reverse).
    public async Task<AccountPlan> StageAccountsAsync(
        IEnumerable<ApplicantAccountRequest> applicants, Guid applicantUserTypeId)
    {
        var wanted = applicants
            .GroupBy(a => a.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var emails = wanted.Select(a => a.Email.Trim()).ToList();

        var existing = (await _minDAESDbContext.Users
                .Where(u => u.UserName != null && emails.Contains(u.UserName))
                .ToListAsync())
            .ToDictionary(u => u.UserName!, StringComparer.OrdinalIgnoreCase);

        var plan = new AccountPlan(new(), new(), new());
        var now = TimeHelper.GetPhilippineTime();

        foreach (var a in wanted)
        {
            var email = a.Email.Trim();
            var code = a.Code.Trim();

            if (existing.TryGetValue(email, out var user))
            {
                // Never take over a staff member's login because the emails match.
                if (user.UserTypeId != applicantUserTypeId)
                {
                    plan.Conflicts.Add(email);
                    continue;
                }

                // The applicant already has a login (from an earlier exam). It now uses THIS
                // exam's code, so the code in the new email is the one that works.
                user.PassWord = PasswordHasher.HashPassword(code);
                user.DatePasswordLastSet = now;
                plan.PasswordUpdated.Add(a with { Email = email, Code = code });
                continue;
            }

            var employee = new Employee
            {
                EmpId = Guid.NewGuid(),
                EmpName = a.Name.Trim(),
                Position = "Applicant",
                EmailAddress = email,
                IsActive = true
            };
            _minDAESDbContext.Employees.Add(employee);
            _minDAESDbContext.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                UserName = email,
                UserTypeId = applicantUserTypeId,
                IsActive = true,
                EmpId = employee.EmpId,
                Employee = employee,
                DatePasswordLastSet = now,
                MustChangePasswordOnFirstLogin = false,
                PassWord = PasswordHasher.HashPassword(code)
            });
            plan.Created.Add(a with { Email = email, Code = code });
        }

        return plan;
    }

    // Call only AFTER the transaction has committed, so nobody is emailed a login that doesn't exist.
    public async Task QueueEmailsAsync(AccountPlan plan)
    {
        var loginUrl = LoginUrl();
        var imageUrl = HeaderImageUrl();
        foreach (var a in plan.Created.Concat(plan.PasswordUpdated))
        {
            await _queue.EnqueueAsync(new EmailJob(
                a.Email,
                ApplicantEmailTemplate.Subject,
                ApplicantEmailTemplate.Credentials(a.Email, a.Code, loginUrl, imageUrl, isResend: false, examTitle: a.ExamTitle)));
        }
    }
}
