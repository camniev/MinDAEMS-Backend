using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinDAES.Api.Data;
using MinDAES.Api.Domain.Entities;

namespace MinDAES.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        private readonly MinDAESDbContext _minDAEsDbContext;
        public EmployeeController(MinDAESDbContext minDAEsDbContext)
        {
            _minDAEsDbContext = minDAEsDbContext;
        }

        //get all employees
        [HttpGet]
        public ActionResult<IEnumerable<Employee>> GellAllEmployees()
        {
            return _minDAEsDbContext.Employees;
        }

        //get an employee by ID
        [HttpGet("{emp_id}")]
        public async Task<ActionResult<Employee?>> GetEmployeeByID(Guid emp_id)
        {
            return await _minDAEsDbContext.Employees.Where(x => x.EmpId == emp_id).FirstOrDefaultAsync();
        }

        //post an employee
        [HttpPost]
        public async Task<ActionResult> PostEmployee(Employee employee)
        {
            await _minDAEsDbContext.Employees.AddAsync(employee);
            await _minDAEsDbContext.SaveChangesAsync();
            return CreatedAtAction(nameof(GetEmployeeByID), new { emp_id = employee.EmpId }, employee);
        }

        //edit an employee
        [HttpPut]
        public async Task<ActionResult> UpdateEmployee(Employee employee)
        {
            _minDAEsDbContext.Employees.Update(employee);
            await _minDAEsDbContext.SaveChangesAsync();
            return Ok();
        }

        //soft delete an employee
        [HttpPut("{emp_id}")]
        public async Task<ActionResult<Employee>> DeleteEmployeeByID(Guid emp_id)
        {
            var employee = await _minDAEsDbContext.Employees.FindAsync(emp_id);
            if (employee is null)
            {
                return NotFound();
            }
            employee.IsActive = false;
            await _minDAEsDbContext.SaveChangesAsync();
            return Ok();
        }
    }
}
