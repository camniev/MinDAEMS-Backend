using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.Enums;
using MinDAES.Api.Helpers;
using MinDAES.Api.Utilities;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAEsDbContext;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailSender _emailSender;
        private readonly EmailQueue _emailQueue;
        private readonly ApplicantAccountService _accounts;
        public AuthController(MinDAESDbContext minDAEsDbContext, IConfiguration configuration, IWebHostEnvironment webHostEnvironment, IEmailSender emailSender, EmailQueue emailQueue, ApplicantAccountService accounts)
        {
            _minDAEsDbContext = minDAEsDbContext;
            _configuration = configuration;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
            _emailQueue = emailQueue;
            _accounts = accounts;
        }

        // THE root cause of "Unknown" names and empty emails after a refresh: Login loaded the
        // user WITH Employee, but refresh-token loaded it WITHOUT, so every refreshed token had
        // given_name = "Unknown" and email = "" � and the applicant endpoints read the email
        // claim, so they answered 403 "no email address" from the first refresh (about 14 minutes
        // in) onward. Both now go through this one method so they can't drift apart again.
        private Task<User?> LoadUserForTokenAsync(Expression<Func<User, bool>> predicate) =>
            _minDAEsDbContext.Users
                .Include(u => u.UserType)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(predicate);

        // TODO (security, see notes): Register, RegisterApplicants and UpdateUser are open to
        // anyone right now. Protect them with [Authorize(Roles = "<your HR/admin role>")].
        [HttpPost("Register")]
        public async Task<ActionResult> Register([FromBody] UserRegistrationModel model)
        {
            if (_minDAEsDbContext.Users.Any(u => u.UserName == model.UserName))
            {
                return BadRequest("Username already exists.");
            }

            var employee = new Employee
            {
                EmpId = Guid.NewGuid(),
                EmpName = model.Employee.EmpName,
                Position = model.Employee.Position,
                EmailAddress = model.Employee.EmailAddress,
                IsActive = true
            };

            var user = new User
            {
                UserId = Guid.NewGuid(),
                UserName = model.UserName,
                UserTypeId = model.UserType.UtId,
                IsActive = true,
                EmpId = employee.EmpId,
                Employee = employee,
                DatePasswordLastSet = TimeHelper.GetPhilippineTime(),
                MustChangePasswordOnFirstLogin = model.MustChangePasswordOnFirstLogin
            };

            if (model.MustChangePasswordOnFirstLogin == true)
            {
                user.PassWord = model.PassWord;
            }
            else
            {
                user.PassWord = PasswordHasher.HashPassword(model.PassWord);
            }

            _minDAEsDbContext.Employees.Add(employee);
            _minDAEsDbContext.Users.Add(user);
            await _minDAEsDbContext.SaveChangesAsync();

            return Ok(new { message = "User added successfully.", UserId = user.UserId });
        }

        [HttpPut("UpdateUser")]
        public async Task<ActionResult> UpdateUser([FromBody] UserRegistrationModel data)
        {
            var user = await _minDAEsDbContext.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == data.UserId);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            user.UserName = data.UserName;
            user.UserTypeId = data.UserType.UtId;

            if (!string.IsNullOrEmpty(data.PassWord))
            {
                if (data.MustChangePasswordOnFirstLogin == true)
                {
                    user.PassWord = data.PassWord;
                }
                else
                {
                    user.PassWord = PasswordHasher.HashPassword(data.PassWord);
                }
                user.DatePasswordLastSet = TimeHelper.GetPhilippineTime();
            }

            user.Employee.EmpName = data.Employee.EmpName;
            user.Employee.Position = data.Employee.Position;
            user.Employee.EmailAddress = data.Employee.EmailAddress;
            user.MustChangePasswordOnFirstLogin = data.MustChangePasswordOnFirstLogin;

            await _minDAEsDbContext.SaveChangesAsync();
            return Ok(new { message = "User updated successfully." });
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login(UserLoginModel model)
        {
            var user = await LoadUserForTokenAsync(u => u.UserName == model.UserName);

            // Same message for "no such user" and "deactivated", so it doesn't reveal which exist.
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Incorrect username or password" });
            }

            if (user.MustChangePasswordOnFirstLogin == true)
            {
                if (user.PassWord != model.PassWord)
                {
                    return Unauthorized(new { message = "Incorrect username or password" });
                }
            }
            else
            {
                if (!PasswordHasher.VerifyPassword(model.PassWord, user.PassWord))
                {
                    return Unauthorized(new { message = "Incorrect username or password" });
                }
            }

            var accessToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _minDAEsDbContext.SaveChangesAsync();

            SetRefreshTokenCookie(refreshToken);

            return Ok(new { accessToken });
        }

        [HttpPut("UpdatePasswordOnFirstLogin")]
        public async Task<IActionResult> UpdatePasswordOnFirstLogin([FromBody] PasswordUpdateModel model)
        {
            var user = await _minDAEsDbContext.Users.FirstOrDefaultAsync(u => u.UserId == model.UserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            if (user.PassWord != model.OldPassword)
            {
                return Unauthorized(new { message = $@"Incorrect default password" });
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                return BadRequest(new { message = "New password and confirmation do not match" });
            }

            user.PassWord = PasswordHasher.HashPassword(model.NewPassword);
            user.DatePasswordLastSet = TimeHelper.GetPhilippineTime();
            user.MustChangePasswordOnFirstLogin = false;

            await _minDAEsDbContext.SaveChangesAsync();

            return Ok(new { message = "Password updated successfully." });
        }

        // Anonymous on purpose, and keyed on the refresh cookie rather than the access token.
        // With [Authorize], logging out after the 15-minute access token had expired returned 401:
        // the browser looked logged out, but the refresh cookie and its database row stayed
        // valid, so the next page load silently signed the person back in.
        [AllowAnonymous]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var user = await _minDAEsDbContext.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken);
                if (user != null)
                {
                    user.RefreshToken = null;
                    user.RefreshTokenExpiryTime = null;
                    await _minDAEsDbContext.SaveChangesAsync();
                }
            }

            // Must use the same Path / SameSite / Secure as when it was set, or the browser
            // keeps the cookie.
            Response.Cookies.Delete("refreshToken", RefreshCookieOptions(null));
            return Ok();
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append("refreshToken", refreshToken, RefreshCookieOptions(DateTimeOffset.UtcNow.AddDays(7)));
        }

        private CookieOptions RefreshCookieOptions(DateTimeOffset? expires)
        {
            // Secure when the app runs in Production OR the request itself came in over HTTPS.
            // The old check looked only at the ASPNETCORE_ENVIRONMENT value "Production": on a
            // server named "Staging" (or anything else) it fell back to Lax + non-Secure, and a
            // front end on a different site then never sends the refresh cookie, so every page
            // reload logs the person out.
            var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
            var secure = isProduction || Request.IsHttps;

            return new CookieOptions
            {
                HttpOnly = true,
                Expires = expires,
                Path = "/",
                SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax, // None needs Secure
                Secure = secure
            };
        }

        private string GenerateJwtToken(User user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(
                    nameof(user),
                    "User cannot be null when generating a JWT token."
                );
            }

            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!);

            var employeeName = user.Employee?.EmpName ?? "Unknown";
            // Applicant accounts use their email as the username, so fall back to that rather
            // than an empty claim if the Employee row is ever missing.
            var employeeEmail = user.Employee?.EmailAddress;
            if (string.IsNullOrWhiteSpace(employeeEmail) && user.UserName?.Contains('@') == true)
                employeeEmail = user.UserName;
            employeeEmail ??= string.Empty;
            var userTypeDescription = user.UserType?.UserTypeDesc ?? string.Empty;
            var mustChangePasswordOnFirstLogin = user.MustChangePasswordOnFirstLogin;

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.Role, userTypeDescription),
                    new Claim(ClaimTypes.GivenName, employeeName),
                    new Claim(ClaimTypes.Email, employeeEmail),
                    new Claim(
                        "mustChangePasswordOnFirstLogin",
                        mustChangePasswordOnFirstLogin.ToString().ToLower()
                    )
                ]),
                Expires = DateTime.UtcNow.AddMinutes(15),
                Audience = _configuration["Jwt:Audience"],
                Issuer = _configuration["Jwt:Issuer"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
            {
                return Unauthorized("No refresh token provided.");
            }

            var user = await LoadUserForTokenAsync(
                u => u.RefreshToken == refreshToken && u.RefreshTokenExpiryTime > DateTime.UtcNow);

            // A deactivated account must not keep getting new access tokens.
            if (user == null || !user.IsActive)
            {
                return Unauthorized("Invalid or expired refresh token.");
            }

            var accessToken = GenerateJwtToken(user);
            return Ok(new { accessToken });
        }

        // TEST EMAIL
        [HttpPost("send-test-email")]
        public async Task<IActionResult> SendTestEmail()
        {
            var email = "cameron.nieve@minda.gov.ph";
            try
            {
                await _emailSender.SendAsync(email, "Test Email from MinDAEMS",
                    GenerateEmailBodyForPasswordUpdate(email, "DMO_I_KMD_0001"));
                return Ok(new { message = "Test email sent successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error sending test email", error = ex.Message });
            }
        }

        //HELPERS
        private async Task<Guid?> GetApplicantUserTypeIdAsync() =>
            await _minDAEsDbContext.UserTypes
                .Where(ut => ut.UserTypeDesc == "Applicant")
                .Select(ut => (Guid?)ut.UserTypeId)
                .SingleOrDefaultAsync();

        private string LoginUrl() =>
            $"{(_configuration["App:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/')}/#/login";

        private string HeaderImageUrl() => _configuration["Email:HeaderImageUrl"] ?? string.Empty;

        // POST api/Auth/ResendApplicantInvite/{examEnrollmentId}
        // The Send button on the Applicants screen. Makes sure the account exists, makes the applicant
        // code from THIS enrollment the account's working password, and emails it. The result is
        // returned to the caller (unlike the queued batch) because a person is waiting on it.
        [HttpPost("ResendApplicantInvite/{examEnrollmentId:guid}")]
        public async Task<ActionResult> ResendApplicantInvite(Guid examEnrollmentId)
        {
            var en = await _minDAEsDbContext.Set<ExamEnrollment>()
                .AsNoTracking()
                .Include(e => e.Applicant)
                .Include(e => e.Exam)
                .FirstOrDefaultAsync(e => e.ExamEnrollmentId == examEnrollmentId);
        
            if (en?.Applicant == null || en.Exam == null)
                return NotFound(new { message = "Enrollment not found." });
        
            // Logins are removed when an exam closes, so there is nothing to send for a closed one.
            if (en.Exam.IsArchived || (en.Exam.Status != ExamStatus.Upcoming && en.Exam.Status != ExamStatus.Ongoing))
                return Conflict(new { message = "Login details can only be sent for an upcoming or ongoing exam." });
        
            var email = en.Applicant.Email?.Trim();
            var code = en.ApplicantCode?.Trim();
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return Conflict(new { message = "This applicant has no email address or applicant code." });
        
            var applicantTypeId = await _accounts.GetApplicantUserTypeIdAsync();
            if (applicantTypeId == null)
                return StatusCode(500, new { message = "The 'Applicant' user type was not found." });
        
            try
            {
                // Creates the login if it's missing, or makes THIS enrollment's code its working password.
                var plan = await _accounts.StageAccountsAsync(
                    new[] { new ApplicantAccountRequest(en.Applicant.FullName, email, code, en.Exam.ExamTitle) },
                    applicantTypeId.Value);
        
                if (plan.Conflicts.Count > 0)
                    return Conflict(new { message = "That email already belongs to a non-applicant account, so no login details were sent." });
        
                await _minDAEsDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error preparing the applicant's account", error = ex.Message });
            }
        
            try
            {
                // Sent directly (not queued): a person clicked the button and is waiting for the result.
                await _emailSender.SendAsync(
                    email,
                    ApplicantEmailTemplate.Subject,
                    ApplicantEmailTemplate.Credentials(email, code, _accounts.LoginUrl(), _accounts.HeaderImageUrl(),
                        isResend: true, examTitle: en.Exam.ExamTitle));
                return Ok(new { message = $"Login details sent to {email}." });
            }
            catch (Exception)
            {
                return StatusCode(502, new { message = "The account is ready, but the email could not be sent. Please try again." });
            }
        }

        //HTML TEMPLATES
        private string GenerateEmailBodyForPasswordUpdate(string username, string password)
        {
            var imageUrl = "https://drive.google.com/uc?export=view&id=1NXEAmNNp21nhrbTn8zv33oTZ4W8okqeS";

            return $@"
            <html>
            <head>
            <style>
            body {{
                background-color: #e7e8f0;
            }}
            .email-container {{
                font-family: Arial, sans-serif;
                max-width: 600px;
                margin: auto;
                padding: 20px;
                background-color: #f9f9f9;
            }}
            .header img {{
                width: 100%;
            }}
            .content {{
                padding: 20px;
            }}
            .content h3 {{
                text-align: center;
            }}
            .venue-request-content {{
                display: flex;
            }}
            .venue-request-content p:nth-child(1) {{
                width: 25%;
                font-weight: bold;
                text-align: right;
                margin-right: 8px;
                color: #7d7d7d;
            }}
            .vr-column-header {{ 
                width: 25%;
                font-weight: bold;
                text-align: right;
                margin-right: 8px;
                color: #7d7d7d;
            }}
            .view-btn-class {{
                margin-top: 32px;
                text-align: center;
            }}
            .view-btn-class a {{
                border-top: 3px solid #212631;
                border-left: 3px solid #212631;
                border-right: 3px solid #212631;
                border-bottom: 6px solid #212631;
                padding: 16px 56px;
                color: #212631;
                background-color: #83c1ff;
                border-radius: 10px;
                font-weight: 600;
                text-decoration: none;
            }}
            .view-btn-class a:hover {{
                background-color: white;
                transition: .3s;
            }}
            .footer {{
                text-align: center;
                font-size: 12px;
                color: #777;
                margin-top: 32px;
            }}
            </style>
            </head>
            <body>
                <table width='100%' cellspacing='0' cellpadding='0' border='0' bgcolor='#e7e8f0'>
                    <tr>
                        <td align='center'>
                            <table width='600' cellspacing='0' cellpadding='0' border='0' class='email-container' style='background-color: #f9f9f9; padding: 20px; margin-top: 20px; margin-bottom: 20px;'>
                                <tr>
                                    <td>
                                        <div>
                                            <div class='header'>
                                                <img src='{imageUrl}' alt='MinDAEMS Email Header' />
                                            </div>
                                            <div class='content'>
                                                <h3 style='padding: 0; margin: 0;'>Welcome! Below are your account details to log in to the <span style='font-weight: bold; color: #8950FC;'>MinDA Examination System</span>.</h3>
                                                <h4 style='color: #7d7d7d;padding: 8px;margin: 0;text-align: center;'>Please ensure to change your password upon first login for security purposes.</h4>
                                                <table style='width: 100%; padding-top: 12px;'>
                                                    <tr>
                                                        <td class='vr-column-header'><p>Username: </p></td>
                                                        <td><p style='font-weight: bold; color: #8950FC;'>{username}</p></td>
                                                    </tr>
                                                    <tr>
                                                        <td class='vr-column-header'><p>Password/Account Code: </p></td>
                                                        <td><p style='font-weight: bold; color: #8950FC;'>{password}</p></td>
                                                    </tr>
                                                </table>
                                                <div class='view-btn-class'>
                                                    <a href='http://localhost:3000/#/'>Open the App</a>
                                                </div>
                                            </div>
                                            <div class='footer'>
                                                <p>This is an automated message. Please do not reply.</p>
                                            </div>
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
        }
    }
}