using DeviceDesk.Modules.Phase2.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/seed")]
[Authorize(Roles = "SuperAdmin")] // keep this locked down
public class Phase2SeederController : ControllerBase
{
    private readonly Phase2DbContext _db;

    public Phase2SeederController(Phase2DbContext db)
    {
        _db = db;
    }

    [HttpPost("synthetic-devices")]
    public async Task<IActionResult> SeedSyntheticDevices(
        [FromQuery] int targetTotal = 375_000,
        [FromQuery] double successRate = 0.97, // 97% pass rate
        CancellationToken ct = default)
    {
        await Phase2SyntheticSeeder.SeedSyntheticDevicesAsync(_db, targetTotal, successRate, ct);

        var total = await _db.Devices.CountAsync(ct);
        var processed = await _db.Devices.CountAsync(d => d.QaPassed != null, ct);
        var passed = await _db.Devices.CountAsync(d => d.QaPassed == true, ct);
        var failed = await _db.Devices.CountAsync(d => d.QaPassed == false, ct);

        return Ok(new
        {
            message = "Synthetic seeding complete.",
            totalDevices = total,
            processedDevices = processed,
            passed,
            failed
        });
    }

    [HttpGet("count")]
    public async Task<IActionResult> GetDeviceCount(CancellationToken ct)
    {
        var total = await _db.Devices.CountAsync(ct);
        return Ok(new { totalDevices = total });
    }

    [HttpPost("mark-all-qa-passed")]
    public async Task<IActionResult> MarkAllDevicesAsQAPassed(
        [FromQuery] bool passed = true,
        CancellationToken ct = default)
    {
        // Get all devices that don't have QA status set
        var devicesToUpdate = await _db.Devices
            .Where(d => d.QaPassed == null)
            .ToListAsync(ct);

        if (devicesToUpdate.Count == 0)
        {
            return Ok(new
            {
                message = "All devices already have QA status set.",
                totalDevices = await _db.Devices.CountAsync(ct),
                processedDevices = await _db.Devices.CountAsync(d => d.QaPassed != null, ct)
            });
        }

        // Mark them all as passed (or failed based on parameter)
        foreach (var device in devicesToUpdate)
        {
            device.QaPassed = passed;
            device.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        var total = await _db.Devices.CountAsync(ct);
        var processed = await _db.Devices.CountAsync(d => d.QaPassed != null, ct);
        var passedCount = await _db.Devices.CountAsync(d => d.QaPassed == true, ct);
        var failed = await _db.Devices.CountAsync(d => d.QaPassed == false, ct);

        return Ok(new
        {
            message = $"Successfully marked {devicesToUpdate.Count} devices as QA {(passed ? "passed" : "failed")}.",
            updated = devicesToUpdate.Count,
            totalDevices = total,
            processedDevices = processed,
            passed = passedCount,
            failed
        });
    }
}

