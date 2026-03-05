using DeviceDesk.Modules.Phase1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/model-scanning")]
    [AllowAnonymous]
    public class ModelScanningController : ControllerBase
    {
        private readonly ModelDrivenScanningService _scanningService;
        private readonly ILogger<ModelScanningController> _logger;

        public ModelScanningController(
            ModelDrivenScanningService scanningService,
            ILogger<ModelScanningController> logger)
        {
            _scanningService = scanningService;
            _logger = logger;
        }

        /// <summary>
        /// Step 1: Get available orders for selection
        /// </summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(CancellationToken ct)
        {
            try
            {
                var orders = await _scanningService.GetAvailableOrdersAsync(ct);
                return Ok(new { success = true, orders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error fetching orders");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 2: Get all models for selected order
        /// </summary>
        [HttpGet("orders/{orderID:guid}/models")]
        public async Task<IActionResult> GetModels(Guid orderID, CancellationToken ct)
        {
            try
            {
                var models = await _scanningService.GetModelsForOrderAsync(orderID, ct);
                
                if (models.Count == 0)
                {
                    return NotFound(new { success = false, error = "No models found for this order" });
                }

                return Ok(new { success = true, models });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error fetching models for order {OrderID}", orderID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 4: Scan a serial for the active model
        /// </summary>
        [HttpPost("orders/{orderID:guid}/models/{modelID:guid}/scan")]
        public async Task<IActionResult> ScanSerial(
            Guid orderID, 
            Guid modelID, 
            [FromBody] ScanRequest request, 
            CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Serial))
                {
                    return BadRequest(new { success = false, error = "Serial number is required" });
                }

                var result = await _scanningService.ScanSerialAsync(orderID, modelID, request.Serial.Trim(), ct);

                if (!result.Success)
                {
                    return BadRequest(new { success = false, error = result.Message });
                }

                return Ok(new 
                { 
                    success = true, 
                    message = result.Message,
                    countedQty = result.CountedQty,
                    expectedQty = result.ExpectedQty,
                    remaining = result.Remaining
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error scanning serial for model {ModelID}", modelID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 5: Close a model after scanning is complete
        /// </summary>
        [HttpPost("models/{modelID:guid}/close")]
        public async Task<IActionResult> CloseModel(Guid modelID, CancellationToken ct)
        {
            try
            {
                var success = await _scanningService.CloseModelAsync(modelID, ct);

                if (!success)
                {
                    return NotFound(new { success = false, error = "Model not found" });
                }

                return Ok(new { success = true, message = "Model closed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error closing model {ModelID}", modelID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 7: Calculate variance for all models in order
        /// </summary>
        [HttpGet("orders/{orderID:guid}/variance")]
        public async Task<IActionResult> CalculateVariance(Guid orderID, CancellationToken ct)
        {
            try
            {
                var variance = await _scanningService.CalculateVarianceAsync(orderID, ct);

                return Ok(new 
                { 
                    success = true,
                    orderID = variance.OrderID,
                    allModelsClosed = variance.AllModelsClosed,
                    allQuantitiesMatch = variance.AllQuantitiesMatch,
                    hasShortages = variance.HasShortages,
                    canGenerateGRV = variance.CanGenerateGRV,
                    models = variance.Models
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error calculating variance for order {OrderID}", orderID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get scanned serials for a specific model
        /// </summary>
        [HttpGet("orders/{orderID:guid}/models/{modelID:guid}/serials")]
        public async Task<IActionResult> GetScannedSerials(Guid orderID, Guid modelID, CancellationToken ct)
        {
            try
            {
                var serials = await _scanningService.GetScannedSerialsAsync(orderID, modelID, ct);
                return Ok(serials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error fetching serials for model {ModelID}", modelID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Step 8: Generate GRV after all models scanned with zero variance
        /// </summary>
        [HttpPost("orders/{orderID:guid}/generate-grv")]
        public async Task<IActionResult> GenerateGRV(Guid orderID, CancellationToken ct)
        {
            try
            {
                // Generate GRV number
                var grvNumber = $"GRV-NS-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

                // Update batch with GRV number and mark as confirmed
                var batch = await _scanningService.ConfirmBatchAsync(orderID, grvNumber, ct);

                if (batch == null)
                {
                    return NotFound(new { success = false, error = "Batch not found" });
                }

                _logger.LogInformation("[ModelScanning] GRV generated for order {OrderID}: {GRVNumber}", orderID, grvNumber);

                return Ok(new 
                { 
                    success = true, 
                    grvNumber = grvNumber,
                    batchNumber = batch.BatchNumber,
                    message = "GRV generated successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error generating GRV for order {OrderID}", orderID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// Get GRV document data for viewing/downloading
        /// </summary>
        [HttpGet("orders/{orderID:guid}/grv")]
        public async Task<IActionResult> GetGRV(Guid orderID, CancellationToken ct = default)
        {
            try
            {
                var grvData = await _scanningService.GetGRVDataAsync(orderID, ct);

                if (grvData == null)
                    return NotFound(new { success = false, error = "GRV not found or not yet generated" });

                _logger.LogInformation("[ModelScanning] Retrieved GRV {GRVNumber} for order {OrderID}", grvData.GRVNumber, orderID);
                return Ok(grvData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ModelScanning] Error retrieving GRV for order {OrderID}", orderID);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class ScanRequest
    {
        public string Serial { get; set; } = string.Empty;
    }
}

