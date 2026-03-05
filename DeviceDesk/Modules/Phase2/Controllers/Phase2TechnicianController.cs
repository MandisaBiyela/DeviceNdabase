using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/technician")]
[Authorize(Roles = UserRoles.IctTechnician)]
public class Phase2TechnicianController : ControllerBase
{
    private readonly Phase2DbContext _db;
    public Phase2TechnicianController(Phase2DbContext db) { _db = db; }

    [HttpGet("queue")]
    public async Task<IActionResult> GetDetailedInspectionQueue([FromQuery] int take = 50)
    {
        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.Stage == Phase2Stage.DetailedInspection)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(take)
            .Select(d => new
            {
                d.Id,
                d.Serial,
                d.Stage,
                d.Zone,
                d.AttentionRequired,
                d.PreAssessmentPassed,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(devices);
    }

    // Technician: My assigned/pool queue (DetailedInspection stage, assigned to me or unassigned)
    [HttpGet("my/queue")]
    public async Task<IActionResult> GetMyQueue([FromQuery] int take = 50)
    {
        var userId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? "unknown";

        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.Stage == Phase2Stage.DetailedInspection && (d.TechnicianId == userId || d.TechnicianId == null))
            .OrderByDescending(d => d.UpdatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(d => new
            {
                d.Id,
                d.Serial,
                d.Stage,
                d.Zone,
                d.AttentionRequired,
                d.PreAssessmentPassed,
                d.TechnicianId,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(devices);
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetDetailedInspectionCount()
    {
        var count = await _db.Devices
            .Where(d => d.Stage == Phase2Stage.DetailedInspection)
            .CountAsync();
        return Ok(count);
    }

    // Technician: My work history (action-based, from audit log)
    [HttpGet("my/history")]
    public async Task<IActionResult> GetMyHistory(
        [FromQuery] int days = 30,
        [FromQuery] int take = 50,
        [FromQuery] int skip = 0
    )
    {
        var userId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? "unknown";

        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

        var since = DateTime.UtcNow.AddDays(-Math.Max(1, days));

        // Technician actions we care about
        var actions = AuditActionGroups.TechnicianActions;

        var baseQuery = _db.AuditLogs.AsNoTracking()
            .Where(a => a.UserId == userId
                        && a.Timestamp >= since
                        && actions.Contains(a.Action));

        var total = await baseQuery.CountAsync();

        var page = await baseQuery
            .OrderByDescending(a => a.Timestamp)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new
            {
                a.Timestamp,
                a.Action,
                a.DeviceId,
                a.DeviceSerial,
                a.Details
            })
            .ToListAsync();

        var deviceIds = page
            .Where(p => p.DeviceId.HasValue)
            .Select(p => p.DeviceId!.Value)
            .Distinct()
            .ToList();

        var devices = await _db.Devices.AsNoTracking()
            .Where(d => deviceIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);

        var items = page.Select(p =>
        {
            var did = p.DeviceId ?? 0;
            devices.TryGetValue(did, out var d);
            return new
            {
                Timestamp = p.Timestamp,
                Action = p.Action,
                DeviceId = p.DeviceId,
                Serial = !string.IsNullOrEmpty(p.DeviceSerial) ? p.DeviceSerial : d?.Serial,
                Stage = d?.Stage,
                StageName = d != null ? d.Stage.ToString() : null,
                Zone = d?.Zone,
                UnderWarranty = d?.UnderWarranty,
                Repairable = d?.Repairable,
                DisposalRequested = d?.DisposalRequested,
                AttentionRequired = d?.AttentionRequired,
                PreAssessmentPassed = d?.PreAssessmentPassed,
                Notes = p.Details ?? d?.PreAssessmentNotes,
                RepairCategory = d?.RepairCategory
            };
        }).ToList();

        return Ok(new { total, skip, take, items });
    }
}