using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Models;
using DeviceDesk.Modules.Phase3.Services;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeviceDesk.Modules.Phase3.Controllers;

[ApiController]
[Route("api/phase3")]
[Authorize]
public class Phase3Controller : ControllerBase
{
    private readonly Phase3DispatchService _dispatch;
    private readonly Phase3DbContext _context;

    public Phase3Controller(Phase3DispatchService dispatch, Phase3DbContext context)
    {
        _dispatch = dispatch;
        _context = context;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private bool IsInRole(string role) => User.IsInRole(role);

    // ═══════════════════════════════════════════════════════════════════
    // PAGE 1: DISPATCH PREPARATION (Dispatch Clerk Only)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("ready-for-dispatch")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
    public async Task<IActionResult> GetReadyForDispatch()
    {
        var pods = await _dispatch.GetReadyForDispatchAsync();
        return Ok(pods);
    }

    [HttpPost("scan-to-dispatch")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
    public async Task<IActionResult> ScanToDispatch([FromBody] ScanRequest req)
    {
        var pod = await _dispatch.ScanDeviceToDispatchAsync(req.PODNumber, GetUserId());
        if (pod == null)
            return NotFound(new { message = "POD not found or not ready for dispatch" });

        return Ok(new { message = "Device scanned to dispatch", pod });
    }

    [HttpPost("create-trip")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
    public async Task<IActionResult> CreateTrip([FromBody] CreateTripRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TripRef) || string.IsNullOrWhiteSpace(req.DriverName) || 
            string.IsNullOrWhiteSpace(req.VehicleReg) || req.PODIds == null || !req.PODIds.Any())
        {
            return BadRequest(new { message = "TripRef, DriverName, VehicleReg, and PODIds are required" });
        }

        var trip = await _dispatch.CreateTripAsync(
            req.TripRef, 
            req.DriverName, 
            req.DriverUserId, 
            req.VehicleReg, 
            req.PODIds, 
            GetUserId()
        );

        return Ok(new { message = "Trip created", trip });
    }

    [HttpPost("trips/{tripId}/send-to-driver")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
    public async Task<IActionResult> SendTripToDriver(Guid tripId)
    {
        var success = await _dispatch.SendTripToDriverAsync(tripId, GetUserId());
        if (!success)
            return BadRequest(new { message = "Cannot send trip to driver" });

        return Ok(new { message = "Trip sent to driver for acceptance" });
    }

    // ═══════════════════════════════════════════════════════════════════
    // PAGE 2: TRANSPORT & HANDOVER (Driver Only)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("driver/my-trips")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.Admin}")]
    public async Task<IActionResult> GetMyTrips()
    {
        var trips = await _dispatch.GetDriverTripsAsync(GetUserId());
        return Ok(trips);
    }

    [HttpPost("driver/trips/{tripId}/accept")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.Admin}")]
    public async Task<IActionResult> AcceptTrip(Guid tripId)
    {
        var success = await _dispatch.AcceptTripAsync(tripId, GetUserId());
        if (!success)
            return BadRequest(new { message = "Cannot accept this trip" });

        return Ok(new { message = "Trip accepted. You can now begin delivery." });
    }

    [HttpPost("driver/pods/{podId}/deliver")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.Admin}")]
    public async Task<IActionResult> MarkPODDelivered(Guid podId, [FromBody] DeliverPODRequest req)
    {
        var success = await _dispatch.MarkPODDeliveredAsync(
            podId, 
            GetUserId(), 
            req.SchoolSigned, 
            req.SignatoryName, 
            req.HasExceptions, 
            req.ExceptionNotes
        );

        if (!success)
            return BadRequest(new { message = "Cannot mark POD as delivered" });

        return Ok(new { message = "POD marked as delivered" });
    }

    [HttpPost("driver/pods/{podId}/upload-signed-pod")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.Admin}")]
    public async Task<IActionResult> UploadSignedPOD(Guid podId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        // Store the file (you'll need to implement document storage)
        // For now, we'll just store a placeholder document ID
        long documentId = DateTime.UtcNow.Ticks; // Replace with actual document storage

        var success = await _dispatch.UploadSignedPODAsync(podId, documentId, GetUserId());
        if (!success)
            return NotFound(new { message = "POD not found" });

        return Ok(new { message = "Signed POD uploaded", documentId });
    }

    [HttpPost("driver/trips/{tripId}/complete")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Driver},{UserRoles.Admin}")]
    public async Task<IActionResult> CompleteTrip(Guid tripId)
    {
        var success = await _dispatch.CompleteTripAsync(tripId, GetUserId());
        if (!success)
            return BadRequest(new { message = "Cannot complete trip. Ensure all PODs are delivered." });

        return Ok(new { message = "Trip completed and sent for debriefing" });
    }

    // ═══════════════════════════════════════════════════════════════════
    // PAGE 3: DEBRIEFING (Dispatch QA Only)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("qa/debriefing-trips")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.DispatchQA},{UserRoles.Admin}")]
    public async Task<IActionResult> GetDebriefingTrips()
    {
        var trips = await _dispatch.GetDebriefingTripsAsync();
        return Ok(trips);
    }

    [HttpPost("qa/trips/{tripId}/debriefing")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.DispatchQA},{UserRoles.Admin}")]
    public async Task<IActionResult> SubmitDebriefing(Guid tripId, [FromBody] DebriefingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Notes))
            return BadRequest(new { message = "Notes are required" });

        var success = await _dispatch.SubmitDebriefingAsync(tripId, req.Passed, req.Notes, GetUserId());
        if (!success)
            return BadRequest(new { message = "Cannot submit debriefing" });

        var status = req.Passed ? "passed and sent for final sign-off" : "failed and returned to driver";
        return Ok(new { message = $"Debriefing {status}" });
    }

