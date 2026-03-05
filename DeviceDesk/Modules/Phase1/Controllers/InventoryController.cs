using DeviceDesk.Modules.Phase1.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly InventoryIntegrationService _service;

        public InventoryController(InventoryIntegrationService service)
        {
            _service = service;
        }

        /// <summary>
        /// Get inventory statistics (Phase 0 + Phase 1)
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<InventoryStatsDto>> GetStats(CancellationToken ct)
        {
            try
            {
                var stats = await _service.GetInventoryStatsAsync(ct);
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Check for duplicates in Phase 0 inventory
        /// </summary>
        [HttpGet("check-duplicates/{batchId}")]
        public async Task<ActionResult<List<string>>> CheckDuplicates(Guid batchId, CancellationToken ct)
        {
            try
            {
                var duplicates = await _service.CheckDuplicatesInInventoryAsync(batchId, ct);
                return Ok(new { duplicates, count = duplicates.Count });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Manually trigger transfer to inventory (if auto-transfer failed)
        /// </summary>
        [HttpPost("transfer/{batchId}")]
        public async Task<IActionResult> TransferToInventory(Guid batchId, CancellationToken ct)
        {
            try
            {
                var count = await _service.TransferToInventoryAsync(batchId, ct);
                return Ok(new { message = $"Successfully transferred {count} devices to inventory.", count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to transfer devices.", details = ex.Message });
            }
        }
    }
}
