using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Middleware;
using DeviceDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/newstock")]
    [Authorize]
    public class NewStockBatchController : ControllerBase
    {
        private readonly NewStockBatchService _service;
        private readonly ILogger<NewStockBatchController> _logger;

        public NewStockBatchController(NewStockBatchService service, ILogger<NewStockBatchController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Create a new stock batch with item descriptions
        /// </summary>
        [HttpPost("batches")]
        public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request, CancellationToken ct)
        {
            DeviceDesk.Services.ValidationService.ValidateRequired(request.CreatedBy, nameof(request.CreatedBy));
            
            if (request.Items == null || request.Items.Count == 0)
                throw new ValidationException("items", "At least one item is required");

            var batch = await _service.CreateBatchAsync(
                request.SupplierName ?? "",
                request.InvoiceNumber,
                request.ExpectedDeliveryDate,
                request.Items,
                request.CreatedBy,
                ct);

            return CreatedAtAction(nameof(GetBatchDetails), new { batchId = batch.BatchId }, new
            {
                batchId = batch.BatchId,
                batchNumber = batch.BatchNumber,
                totalQuantityExpected = batch.TotalQuantityExpected,
                status = batch.Status.ToString()
            });
        }

        /// <summary>
        /// Get all batches with optional status filter
        /// </summary>
        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches([FromQuery] string? status, CancellationToken ct)
        {
            Modules.Phase0.Models.NewStockBatchStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<Modules.Phase0.Models.NewStockBatchStatus>(status, true, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }

            var batches = await _service.GetBatchesAsync(statusFilter, ct);
            return Ok(batches);
        }

        /// <summary>
        /// Get batch details with items
        /// </summary>
        [HttpGet("batches/{batchId}")]
        public async Task<IActionResult> GetBatchDetails(Guid batchId, CancellationToken ct)
        {
            var batch = await _service.GetBatchDetailsAsync(batchId, ct);
            
            if (batch == null)
                throw new NotFoundException("Batch", batchId);

            return Ok(batch);
        }
    }

    public record CreateBatchRequest(
        string? SupplierName,
        string? InvoiceNumber,
        DateTime? ExpectedDeliveryDate,
        List<NewStockBatchItemDto> Items,
        string CreatedBy
    );
}