    // ═══════════════════════════════════════════════════════════════════
    // PAGE 4: FINAL SIGN-OFF (Dispatch Manager Only)
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("manager/signoff-trips")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.DispatchManager},{UserRoles.Admin}")]
    public async Task<IActionResult> GetSignOffTrips()
    {
        var trips = await _dispatch.GetSignOffTripsAsync();
        return Ok(trips);
    }

    [HttpPost("manager/trips/{tripId}/signoff")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.DispatchManager},{UserRoles.Admin}")]
    public async Task<IActionResult> SubmitFinalSignOff(Guid tripId, [FromBody] SignOffRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Notes))
            return BadRequest(new { message = "Notes are required" });

        var success = await _dispatch.SubmitFinalSignOffAsync(tripId, req.Passed, req.Notes, GetUserId());
        if (!success)
            return BadRequest(new { message = "Cannot submit sign-off" });

        var status = req.Passed ? "approved and closed" : "rejected and sent back to QA";
        return Ok(new { message = $"Trip {status}" });
    }

    // ═══════════════════════════════════════════════════════════════════
    // GENERAL QUERIES
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("trips/{tripId}")]
    public async Task<IActionResult> GetTripDetails(Guid tripId)
    {
        var trip = await _dispatch.GetTripDetailsAsync(tripId);
        if (trip == null)
            return NotFound(new { message = "Trip not found" });

        return Ok(trip);
    }

    [HttpGet("trips")]
    public async Task<IActionResult> GetAllTrips()
    {
        var trips = await _dispatch.GetAllTripsAsync();
        return Ok(trips);
    }

    [HttpGet("pods")]
    public async Task<IActionResult> GetAllPODs()
    {
        var pods = await _context.DispatchPODs
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        return Ok(pods);
    }

    // ═══════════════════════════════════════════════════════════════════
    // CROSS-PHASE INTEGRATION: Access Phase 2 data
    // ═══════════════════════════════════════════════════════════════════

    [HttpGet("phase2/devices/ready-for-dispatch")]
    [Authorize(Roles = $"{UserRoles.DispatchClerk},{UserRoles.Admin}")]
    public async Task<IActionResult> GetPhase2DevicesReadyForDispatch()
    {
        // Access Phase 2 database to get devices that have passed QA and are ready for dispatch
        // Include both AwaitingDispatch (before scan-out) and Dispatch (after Phase 2 scan-out) stages
        var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
        
        var devices = await phase2Db.Devices
            .Where(d => (d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.AwaitingDispatch 
                      || d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Dispatch)
                      && d.QaPassed == true)
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id,
                d.Serial,
                Stage = d.Stage.ToString(),
                QaPassed = d.QaPassed,
                ScannedOutAt = d.ScannedOutAt,
                UpdatedAt = d.UpdatedAt
            })
            .ToListAsync();
            
        return Ok(devices);
    }

    // ═══════════════════════════════════════════════════════════════════
    // REQUEST MODELS
    // ═══════════════════════════════════════════════════════════════════

    public record ScanRequest(string PODNumber);

    public record CreateTripRequest(
        string TripRef,
        string DriverName,
        string? DriverUserId,
        string VehicleReg,
        List<Guid> PODIds
    );

    public record DeliverPODRequest(
        bool SchoolSigned,
        string? SignatoryName,
        bool HasExceptions,
        string? ExceptionNotes
    );

    public record DebriefingRequest(
        bool Passed,
        string Notes
    );

    public record SignOffRequest(
        bool Passed,
        string Notes
    );
}
