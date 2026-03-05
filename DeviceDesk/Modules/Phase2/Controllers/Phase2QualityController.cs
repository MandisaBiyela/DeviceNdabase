using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeviceDesk.Modules.Phase2.Data;
using System.Security.Claims;
using System.Linq;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/quality")]
[Authorize(Roles = UserRoles.IctInspector)]
public class Phase2QualityController : ControllerBase
{
    private readonly QualityService _service;
    private readonly Phase2DbContext _db;
    public Phase2QualityController(QualityService service, Phase2DbContext db) { _service = service; _db = db; }

    public record QualityRequest(int DeviceId, string InspectorId, bool Passed, string? Notes, bool? ScanOutToDispatch);

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] QualityRequest req)
    {
        var userId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? req.InspectorId;
        
        // SECURITY: Inspectors cannot scan out devices - only ICT Clerk can do that
        // Force ScanOutToDispatch to false for Inspector role
        // Inspector's role is only to mark QA pass/fail; devices move to AwaitingDispatch
        // ICT Clerk must separately scan out devices using the dispatch scan-out functionality
        bool scanOutToDispatch = false;
        
        await _service.RecordQualityAsync(req.DeviceId, req.InspectorId, req.Passed, req.Notes, scanOutToDispatch, userId);
        return Ok();
    }

    // Inspector: Pre-assessment queue (devices received, not yet pre-assessed)
    [HttpGet("my/preassessment-queue")]
    public async Task<IActionResult> GetMyPreassessmentQueue([FromQuery] int take = 50)
    {
        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.Stage == Phase2Stage.Received && d.PreAssessmentPassed == null)
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
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(devices);
    }

    // Inspector: QA queue (devices awaiting quality assessment)
    [HttpGet("my/qa-queue")]
    public async Task<IActionResult> GetMyQaQueue([FromQuery] int take = 50)
    {
        var devices = await _db.Devices.AsNoTracking()
            .Where(d => d.Stage == Phase2Stage.QualityAssessment)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(d => new
            {
                d.Id,
                d.Serial,
                d.Stage,
                d.Zone,
                d.QaPassed,
                d.UpdatedAt
            })
            .ToListAsync();

        return Ok(devices);
    }

    // Inspector: My work history (action-based, from audit log)
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

        // Inspector actions: pre-assessment & QA
        var actions = AuditActionGroups.InspectorActions;

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
                QaPassed = d?.QaPassed,
                PreAssessmentPassed = d?.PreAssessmentPassed,
                Notes = p.Details ?? d?.PreAssessmentNotes
            };
        }).ToList();

        return Ok(new { total, skip, take, items });
    }
}
