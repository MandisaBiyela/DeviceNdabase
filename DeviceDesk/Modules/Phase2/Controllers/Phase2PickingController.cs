using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/picking")]
[Authorize(Roles = UserRoles.IctAllocator)]
public class Phase2PickingController : ControllerBase
{
    private readonly PickingService _pickingService;

    public Phase2PickingController(PickingService pickingService)
    {
        _pickingService = pickingService;
    }

    /// <summary>
    /// Search devices available for picking
    /// </summary>
    [HttpGet("search-devices")]
    public async Task<IActionResult> SearchDevices(
        [FromQuery] long? schoolId = null,
        [FromQuery] string? district = null,
        [FromQuery] string? serial = null,
        [FromQuery] string? stage = null,
        CancellationToken ct = default)
    {
        try
        {
            Phase2Stage? stageEnum = null;
            if (!string.IsNullOrWhiteSpace(stage) && Enum.TryParse<Phase2Stage>(stage, out var parsedStage))
            {
                stageEnum = parsedStage;
            }

            var devices = await _pickingService.SearchDevicesForPickingAsync(
                schoolId,
                district,
                serial,
                stageEnum,
                ct);

            return Ok(devices);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a new picking slip
    /// </summary>
    [HttpPost("slips")]
    public async Task<IActionResult> CreatePickingSlip(
        [FromBody] CreatePickingSlipRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request.DeviceIds == null || request.DeviceIds.Count == 0)
            {
                return BadRequest(new { message = "At least one device ID is required." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "unknown";

            var slip = await _pickingService.CreatePickingSlipAsync(
                request.SchoolId,
                request.RequestedCollectionDate,
                request.Notes,
                request.Reference,
                request.DeviceIds,
                userId,
                ct);

            // Return detail DTO
            var detail = await _pickingService.GetPickingSlipAsync(slip.Id, ct);

            return Ok(new
            {
                success = true,
                message = $"Picking slip {slip.SlipNumber} created successfully with {slip.Items.Count} device(s).",
                slip = detail
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get list of picking slips with optional filters
    /// </summary>
    [HttpGet("slips")]
    public async Task<IActionResult> GetPickingSlips(
        [FromQuery] string? status = null,
        [FromQuery] long? schoolId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] string? createdBy = null,
        CancellationToken ct = default)
    {
        try
        {
            PickingSlipStatus? statusEnum = null;
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PickingSlipStatus>(status, out var parsedStatus))
            {
                statusEnum = parsedStatus;
            }

            var slips = await _pickingService.GetPickingSlipsAsync(
                statusEnum,
                schoolId,
                dateFrom,
                dateTo,
                createdBy,
                ct);

            return Ok(slips);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get single picking slip by ID for viewing/printing
    /// </summary>
    [HttpGet("slips/{id:guid}")]
    public async Task<IActionResult> GetPickingSlip(Guid id, CancellationToken ct = default)
    {
        try
        {
            var slip = await _pickingService.GetPickingSlipAsync(id, ct);

            if (slip == null)
            {
                return NotFound(new { message = "Picking slip not found." });
            }

            return Ok(slip);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update picking slip status
    /// </summary>
    [HttpPost("slips/{id:guid}/set-status")]
    public async Task<IActionResult> SetSlipStatus(
        Guid id,
        [FromBody] SetStatusRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (!Enum.TryParse<PickingSlipStatus>(request.Status, out var status))
            {
                return BadRequest(new { message = $"Invalid status: {request.Status}" });
            }

            var success = await _pickingService.UpdateSlipStatusAsync(id, status, ct);

            if (!success)
            {
                return NotFound(new { message = "Picking slip not found." });
            }

            return Ok(new { success = true, message = $"Status updated to {request.Status}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    public record CreatePickingSlipRequest(
        long? SchoolId,
        DateTime? RequestedCollectionDate,
        string? Notes,
        string? Reference,
        List<int> DeviceIds);

    public record SetStatusRequest(string Status);
}

