using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/audit")]
public class Phase2AuditController : ControllerBase
{
    private readonly AuditService _audit;
    private readonly Phase2DbContext _db;

    public Phase2AuditController(AuditService audit, Phase2DbContext db)
    {
        _audit = audit;
        _db = db;
    }

    public record ScanRequest(string? Serial, int? DeviceId, Phase2Stage? Stage);

    [HttpPost("scan")]
    [Authorize(Roles = $"{UserRoles.IctClerk},{UserRoles.IctInspector},{UserRoles.IctTechnician},{UserRoles.IctManager}")]
    public async Task<IActionResult> LogScan([FromBody] ScanRequest req)
    {
        var userId =
            User.FindFirst(ClaimTypes.Email)?.Value ??
            User.FindFirst("email")?.Value ??
            User.Identity?.Name ?? "unknown";

        int? deviceId = req.DeviceId;
        string? serial = req.Serial;

        if ((deviceId == null || deviceId == 0) && !string.IsNullOrWhiteSpace(serial))
        {
            var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Serial == serial);
            if (device != null)
            {
                deviceId = device.Id;
            }
        }

        var stageText = req.Stage?.ToString() ?? "Unknown";
        var details = $"Scan at stage: {stageText}";
        await _audit.LogAsync(userId, "Scan", deviceId, serial, details);

        return Ok(new { logged = true });
    }

    [HttpGet("events")]
    [Authorize(Roles = UserRoles.IctManager)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] int? deviceId = null,
        [FromQuery] string? serial = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        [FromQuery] string? sort = null
    )
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(a => a.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (deviceId.HasValue) q = q.Where(a => a.DeviceId == deviceId);
        if (!string.IsNullOrWhiteSpace(serial)) q = q.Where(a => a.DeviceSerial == serial);

        q = (sort?.ToLowerInvariant() == "asc") ? q.OrderBy(a => a.Timestamp) : q.OrderByDescending(a => a.Timestamp);

        var total = await q.CountAsync();
        var items = await q.Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 1000)).Select(a => new
        {
            a.Id,
            a.Timestamp,
            a.UserId,
            a.Action,
            a.DeviceId,
            a.DeviceSerial,
            a.Details
        }).ToListAsync();

        return Ok(new { total, items });
    }

    [HttpGet("export")]
    [Authorize(Roles = UserRoles.IctManager)]
    public async Task<IActionResult> Export(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] int? deviceId = null,
        [FromQuery] string? serial = null,
        [FromQuery] int take = 1000
    )
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(a => a.Timestamp <= to.Value);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(a => a.UserId == userId);
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        if (deviceId.HasValue) q = q.Where(a => a.DeviceId == deviceId);
        if (!string.IsNullOrWhiteSpace(serial)) q = q.Where(a => a.DeviceSerial == serial);

        var logs = await q.OrderByDescending(a => a.Timestamp).Take(Math.Clamp(take, 1, 10000)).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Id,Timestamp,UserId,Action,DeviceId,DeviceSerial,Details");
        foreach (var a in logs)
        {
            string esc(string? s) => s == null ? "" : s.Replace("\"", "\"\"");
            sb.AppendLine(
                string.Join(',', new[]
                {
                    a.Id.ToString(),
                    a.Timestamp.ToString("o"),
                    $"\"{esc(a.UserId)}\"",
                    $"\"{esc(a.Action)}\"",
                    a.DeviceId?.ToString() ?? "",
                    $"\"{esc(a.DeviceSerial)}\"",
                    $"\"{esc(a.Details)}\""
                })
            );
        }

        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"audit_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}