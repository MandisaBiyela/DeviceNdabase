using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase2.Services;
using DeviceDesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace DeviceDesk.Modules.Phase2.Controllers;

[ApiController]
[Route("api/phase2/receipting")]
[Authorize(Roles = UserRoles.IctClerk)]
    public class Phase2ReceiptingController : ControllerBase
    {
        private readonly ReceiptingService _service;
        public Phase2ReceiptingController(ReceiptingService service) { _service = service; }

    public record ReceiptItem(string Serial, Phase2Zone Zone);
    public record CreateReceiptRequest(string GrvNumber, string ClerkId, List<ReceiptItem> Items);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReceiptRequest req)
    {
        if (req.Items == null || req.Items.Count == 0) return BadRequest(new { message = "No items provided" });
        try
        {
            var receipt = await _service.CreateReceiptAsync(req.GrvNumber, req.ClerkId, req.Items.Select(i => (i.Serial, i.Zone)));
            return Ok(new { success = true, receiptId = receipt.Id, grvNumber = receipt.GrvNumber, itemCount = receipt.ItemCount });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Returns pending GRVs from Phase 1 not yet receipted in Phase 2
    [HttpGet("pending-grvs")]
    public async Task<IActionResult> GetPendingGrvs()
    {
        var grvs = await _service.GetPendingGrvsAsync();
        return Ok(grvs);
    }
}
