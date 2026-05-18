using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase1.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/reconciliation")]
    [Authorize]
    public class ReconciliationController : ControllerBase
    {
        private readonly ReconciliationService _reconciliation;
        private readonly GRVService _grv;
        private readonly ILogger<ReconciliationController> _logger;

        public ReconciliationController(
            ReconciliationService reconciliation, 
            GRVService grv,
            ILogger<ReconciliationController> logger)
        {
            _reconciliation = reconciliation;
            _grv = grv;
            _logger = logger;
        }

        [HttpPost("start-scanning")]
        public async Task<ActionResult<ReconciliationStatusDto>> StartScanning(
            [FromBody] StartScanningRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _reconciliation.StartScanningAsync(request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("complete-scanning")]
        public async Task<ActionResult<ReconciliationStatusDto>> CompleteScanning(
            [FromBody] CompleteScanningRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _reconciliation.CompleteScanningAsync(request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("submit-count")]
        public async Task<ActionResult<ReconciliationStatusDto>> SubmitCount(
            [FromBody] SubmitCountRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _reconciliation.SubmitCountAsync(request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("resolve-variance")]
        public async Task<ActionResult<ReconciliationStatusDto>> ResolveVariance(
            [FromBody] ResolveVarianceRequest request,
            CancellationToken ct)
        {
            try
            {
                var result = await _reconciliation.ResolveVarianceAsync(request, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("status/{batchId}")]
        public async Task<ActionResult<ReconciliationStatusDto>> GetStatus(
            Guid batchId,
            CancellationToken ct)
        {
            try
            {
                var result = await _reconciliation.GetReconciliationStatusAsync(batchId, ct);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("generate-grv/{batchId}")]
        public async Task<ActionResult<GRVDto>> GenerateGRV(
            Guid batchId,
            CancellationToken ct)
        {
            try
            {
                if (batchId == Guid.Empty)
                    return BadRequest(new { error = "Invalid batch id." });
                var grv = await _grv.GenerateGRVAsync(batchId, ct);
                return Ok(grv);
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    return NotFound(new { error = ex.Message });
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating GRV for batch {BatchId}", batchId);
                return StatusCode(500, new { error = $"Failed to generate GRV: {ex.Message}" });
            }
        }

        [HttpGet("grv/{grvId}/pdf")]
        public async Task<IActionResult> DownloadGRVPdf(
            Guid grvId,
            CancellationToken ct)
        {
            try
            {
                var pdfData = await _grv.GetGRVPdfAsync(grvId, ct);
                if (pdfData == null)
                    return NotFound(new { error = "GRV not found." });

                return File(pdfData, "application/pdf", $"GRV_{grvId}.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
