using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/storage-templates")]
[Authorize(Roles = UserRoles.IctAllocator + "," + UserRoles.Admin)]
public class StorageTemplateController : ControllerBase
{
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;

    public StorageTemplateController(Phase2DbContext phase2Db, DeviceDeskDbContext coreDb)
    {
        _phase2Db = phase2Db;
        _coreDb = coreDb;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTemplates(CancellationToken ct)
    {
        var templates = await _phase2Db.SchoolStorageTemplates
            .AsNoTracking()
            .OrderBy(t => t.SchoolId)
            .ThenBy(t => t.Category)
            .ToListAsync(ct);

        // Get school names and EMIS codes
        var schoolIds = templates.Select(t => t.SchoolId).Distinct().ToList();
        var schools = await _coreDb.Schools
            .AsNoTracking()
            .Where(s => schoolIds.Contains(s.SchoolId))
            .ToDictionaryAsync(s => s.SchoolId, s => new { s.Name, s.EmisCode }, ct);

        var result = templates.Select(t => new
        {
            t.Id,
            t.SchoolId,
            schoolName = schools.GetValueOrDefault(t.SchoolId)?.Name ?? $"School {t.SchoolId}",
            schoolEmisCode = schools.GetValueOrDefault(t.SchoolId)?.EmisCode,
            category = t.Category.ToString(),
            t.Building,
            t.Room,
            t.RackPattern,
            t.ShelfPattern,
            t.BinPattern,
            t.MaxRacks,
            t.MaxShelvesPerRack,
            t.MaxBinsPerShelf,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt
        }).ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTemplate(int id, CancellationToken ct)
    {
        var template = await _phase2Db.SchoolStorageTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound();

        return Ok(new
        {
            template.Id,
            template.SchoolId,
            category = template.Category.ToString(),
            template.Building,
            template.Room,
            template.RackPattern,
            template.ShelfPattern,
            template.BinPattern,
            template.MaxRacks,
            template.MaxShelvesPerRack,
            template.MaxBinsPerShelf,
            template.IsActive
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTemplate(int id, [FromBody] UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await _phase2Db.SchoolStorageTemplates
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (template == null)
            return NotFound();

        template.Building = request.Building;
        template.Room = request.Room;
        template.RackPattern = request.RackPattern;
        template.ShelfPattern = request.ShelfPattern;
        template.BinPattern = request.BinPattern;
        template.MaxRacks = request.MaxRacks;
        template.MaxShelvesPerRack = request.MaxShelvesPerRack;
        template.MaxBinsPerShelf = request.MaxBinsPerShelf;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _phase2Db.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "Template updated successfully." });
    }

    [HttpPost("generate-all")]
    public async Task<IActionResult> GenerateAllTemplates(CancellationToken ct)
    {
        try
        {
            var created = await StorageTemplateSeeder.GenerateForAllSchoolsAsync(_coreDb, _phase2Db, ct);
            return Ok(new
            {
                success = true,
                created,
                message = $"Generated {created} storage templates."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error generating templates", error = ex.Message });
        }
    }

    public record UpdateTemplateRequest(
        string Building,
        string Room,
        string RackPattern,
        string ShelfPattern,
        string BinPattern,
        int MaxRacks,
        int MaxShelvesPerRack,
        int MaxBinsPerShelf);
}

