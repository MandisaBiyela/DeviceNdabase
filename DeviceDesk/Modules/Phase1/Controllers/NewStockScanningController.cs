using DeviceDesk.Modules.Phase1.Services;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Middleware;
using DeviceDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/newstock")]
    [Authorize]
    public class NewStockScanningController : ControllerBase
    {
        private readonly NewStockScanningService _service;
        private readonly RnrGrvService _rnrGrvService;
        private readonly ILogger<NewStockScanningController> _logger;

        public NewStockScanningController(NewStockScanningService service, RnrGrvService rnrGrvService, ILogger<NewStockScanningController> logger)
        {
            _service = service;
            _rnrGrvService = rnrGrvService;
            _logger = logger;
        }

        /// <summary>
        /// Get all batches pending scan from Phase 0
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingBatches(CancellationToken ct)
        {
            var batches = await _service.GetPendingBatchesAsync(ct);
            return Ok(batches);
        }

        /// <summary>
        /// Get batch details for scanning (blind copy)
        /// </summary>
        [HttpGet("batches/{batchId}")]
        public async Task<IActionResult> GetBatchForScanning(Guid batchId, CancellationToken ct)
        {
            var batch = await _service.GetBatchForScanningAsync(batchId, ct);
            
            if (batch == null)
                throw new NotFoundException("Batch", batchId);

            return Ok(batch);
        }

        /// <summary>
        /// Scan a device serial number
        /// </summary>
        [HttpPost("batches/{batchId}/scan")]
        public async Task<IActionResult> ScanDevice(Guid batchId, [FromBody] ScanDeviceRequest request, CancellationToken ct)
        {
            DeviceDesk.Services.ValidationService.ValidateRequired(request.SerialNumber, nameof(request.SerialNumber));
            DeviceDesk.Services.ValidationService.ValidateRequired(request.ScannedBy, nameof(request.ScannedBy));

            var result = await _service.ScanDeviceAsync(
                batchId,
                request.SerialNumber,
                request.IMEI,
                request.Brand,
                request.Model,
                request.ScannedBy,
                ct);

            return Ok(result);
        }

        /// <summary>
        /// Confirm batch and generate GRV
        /// </summary>
        [HttpPost("batches/{batchId}/confirm")]
        public async Task<IActionResult> ConfirmBatch(Guid batchId, [FromBody] ConfirmBatchRequest request, CancellationToken ct)
        {
            DeviceDesk.Services.ValidationService.ValidateRequired(request.ConfirmedBy, nameof(request.ConfirmedBy));

            var result = await _service.ConfirmBatchAsync(batchId, request.ConfirmedBy, request.Notes, ct);
            return Ok(result);
        }

        /// <summary>
        /// Delete a scanned device (undo scan)
        /// </summary>
        [HttpDelete("scans/{scanId}")]
        public async Task<IActionResult> DeleteScan(Guid scanId, CancellationToken ct)
        {
            var deleted = await _service.DeleteScannedDeviceAsync(scanId, ct);
            
            if (!deleted)
                throw new NotFoundException("Scan", scanId);

            return Ok(new { message = "Scan deleted successfully" });
        }

        #region Device Allocation Endpoints (New Stock)

        /// <summary>
        /// Set allocation for a single device in a New Stock batch
        /// </summary>
        [HttpPost("batches/{batchId}/allocate-device")]
        public async Task<IActionResult> AllocateDevice(
            Guid batchId,
            [FromBody] DeviceAllocationDto dto,
            CancellationToken ct = default)
        {
            try
            {
                var userId = User?.Identity?.Name ?? "system";
                await _rnrGrvService.SetDeviceAllocationAsync(batchId, dto, userId);
                return Ok(new { success = true, message = "Device allocation saved" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NewStock AllocateDevice] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to save device allocation", details = ex.Message });
            }
        }

        /// <summary>
        /// Set allocations for multiple devices in a New Stock batch (bulk operation)
        /// </summary>
        [HttpPost("batches/{batchId}/allocate-bulk")]
        public async Task<IActionResult> AllocateBulk(
            Guid batchId,
            [FromBody] BulkAllocationRequest request,
            CancellationToken ct = default)
        {
            try
            {
                if (batchId != request.BatchId)
                    return BadRequest(new { error = "BatchId mismatch" });
                
                var userId = User?.Identity?.Name ?? "system";
                await _rnrGrvService.SetBulkAllocationsAsync(request, userId);
                
                return Ok(new { 
                    success = true, 
                    message = $"Bulk allocation saved for {request.Allocations.Count} devices" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NewStock AllocateBulk] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to save bulk allocations", details = ex.Message });
            }
        }

        /// <summary>
        /// Get current allocations for all devices in a New Stock batch
        /// </summary>
        [HttpGet("batches/{batchId}/allocations")]
        public async Task<IActionResult> GetAllocations(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                var allocations = await _rnrGrvService.GetAllocationsAsync(batchId);
                return Ok(allocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NewStock GetAllocations] Error: {Message}", ex.Message);
                return StatusCode(500, new { error = "Failed to get allocations", details = ex.Message });
            }
        }

        #endregion
    }

    public record ScanDeviceRequest(
        string SerialNumber,
        string? IMEI,
        string? Brand,
        string? Model,
        string ScannedBy
    );

    public record ConfirmBatchRequest(
        string ConfirmedBy,
        string? Notes
    );
}
