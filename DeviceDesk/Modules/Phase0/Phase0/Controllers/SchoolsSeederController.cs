using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/admin/schools")]
    [Authorize(Roles = "Admin,SuperAdmin")] // Only admins can seed
    public class SchoolsSeederController : ControllerBase
    {
        private readonly DeviceDeskDbContext _db;
        private readonly IWebHostEnvironment _env;

        public SchoolsSeederController(DeviceDeskDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        [HttpPost("seed")]
        public async Task<IActionResult> SeedSchools(CancellationToken ct)
        {
            try
            {
                var csvPath = Path.Combine(_env.ContentRootPath, "Data", "Seeds", "schools_emis.csv");
                
                if (!System.IO.File.Exists(csvPath))
                {
                    return BadRequest(new { message = $"CSV file not found at: {csvPath}" });
                }

                await SchoolsSeeder.SeedFromCsvAsync(_db, csvPath, ct);

                var count = await _db.Schools.CountAsync(ct);
                return Ok(new 
                { 
                    message = "Schools seeding completed. Check console for details.",
                    totalSchools = count,
                    csvPath = csvPath
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new 
                { 
                    message = "Error seeding schools", 
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("count")]
        public async Task<IActionResult> GetSchoolCount(CancellationToken ct)
        {
            var count = await _db.Schools.CountAsync(ct);
            return Ok(new { totalSchools = count });
        }
    }
}

