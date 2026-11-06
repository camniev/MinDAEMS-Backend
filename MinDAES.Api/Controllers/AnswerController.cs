
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
    public class AnswerController : ControllerBase
    {
        //retrieve the injected MinDAESDbContext dependency from the constructor and assign it to a readonly object
        private readonly MinDAESDbContext _minDAEsDbContext;

        //streamlining the constructor using the expression bodied function
        //this approach consolidates all components into a single concise line
        public AnswerController(MinDAESDbContext minDAEsDbContext) => _minDAEsDbContext = minDAEsDbContext;
    }
}
