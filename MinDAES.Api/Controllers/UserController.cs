
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Domain.DTOs;
using MinDAES.Api.Domain.Entities;
using MinDAES.Api.Domain.DTOs;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinDAES.Api.Data;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        //retrieve the injected MinDAESDbContext dependency from the constructor and assign it to a readonly object
        private readonly MinDAESDbContext _minDAEsDbContext;

        //streamlining the constructor using the expression bodied function
        //this approach consolidates all components into a single concise line
        public UserController(MinDAESDbContext minDAEsDbContext) => _minDAEsDbContext = minDAEsDbContext;

        //create HttpGet method to retrieve all users from the database
        [HttpGet("GetAllUsers")]
        public ActionResult<IEnumerable<User>> GetAllUsers()
        {
            //return _minDAEsDbContext.Users;
            var users = _minDAEsDbContext.Users
                .Include(u => u.UserType)
                .Include(u => u.Employee)
                .Where(u => u.UserType != null)
                .Select(u => new
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    IsActive = u.IsActive,
                    UserType = new
                    {
                        utId = u.UserType.UserTypeId,
                        utDesc = u.UserType.UserTypeDesc,
                    },
                    Employee = new
                    {
                        EmpId = u.Employee.EmpId,
                        EmpName = u.Employee.EmpName,
                        Position = u.Employee.Position,
                        EmailAddress = u.Employee.EmailAddress,
                        IsActive = u.Employee.IsActive
                    }
                })
                .ToList();
            return Ok(users);
        }

        //fetch a USER (joined to USER TYPE) by its unique ID
        [HttpGet("GetUserById/{user_id}")]
        public async Task<ActionResult> GetUserById(Guid user_id)
        {
            //return await _minDAEsDbContext.Users.Where(x => x.UserId == user_id).SingleOrDefaultAsync();
            var user = await _minDAEsDbContext.Users
                .Include(u => u.UserType)
                .Include(u => u.Employee)
                .Where(u => u.UserId == user_id)
                .Select(u => new
                {
                    UserId = u.UserId,
                    UserName = u.UserName,
                    IsActive = u.IsActive,
                    DatePasswordLastSet = u.DatePasswordLastSet,
                    UserType = new
                    {
                        utId = u.UserType.UserTypeId,
                        utDesc = u.UserType.UserTypeDesc,
                    },
                    Employee = new
                    {
                        EmpId = u.Employee.EmpId,
                        EmpName = u.Employee.EmpName,
                        Position = u.Employee.Position,
                        EmailAddress = u.Employee.EmailAddress,
                        IsActive = u.Employee.IsActive
                    }
                })
                .SingleOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            return Ok(user);
        }

        [HttpGet("GetActiveUserTypes")]
        public async Task<ActionResult<IEnumerable<UserType?>>> GetActiveUserTypes()
        {
            var user_types = await _minDAEsDbContext.UserTypes.Where(ut => ut.IsActive == true).ToListAsync();
            return Ok(user_types);
        }

        //create HttpPost method to create a new user, accepting a user object as a parameter
        [HttpPost]
        public async Task<ActionResult> CreateUsers(User user)
        {
            await _minDAEsDbContext.Users.AddAsync(user);
            await _minDAEsDbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUserById), new { user_id = user.UserId }, user);
        }

        // implement HttpPut method to update an existing user
        [HttpPut]
        public async Task<ActionResult> Update(User user)
        {
            _minDAEsDbContext.Users.Update(user);
            await _minDAEsDbContext.SaveChangesAsync();
            return Ok();
        }

        // update user status
        [HttpPut("UpdateUserStatus")]
        public async Task<ActionResult> UpdateUserStatus([FromBody] UserStatusDto userDto)
        {
            var user = await _minDAEsDbContext.Users.FirstOrDefaultAsync(u => u.UserId == userDto.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }
            user.IsActive = userDto.IsActive;
            await _minDAEsDbContext.SaveChangesAsync();
            return Ok(new { message = "User status updated successfully." });
        }

        // soft delete/deactivate a user
        [HttpPut("{user_id}")]
        public async Task<ActionResult> DeleteUserById(Guid user_id)
        {
            //soft delete
            var user = await _minDAEsDbContext.Users.FindAsync(user_id);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = false;
            await _minDAEsDbContext.SaveChangesAsync();
            return Ok();
        }
    }
}
