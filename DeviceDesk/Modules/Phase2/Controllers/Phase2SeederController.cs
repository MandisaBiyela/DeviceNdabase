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
}

