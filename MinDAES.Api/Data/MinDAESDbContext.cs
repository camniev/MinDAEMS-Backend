using Microsoft.EntityFrameworkCore;

namespace MinDAES.Api.Data
{
    public class MinDAESDbContext : DbContext
    {
        public MinDAESDbContext(DbContextOptions options) : base(options)
        {

        }
    }
}
