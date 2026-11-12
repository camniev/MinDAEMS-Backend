using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MinDAES.Api.Domain.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using MinDAES.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using MinDAES.Api.Helpers;
using MinDAES.Api.Data;
using MinDAES.Api.Utilities;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAEsDbContext;
        private readonly IConfiguration _configuration;
        public AuthController(MinDAESDbContext minDAEsDbContext, IConfiguration configuration)
        {
            _minDAEsDbContext = minDAEsDbContext;
            _configuration = configuration;
        }

        [HttpPost("Register")]
        public async Task<ActionResult> Register([FromBody] UserRegistrationModel model)
        {
            if (_minDAEsDbContext.Users.Any(u => u.UserName == model.UserName))
            {
                return BadRequest("Username already exists.");
            }

            var employee = new Employee {
                EmpId = Guid.NewGuid(),
                EmpName = model.Employee.EmpName,
                Position = model.Employee.Position,
                EmailAddress = model.Employee.EmailAddress,
                IsActive = true
            };

            var user = new User {
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

        [HttpPost("RegisterApplicants")]
        public async Task<ActionResult> RegisterApplicants([FromBody] RegisterApplicantsRequest model)
        {
            if (model?.Applicants == null || model.Applicants.Count == 0)
                return BadRequest("At least one applicant is required.");

            var invalid = model.Applicants
                .Where(a => string.IsNullOrWhiteSpace(a.Name)
                         || string.IsNullOrWhiteSpace(a.Email)
                         || string.IsNullOrWhiteSpace(a.ApplicantCode))
                .ToList();
            if (invalid.Count > 0)
                return BadRequest("Every applicant needs a name, an email and an applicant code.");

            //if (!Guid.TryParse(_configuration["Auth:ApplicantUserTypeId"], out var applicantUserTypeId))
            //    return StatusCode(500, "Applicant user type is not configured (Auth:ApplicantUserTypeId).");

            var applicantUserTypeId = await _minDAEsDbContext.UserTypes
                .Where(ut => ut.UserTypeDesc == "Applicant")
                .Select(ut => (Guid?)ut.UserTypeId)
                .SingleOrDefaultAsync();

            if (applicantUserTypeId == null)
                return StatusCode(500, "The 'Applicant' user type was not found.");

            try
            {
                var (created, existing) = await EnsureApplicantAccountsAsync(model.Applicants, applicantUserTypeId.Value);
                return Ok(new
                {
                    created,
                    alreadyExisted = existing,
                    message = $"{created} account(s) created, {existing} already existed."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating applicant accounts", error = ex.Message });
            }
        }

        // Kept separate so EnrollApplicants can call it too. That would make "enroll + create
        // accounts" a single transaction on the server, which is the more robust long-term setup.
        private async Task<(int created, int existing)> EnsureApplicantAccountsAsync(
            IEnumerable<ApplicantAccountDto> applicants, Guid applicantUserTypeId)
        {
            // One entry per email, even if the request lists someone twice.
            var wanted = applicants
                .GroupBy(a => a.Email!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var emails = wanted.Select(a => a.Email!.Trim()).ToList();
            var taken = (await _minDAEsDbContext.Users
                    .Where(u => emails.Contains(u.UserName))
                    .Select(u => u.UserName)
                    .ToListAsync())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var created = 0;
            foreach (var a in wanted)
            {
                var email = a.Email!.Trim();
                if (taken.Contains(email)) continue;

                var employee = new Employee
                {
                    EmpId = Guid.NewGuid(),
                    EmpName = a.Name!.Trim(),
                    Position = "Applicant",
                    EmailAddress = email,
                    IsActive = true
                };

                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    UserName = email,
                    UserTypeId = applicantUserTypeId,
                    IsActive = true,
                    EmpId = employee.EmpId,
                    Employee = employee,
                    DatePasswordLastSet = TimeHelper.GetPhilippineTime(),
                    // The applicant code IS the credential given to the applicant, so there is
                    // nothing to force them to change. Importantly, this also means the password
                    // is hashed: Register stores it as plain text when this flag is true.
                    MustChangePasswordOnFirstLogin = false,
                    PassWord = PasswordHasher.HashPassword(a.ApplicantCode!.Trim())
                };

                _minDAEsDbContext.Employees.Add(employee);
                _minDAEsDbContext.Users.Add(user);
                created++;
            }

            // One SaveChanges = one transaction: either every new account is saved or none is.
            if (created > 0) await _minDAEsDbContext.SaveChangesAsync();
            return (created, wanted.Count - created);
        }

        [HttpPut("UpdateUser")]
        public async Task<ActionResult> UpdateUser([FromBody] UserRegistrationModel data)
        {
            var user = await _minDAEsDbContext.Users
            .Include(u => u.Employee)
            .FirstOrDefaultAsync(u => u.UserId == data.UserId);
            
            if (user == null) {
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
            return Ok(new { message = "User updated successfully."});
        }

        [HttpPost("Login")]
        public async Task<ActionResult> Login(UserLoginModel model)
        {
            var user = await _minDAEsDbContext.Users
                .Include(u => u.UserType)
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.UserName == model.UserName);

            if (user == null)
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

            return Ok( new { accessToken });
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

        [Authorize]
        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _minDAEsDbContext.Users.FindAsync(new Guid(userId));

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiryTime = null;
                await _minDAEsDbContext.SaveChangesAsync();
            }

            Response.Cookies.Delete("refreshToken");
            return Ok();
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                Path = "/",
                SameSite = isProduction ? SameSiteMode.None : SameSiteMode.Lax, // Use None for cross-site cookies in production, Lax otherwise
                Secure = isProduction // Secure should be true in production with HTTPS, false in development
            };

            Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
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
            var employeeEmail = user.Employee?.EmailAddress ?? string.Empty;
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

            var user = await _minDAEsDbContext.Users
                .Include(u => u.UserType)  // Ensure UserType is included
                .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.RefreshTokenExpiryTime > DateTime.UtcNow);

            if (user == null)
            {
                return Unauthorized("Invalid or expired refresh token.");
            }

            var accessToken = GenerateJwtToken(user);
            return Ok(new { accessToken });
        }
    }
}
