using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/disposal")]
public class Phase2DisposalController : ControllerBase
{
    private readonly DisposalService _service;
    public Phase2DisposalController(DisposalService service)
    {
        _service = service;
    }

    public record DisposalRequestRequest(int DeviceId, string Reason);
    public record DisposalApproveRequest(int DisposalId, string ManagerId, string ManagerPin, string ManagerSignature);

    private string GetCurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? (User.Identity?.Name)
           ?? throw new UnauthorizedAccessException("No logged in user.");

    [HttpPost("request")]
    [Authorize(Roles = UserRoles.IctTechnician)]
    public async Task<IActionResult> CreateRequest([FromBody] DisposalRequestRequest req)
    {
        try
        {
            var technicianId = GetCurrentUserId();
            var result = await _service.RequestDisposalAsync(req.DeviceId, technicianId, req.Reason);
            return Ok(new { DisposalId = result.DisposalId, Reused = result.Reused });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("approve")]
    [Authorize(Roles = UserRoles.IctTechnician)]
    public async Task<IActionResult> Approve([FromBody] DisposalApproveRequest req)
    {
        try
        {
            await _service.ApproveDisposalAsync(req.DisposalId, req.ManagerId, req.ManagerPin, req.ManagerSignature);
            return Ok(new { message = "Disposal approved." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // List pending disposals for a technician (history card)
    [HttpGet("pending")]
    [Authorize(Roles = UserRoles.IctTechnician)]
    public async Task<IActionResult> Pending()
    {
        var technicianId = GetCurrentUserId();
        var records = await _service.ListPendingByTechnicianAsync(technicianId);
        var result = records.Select(d => new
        {
            id = d.Id,
            deviceId = d.DeviceId,
            serial = d.Device?.Serial,
            reason = d.Reason,
            requestedAt = d.RequestedAt,
            isApproved = d.IsApproved,
            approvedAt = d.ApprovedAt
        });
        return Ok(result);
    }
}
