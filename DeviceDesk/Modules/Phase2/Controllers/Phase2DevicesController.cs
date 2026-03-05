using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/devices")]
[Authorize(Roles = $"{UserRoles.IctClerk},{UserRoles.IctInspector},{UserRoles.IctTechnician},{UserRoles.IctManager}")]
public class Phase2DevicesController : ControllerBase
{
    private readonly Phase2DbContext _db;
    public Phase2DevicesController(Phase2DbContext db) { _db = db; }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id)
    {
        var d = await _db.Devices
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Serial,
                x.Stage,
                x.Zone,
                x.TechnicianId,
                x.InspectionDate,
                x.RepairCategory,
                x.UnderWarranty,
                x.DisposalRequested,
                x.QaPassed,
                x.ReworkCount,
                x.ReceiptId,
                x.UpdatedAt,
                x.PreAssessmentNotes
            })
            .SingleOrDefaultAsync();

        if (d == null) return NotFound();
        return Ok(d);
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Phase2Stage? stage, [FromQuery] Phase2Zone? zone, [FromQuery] string? serial)
    {
        var q = _db.Devices.AsQueryable();
        if (stage.HasValue) q = q.Where(d => d.Stage == stage);
        if (zone.HasValue) q = q.Where(d => d.Zone == zone);
        if (!string.IsNullOrWhiteSpace(serial))
        {
            var serialTrimmed = serial.Trim().ToLower();
            // Case-insensitive search: prefer exact match, but allow partial
            q = q.Where(d => d.Serial.ToLower() == serialTrimmed || d.Serial.ToLower().Contains(serialTrimmed));
        }
        var list = await q
            .AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt)
            .Take(500)
            .Select(d => new
            {
                d.Id,
                d.Serial,
                d.Stage,
                d.Zone,
                d.TechnicianId,
                d.InspectionDate,
                d.RepairCategory,
                d.UnderWarranty,
                d.DisposalRequested,
                d.QaPassed,
                d.ReworkCount,
                d.ReceiptId,
                d.UpdatedAt,
                d.PreAssessmentNotes,
                d.PreAssessmentPassed,
                // Computed flags
                IsDisposed = d.Stage == Phase2Stage.Disposal || _db.Disposals.Any(x => x.DeviceId == d.Id && x.IsApproved),
                PendingDisposal = _db.Disposals.Any(x => x.DeviceId == d.Id && !x.IsApproved)
            })
            .ToListAsync();
        return Ok(list);
    }
}
