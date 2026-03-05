using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase1.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/scanning")]
    public class ScanningController : ControllerBase
    {
        private readonly ScanningService _service;

        public ScanningController(ScanningService service)
        {
            _service = service;
        }

        /// <summary>
        /// Scan and validate a device
        /// </summary>
        [HttpPost("scan")]
        public async Task<ActionResult<ScanValidationResponse>> ScanDevice(
            [FromBody] Models.ScanDeviceRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _service.ValidateAndScanDeviceAsync(request, ct);
                
                // Return appropriate status code based on validation result
                if (!result.IsValid)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while scanning the device.", details = ex.Message });
            }
        }

        /// <summary>
        /// Get all scanned devices for a batch
        /// </summary>
        [HttpGet("batches/{batchId}/devices")]
        public async Task<ActionResult<List<ScannedDeviceDto>>> GetScannedDevices(
            Guid batchId,
            CancellationToken ct)
        {
            try
            {
                var devices = await _service.GetScannedDevicesAsync(batchId, ct);
                return Ok(devices);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Delete a scanned device (undo scan)
        /// </summary>
        [HttpDelete("devices/{itemId}")]
        public async Task<IActionResult> DeleteScannedDevice(
            Guid itemId,
            CancellationToken ct)
        {
            try
            {
                var deleted = await _service.DeleteScannedDeviceAsync(itemId, ct);
                if (!deleted)
                    return NotFound(new { error = "Device not found." });

                return Ok(new { message = "Device removed successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
