using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/dispatch")]
public class Phase2DispatchController : ControllerBase
{
    private readonly DispatchService _service;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<Phase2DispatchController> _logger;
    
    public Phase2DispatchController(DispatchService service, UserManager<ApplicationUser> userManager, ILogger<Phase2DispatchController> logger)
    {
        _service = service;
        _userManager = userManager;
        _logger = logger;
    }

    private string GetCurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? User.FindFirstValue("sub")
           ?? (User.Identity?.Name)
           ?? throw new UnauthorizedAccessException("No logged in user.");

    [HttpGet("ready")]
    [Authorize(Roles = UserRoles.IctClerk)]
    public async Task<IActionResult> Ready([FromQuery] string? filter)
    {
        var items = await _service.GetReadyForDispatchAsync(filter);
        var result = items.Select(d => new {
            id = d.Id,
            serial = d.Serial,
            receiptId = d.ReceiptId,
            qaPassed = d.QaPassed,
            scannedOutAt = d.ScannedOutAt
        });
        return Ok(result);
    }

    public record ScanOutBySerialRequest(string Serial);

    [HttpPost("scanout/by-serial")]
    [Authorize(Roles = UserRoles.IctClerk)]
    public async Task<IActionResult> ScanOutBySerial([FromBody] ScanOutBySerialRequest req)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("[ScanOutController] Scan-out request for serial {Serial} by user {UserId}", req.Serial, userId);
            var device = await _service.ScanOutBySerialAsync(req.Serial, userId);
            _logger.LogInformation("[ScanOutController] Scan-out successful for device {DeviceId} (Serial: {Serial})", device.Id, device.Serial);
            return Ok(new { message = "Scanned out", deviceId = device.Id, serial = device.Serial });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[ScanOutController] Scan-out failed for serial {Serial}: {Error}", req.Serial, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    public record ScanOutByIdRequest(int DeviceId);

    [HttpPost("scanout/by-id")]
    [Authorize(Roles = UserRoles.IctClerk)]
    public async Task<IActionResult> ScanOutById([FromBody] ScanOutByIdRequest req)
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("[ScanOutController] Scan-out request for device ID {DeviceId} by user {UserId}", req.DeviceId, userId);
            var device = await _service.ScanOutByIdAsync(req.DeviceId, userId);
            _logger.LogInformation("[ScanOutController] Scan-out successful for device {DeviceId} (Serial: {Serial})", device.Id, device.Serial);
            return Ok(new { message = "Scanned out", deviceId = device.Id, serial = device.Serial });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("[ScanOutController] Scan-out failed for device ID {DeviceId}: {Error}", req.DeviceId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("history")]
    [Authorize(Roles = UserRoles.IctClerk)]
    public async Task<IActionResult> History()
    {
        var items = await _service.GetScanOutHistoryAsync(50);
        
        // Get unique user IDs
        var userIds = items
            .Where(d => !string.IsNullOrEmpty(d.ScannedOutByUserId))
            .Select(d => d.ScannedOutByUserId!)
            .Distinct()
            .ToList();
        
        // Look up user names in batch
        // Note: ScannedOutByUserId might be an email/username, not always an ID
        var userNames = new Dictionary<string, string>();
        foreach (var userId in userIds)
        {
            try
            {
                ApplicationUser? user = null;
                
                // Try finding by ID first
                user = await _userManager.FindByIdAsync(userId);
                
                // If not found and it looks like an email, try finding by email
                if (user == null && userId.Contains("@"))
                {
                    user = await _userManager.FindByEmailAsync(userId);
                }
                
                // If still not found, try finding by username
                if (user == null)
                {
                    user = await _userManager.FindByNameAsync(userId);
                }
                
                if (user != null)
                {
                    // Prefer FullName, fallback to Email, then UserName
                    userNames[userId] = !string.IsNullOrEmpty(user.FullName) 
                        ? user.FullName 
                        : (!string.IsNullOrEmpty(user.Email) ? user.Email : user.UserName ?? userId);
                }
                else
                {
                    // If it looks like an email, use it directly; otherwise show as-is
                    userNames[userId] = userId.Contains("@") ? userId : userId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ScanOutHistory] Error looking up user {UserId}: {Error}", userId, ex.Message);
                // If it looks like an email, use it directly; otherwise show as-is
                userNames[userId] = userId.Contains("@") ? userId : userId;
            }
        }
        
        var result = items.Select(d => new {
            id = d.Id,
            serial = d.Serial,
            scannedOutAt = d.ScannedOutAt,
            scannedOutBy = !string.IsNullOrEmpty(d.ScannedOutByUserId) && userNames.ContainsKey(d.ScannedOutByUserId)
                ? userNames[d.ScannedOutByUserId]
                : d.ScannedOutByUserId ?? "Unknown"
        });
        return Ok(result);
    }
}