using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Controllers;

[Route("api/phase1/schools")]
[ApiController]
[Authorize]
public class SchoolsLookupController : ControllerBase
{
    private readonly DeviceDeskDbContext _coreDb;

    public SchoolsLookupController(DeviceDeskDbContext coreDb)
    {
        _coreDb = coreDb;
    }

    /// <summary>
    /// Search schools by partial name or EMIS code.
    /// GET /api/phase1/schools/search?term=umlazi
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<SchoolLookupDto[]>> Search(
        [FromQuery] string term,
        CancellationToken ct)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length < 2)
        {
            // Avoid hammering DB on single letters
            return Ok(Array.Empty<SchoolLookupDto>());
        }

        var termLower = term.ToLower();
        var results = await _coreDb.Schools
            .AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(termLower) || 
                       s.EmisCode.ToLower().Contains(termLower))
            .OrderBy(s => s.Name)
            .Take(20)
            .Select(s => new SchoolLookupDto
            {
                SchoolId = (int)s.SchoolId, // Cast long to int
                EmisCode = s.EmisCode,
                Name = s.Name,
                District = s.District ?? string.Empty,
                Circuit = s.Circuit ?? string.Empty,
                Cmc = s.Cmc ?? string.Empty
            })
            .ToArrayAsync(ct);

        return Ok(results);
    }
}

