using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/schools")]
    public class SchoolsController : ControllerBase
    {
        private readonly DeviceDeskDbContext _db;
        public SchoolsController(DeviceDeskDbContext db) => _db = db;

        public record UpsertDto(string EmisCode, string Name, string? District, string? Address);

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] UpsertDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EmisCode) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("EMIS and Name required");

            var s = await _db.Schools.FirstOrDefaultAsync(x => x.EmisCode == dto.EmisCode);
            if (s == null)
            {
                s = new School { EmisCode = dto.EmisCode, Name = dto.Name, District = dto.District, Address = dto.Address };
                _db.Schools.Add(s);
            }
            else
            {
                s.Name = dto.Name; s.District = dto.District; s.Address = dto.Address;
            }
            await _db.SaveChangesAsync();
            return Ok(new { schoolId = s.SchoolId, s.EmisCode, s.Name, s.District, s.Address });
        }
    }
}