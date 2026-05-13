using DeviceDesk.Modules.Phase1.Services.DocumentIngest;
using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase1.Controllers;

[ApiController]
[Route("api/phase1/document-ingest")]
public class DocumentIngestController : ControllerBase
{
    private readonly ReceivingDocumentIngestService _ingest;
    private readonly Phase1DbContext _db;
    private readonly ILogger<DocumentIngestController> _logger;

    public DocumentIngestController(
        ReceivingDocumentIngestService ingest,
        Phase1DbContext db,
        ILogger<DocumentIngestController> logger)
    {
        _ingest = ingest;
        _db = db;
        _logger = logger;
    }

    /// <summary>Upload a document, extract text, classify with Claude (or heuristic), and attempt DB matching.</summary>
    [HttpPost("upload")]
    [AllowAnonymous]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded." });

        try
        {
            var result = await _ingest.UploadAndAnalyzeAsync(file, User, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingest upload failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> Confirm([FromBody] DocumentIngestConfirmRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IngestSessionId))
            return BadRequest(new { error = "ingestSessionId is required." });

        try
        {
            var result = await _ingest.ConfirmAsync(request, User, ct);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingest confirm failed.");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>Classifier keys from document_type_registry (for manual fallback UI).</summary>
    [HttpGet("document-type-keys")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDocumentTypeKeys(CancellationToken ct)
    {
        var keys = await _db.DocumentTypeRegistries.AsNoTracking()
            .OrderBy(r => r.DocumentTypeKey)
            .Select(r => new { r.DocumentTypeKey, r.DisplayName, r.IsSystemType, r.TableName })
            .ToListAsync(ct);
        return Ok(keys);
    }
}
