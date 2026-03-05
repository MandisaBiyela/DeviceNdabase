using DeviceDesk.Modules.Phase3.Services;
using DeviceDesk.Modules.Phase3.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DeviceDesk.Modules.Phase3.Controllers;

[ApiController]
[Route("api/dispatch/batches")]
[AllowAnonymous]
public class DispatchBatchController : ControllerBase
{
    private readonly DispatchBatchService _batchService;
    private readonly ILogger<DispatchBatchController> _logger;

    public DispatchBatchController(
        ILogger<DispatchBatchController> logger,
        DispatchBatchService batchService)
    {
        _batchService = batchService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";

    /// <summary>
    /// GET /api/dispatch/batches - Get all batches (optionally filtered by status)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBatches([FromQuery] BatchStatus? status = null)
    {
        try
        {
            var batches = await _batchService.GetBatchesAsync(status);
            return Ok(batches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting batches: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to get batches" });
        }
    }

    /// <summary>
    /// GET /api/dispatch/batches/incomplete - Get all incomplete batches (not Completed and not Cancelled)
    /// </summary>
    [HttpGet("incomplete")]
    public async Task<IActionResult> GetIncompleteBatches()
    {
        try
        {
            var batches = await _batchService.GetIncompleteBatchesAsync();
            return Ok(batches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting incomplete batches: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to get incomplete batches" });
        }
    }

    /// <summary>
    /// GET /api/dispatch/batches/{id} - Get batch details
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBatch(Guid id)
    {
        var batch = await _batchService.GetBatchAsync(id);
        if (batch == null)
            return NotFound(new { message = "Batch not found" });

        return Ok(batch);
    }

    /// <summary>
    /// GET /api/dispatch/batches/queue - Get devices in dispatch queue
    /// </summary>
    [HttpGet("queue")]
    public async Task<IActionResult> GetDispatchQueue()
    {
        try
        {
            _logger.LogInformation("[DispatchQueue] GET /api/dispatch/batches/queue called");
            var devices = await _batchService.GetDispatchQueueAsync();
            _logger.LogInformation("[DispatchQueue] Returning {Count} devices to frontend", devices.Count);
            // Service method now returns empty list on error instead of throwing
            // This allows the frontend to display an empty queue gracefully
            return Ok(devices);
        }
        catch (Exception ex)
        {
            // This catch is now a safety net - the service should handle errors internally
            _logger.LogError(ex, "Unexpected error in GetDispatchQueue controller: {Error}", ex.Message);
            // Return empty array instead of 500 to prevent frontend crashes
            return Ok(new List<object>());
        }
    }

    /// <summary>
    /// GET /api/dispatch/batches/debug/phase2-devices - Diagnostic endpoint to see all Phase 2 devices and why they're filtered
    /// </summary>
    [HttpGet("debug/phase2-devices")]
    public async Task<IActionResult> DebugPhase2Devices()
    {
        var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
        var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
        
        // Get all devices with QA status
        var allDevices = await phase2Db.Devices
            .Select(d => new
            {
                d.Id,
                d.Serial,
                d.Stage,
                d.QaPassed,
                d.DispatchStatus,
                d.UpdatedAt,
                d.CreatedAt
            })
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync();

        // Get devices in batches
        var devicesInBatches = await phase3Db.BatchDevices
            .Include(bd => bd.Batch)
            .Where(bd => bd.Batch.Status != BatchStatus.Cancelled)
            .Select(bd => new { bd.DeviceId, BatchId = bd.Batch.BatchId, BatchStatus = bd.Batch.Status })
            .ToListAsync();

        var deviceIdsInBatches = devicesInBatches.Select(b => b.DeviceId).ToList();
        
        // Analyze filtering
        var qaPassedDevices = allDevices.Where(d => d.QaPassed.HasValue && d.QaPassed.Value == true).ToList();
        var excludedByStage = qaPassedDevices.Where(d => 
            d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Disposal ||
            d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Quarantine ||
            d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.WarrantyReturn).ToList();
        var excludedByBatch = qaPassedDevices.Where(d => deviceIdsInBatches.Contains(d.Id)).ToList();
        var excludedByDelivered = qaPassedDevices.Where(d => d.DispatchStatus == DispatchDeviceState.Delivered).ToList();
        
        var shouldBeInQueue = qaPassedDevices.Where(d =>
            d.Stage != DeviceDesk.Modules.Phase2.Models.Phase2Stage.Disposal &&
            d.Stage != DeviceDesk.Modules.Phase2.Models.Phase2Stage.Quarantine &&
            d.Stage != DeviceDesk.Modules.Phase2.Models.Phase2Stage.WarrantyReturn &&
            !deviceIdsInBatches.Contains(d.Id) &&
            (d.DispatchStatus == null || d.DispatchStatus != DispatchDeviceState.Delivered)).ToList();
        
        return Ok(new
        {
            summary = new
            {
                totalDevices = allDevices.Count,
                devicesWithQaPassed = qaPassedDevices.Count,
                devicesInBatchesCount = devicesInBatches.Count,
                shouldBeInQueue = shouldBeInQueue.Count
            },
            filtering = new
            {
                excludedByStage = excludedByStage.Select(d => new { d.Id, d.Serial, d.Stage }),
                excludedByBatch = excludedByBatch.Select(d => new { d.Id, d.Serial, BatchId = devicesInBatches.First(b => b.DeviceId == d.Id).BatchId }),
                excludedByDelivered = excludedByDelivered.Select(d => new { d.Id, d.Serial, d.DispatchStatus })
            },
            shouldBeInQueue = shouldBeInQueue.Select(d => new { d.Id, d.Serial, d.Stage, d.QaPassed, d.DispatchStatus }),
            allDevices = allDevices.Take(50).ToList(), // Limit to prevent huge responses
            devicesInBatches = devicesInBatches
        });
    }

    /// <summary>
    /// GET /api/dispatch/batches/debug/device/{serial} - Check why a specific device isn't in queue
    /// </summary>
    [HttpGet("debug/device/{serial}")]
    public async Task<IActionResult> DebugDevice(string serial)
    {
        var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
        var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
        
        var device = await phase2Db.Devices.FirstOrDefaultAsync(d => d.Serial == serial);
        if (device == null)
            return Ok(new { found = false, message = $"Device with serial '{serial}' not found in Phase2Devices" });
        
        var inBatch = await phase3Db.BatchDevices
            .Include(bd => bd.Batch)
            .Where(bd => bd.DeviceId == device.Id && bd.Batch.Status != BatchStatus.Cancelled)
            .Select(bd => new { bd.Batch.BatchId, bd.Batch.Status })
            .FirstOrDefaultAsync();
        
        return Ok(new
        {
            found = true,
            device = new
            {
                device.Id,
                device.Serial,
                device.Stage,
                device.QaPassed,
                device.DispatchStatus,
                device.ScannedOutAt,
                device.ScannedOutByUserId,
                device.SchoolName
            },
            inBatch = inBatch != null ? new { inBatch.BatchId, inBatch.Status } : null,
            wouldAppearInQueue = device.ScannedOutAt != null && 
                                inBatch == null && 
                                (device.DispatchStatus == null || device.DispatchStatus != DispatchDeviceState.Delivered),
            exclusionReasons = new
            {
                noScannedOutAt = device.ScannedOutAt == null,
                alreadyInBatch = inBatch != null,
                isDelivered = device.DispatchStatus == DispatchDeviceState.Delivered
            }
        });
    }

    /// <summary>
    /// GET /api/dispatch/batches/debug/device-counts - Simple diagnostic endpoint to check device counts
    /// </summary>
    [HttpGet("debug/device-counts")]
    public async Task<IActionResult> GetDeviceCounts()
    {
        try
        {
            var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
            var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
            
            // Get basic counts
            var totalDevices = await phase2Db.Devices.CountAsync();
            var qaPassedCount = await phase2Db.Devices.CountAsync(d => d.QaPassed.HasValue && d.QaPassed.Value == true);
            var inDispatchStage = await phase2Db.Devices.CountAsync(d => d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Dispatch || d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.AwaitingDispatch);
            var inDisposalStage = await phase2Db.Devices.CountAsync(d => d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Disposal);
            var inQuarantineStage = await phase2Db.Devices.CountAsync(d => d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.Quarantine);
            var deliveredCount = await phase2Db.Devices.CountAsync(d => d.DispatchStatus == DispatchDeviceState.Delivered);
            
            var devicesInBatches = await phase3Db.BatchDevices
                .Include(bd => bd.Batch)
                .Where(bd => bd.Batch.Status != DeviceDesk.Modules.Phase3.Models.BatchStatus.Cancelled && 
                            bd.Batch.Status != DeviceDesk.Modules.Phase3.Models.BatchStatus.Draft)
                .CountAsync();
            
            // Get sample devices with QA passed
            var sampleQaPassed = await phase2Db.Devices
                .Where(d => d.QaPassed.HasValue && d.QaPassed.Value == true)
                .Take(5)
                .Select(d => new { d.Id, d.Serial, d.Stage, d.QaPassed, d.DispatchStatus })
                .ToListAsync();
            
            return Ok(new
            {
                summary = new
                {
                    totalPhase2Devices = totalDevices,
                    devicesWithQaPassed = qaPassedCount,
                    devicesInDispatchStages = inDispatchStage,
                    devicesInDisposal = inDisposalStage,
                    devicesInQuarantine = inQuarantineStage,
                    devicesDelivered = deliveredCount,
                    devicesInNonDraftBatches = devicesInBatches
                },
                sampleQaPassedDevices = sampleQaPassed,
                explanation = new
                {
                    message = "Devices appear in dispatch queue if they have QaPassed=true OR are in Dispatch/AwaitingDispatch stage",
                    excludes = new[]
                    {
                        "Devices in Disposal, Quarantine, or WarrantyReturn stages",
                        "Devices already in non-draft batches",
                        "Devices with DispatchStatus = Delivered"
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting device counts: {Error}", ex.Message);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// POST /api/dispatch/batches - Create a new draft batch with auto-extracted school info from devices
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBatch([FromBody] CreateBatchRequest request)
    {
        if (request.DeviceIds == null || !request.DeviceIds.Any())
            return BadRequest(new { message = "At least one device ID is required" });

        // Extract school information from selected devices
        DispatchBatch? batch;
        try
        {
            batch = await _batchService.CreateDraftBatchWithDevicesAsync(
                request.DeviceIds,
                request.StockType,
                request.SourceReference,
                GetUserId()
            );
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating batch: {Error}", ex.Message);
            return StatusCode(500, new { error = "Failed to create batch" });
        }

        if (batch == null)
            return BadRequest(new { message = "Could not extract school information from selected devices. Ensure all devices belong to the same school." });

        // Return batch with explicit batchId property and all school information
        return CreatedAtAction(nameof(GetBatch), new { id = batch.BatchId }, new
        {
            batchId = batch.BatchId,
            status = batch.Status,
            schoolName = batch.SchoolName,
            district = batch.District,
            emisCode = batch.EmisCode,
            stockType = batch.StockType,
            sourceReference = batch.SourceReference,
            createdAt = batch.CreatedAt
        });
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/devices - Add devices to batch
    /// </summary>
    [HttpPost("{id:guid}/devices")]
    public async Task<IActionResult> AddDevices(Guid id, [FromBody] AddDevicesRequest request)
    {
        if (request.DeviceIds == null || !request.DeviceIds.Any())
            return BadRequest(new { message = "Device IDs are required" });

        var (success, message, added) = await _batchService.AddDevicesToBatchAsync(
            id,
            request.DeviceIds,
            GetUserId()
        );

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message, added });
    }

    /// <summary>
    /// Get devices that match the batch's school + GRV and are eligible to be added.
    /// </summary>
    [HttpGet("{id:guid}/matching-devices")]
    public async Task<IActionResult> GetMatchingDevices(Guid id)
    {
        try
        {
            var result = await _batchService.GetMatchingDevicesForBatchAsync(id);

            return Ok(new
            {
                totalInGrv = result.TotalInGrv,
                eligibleCount = result.EligibleCount,
                inIctCount = result.InIctCount,
                devices = result.EligibleDevices
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting matching devices for batch {BatchId}", id);
            return StatusCode(500, new { message = "Error getting matching devices" });
        }
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/cancel - Cancel a batch and return devices to queue
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelBatch(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var result = await _batchService.CancelBatchAsync(id, userId);

            if (!result.success)
                return BadRequest(new { message = result.message });

            return Ok(new { message = result.message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling batch {BatchId}", id);
            return StatusCode(500, new { message = "Error cancelling batch" });
        }
    }

    /// <summary>
    /// DELETE /api/dispatch/batches/{id}/devices/{deviceId} - Remove device from batch
    /// </summary>
    [HttpDelete("{id:guid}/devices/{deviceId:int}")]
    public async Task<IActionResult> RemoveDevice(Guid id, int deviceId)
    {
        var (success, message) = await _batchService.RemoveDeviceFromBatchAsync(
            id,
            deviceId,
            GetUserId()
        );

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    /// <summary>
    /// PUT /api/dispatch/batches/{id}/details - Update batch details
    /// </summary>
    [HttpPut("{id:guid}/details")]
    public async Task<IActionResult> UpdateDetails(Guid id, [FromBody] UpdateBatchDetailsRequest request)
    {
        var (success, message) = await _batchService.UpdateBatchDetailsAsync(
            id,
            request.District,
            request.EmisCode,
            request.TripReference,
            request.DriverName,
            request.DriverUserId,
            request.VehicleReg
        );

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/lock - Lock batch and generate POD
    /// </summary>
    [HttpPost("{id:guid}/lock")]
    public async Task<IActionResult> LockBatch(Guid id)
    {
        try
        {
            var (success, message) = await _batchService.LockBatchAsync(id, GetUserId());

            if (!success)
                return BadRequest(new { message });

            return Ok(new { message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking batch {BatchId}", id);
            return StatusCode(500, new { message = "Error locking batch: " + ex.Message });
        }
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/audit - Perform loading audit scan
    /// </summary>
    [HttpPost("{id:guid}/audit")]
    public async Task<IActionResult> PerformAudit(Guid id, [FromBody] PerformAuditRequest request)
    {
        if (request.ScannedSerials == null || !request.ScannedSerials.Any())
            return BadRequest(new { message = "Scanned serials are required" });

        var (success, message, auditResult) = await _batchService.PerformLoadingAuditAsync(
            id,
            request.ScannedSerials,
            GetUserId()
        );

        if (!success)
            return BadRequest(new { message });

        return Ok(new
        {
            message,
            audit = auditResult
        });
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/enroute - Mark batch as en route
    /// </summary>
    [HttpPost("{id:guid}/enroute")]
    public async Task<IActionResult> MarkEnRoute(Guid id)
    {
        var (success, message) = await _batchService.MarkEnRouteAsync(id, GetUserId());

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/delivered - Mark batch as delivered/arrived
    /// </summary>
    [HttpPost("{id:guid}/delivered")]
    public async Task<IActionResult> MarkDelivered(Guid id)
    {
        var (success, message) = await _batchService.MarkDeliveredAsync(id, GetUserId());

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    /// <summary>
    /// POST /api/dispatch/batches/{id}/debrief - Complete arrival debrief
    /// </summary>
    [HttpPost("{id:guid}/debrief")]
    public async Task<IActionResult> CompleteDebrief(Guid id, [FromBody] CompleteDebriefRequest request)
    {
        var (success, message) = await _batchService.CompleteDebriefAsync(
            id,
            request.SchoolSigned,
            request.SchoolSignatoryName,
            request.DebriefNotes,
            request.HasExceptions,
            request.ExceptionNotes,
            GetUserId()
        );

        if (!success)
            return BadRequest(new { message });

        return Ok(new { message });
    }

    /// <summary>
    /// GET /api/dispatch/batches/{id}/pod-download - Download POD document (generates if needed)
    /// </summary>
    [HttpGet("{id:guid}/pod-download")]
    public async Task<IActionResult> DownloadPOD(Guid id)
    {
        try
        {
            // Get batch directly from database for reliable property access
            var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
            var batchEntity = await phase3Db.DispatchBatches.FindAsync(id);
            
            if (batchEntity == null)
                return NotFound(new { message = "Batch not found" });

            long? podDocumentId = batchEntity.PODDocumentId;
            string? podNumber = batchEntity.PODNumber;
            
            _logger.LogInformation("DownloadPOD: batchId={BatchId}, podDocumentId={DocId}, podNumber={POD}", 
                id, podDocumentId?.ToString() ?? "null", podNumber ?? "null");
            
            // If document doesn't exist, generate it
            if (podDocumentId == null && !string.IsNullOrEmpty(podNumber))
            {
                _logger.LogInformation("POD document not found, generating...");
                podDocumentId = await GenerateAndSavePODDocument(id, podNumber);
                _logger.LogInformation("Generated POD document with ID: {DocId}", podDocumentId?.ToString() ?? "null");
            }
            
            if (podDocumentId == null)
                return NotFound(new { message = "POD document not found and could not be generated." });

            return await DownloadDocumentById(podDocumentId.Value, "POD");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading POD for batch {BatchId}: {Error}", id, ex.Message);
            return StatusCode(500, new { message = $"Failed to download POD: {ex.Message}" });
        }
    }

    /// <summary>
    /// GET /api/dispatch/batches/{id}/delivery-note-download - Download Delivery Note document (generates if needed)
    /// </summary>
    [HttpGet("{id:guid}/delivery-note-download")]
    public async Task<IActionResult> DownloadDeliveryNote(Guid id)
    {
        try
        {
            // Get batch directly from database for reliable property access
            var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
            var batchEntity = await phase3Db.DispatchBatches.FindAsync(id);
            
            if (batchEntity == null)
                return NotFound(new { message = "Batch not found" });

            long? dnDocumentId = batchEntity.DeliveryNoteDocumentId;
            string? podNumber = batchEntity.PODNumber;
            
            _logger.LogInformation("DownloadDeliveryNote: batchId={BatchId}, dnDocumentId={DocId}, podNumber={POD}", 
                id, dnDocumentId?.ToString() ?? "null", podNumber ?? "null");
            
            // If document doesn't exist, try to find it by POD number first (in case it was generated but not linked)
            if (dnDocumentId == null && !string.IsNullOrEmpty(podNumber))
            {
                try
                {
                    _logger.LogInformation("Delivery Note document ID not found in batch, searching by POD number...");
                    var phase0Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Infrastructure.Data.DeviceDeskDbContext>();
                    var existingDN = await phase0Db.Documents
                        .Where(d => d.DocType == "DeliveryNote" && d.FileName == $"DeliveryNote-{podNumber}.pdf")
                        .FirstOrDefaultAsync();
                    
                    if (existingDN != null)
                    {
                        _logger.LogInformation("Found existing Delivery Note document with ID {DocId} for POD {POD}", existingDN.DocumentId, podNumber);
                        dnDocumentId = existingDN.DocumentId;
                        
                        // Link it to the batch
                        var phase3DbLink = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
                        var batchEntityLink = await phase3DbLink.DispatchBatches.FindAsync(id);
                        if (batchEntityLink != null)
                        {
                            batchEntityLink.DeliveryNoteDocumentId = existingDN.DocumentId;
                            await phase3DbLink.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Delivery Note document not found in database, generating...");
                        dnDocumentId = await GenerateAndSaveDeliveryNoteDocument(id, podNumber);
                        _logger.LogInformation("Generated Delivery Note document with ID: {DocId}", dnDocumentId?.ToString() ?? "null");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while searching for or generating Delivery Note document: {Error}", ex.Message);
                    // Fall through to generation attempt
                    dnDocumentId = await GenerateAndSaveDeliveryNoteDocument(id, podNumber);
                }
            }
            
            if (dnDocumentId == null)
                return NotFound(new { message = "Delivery Note document not found and could not be generated." });

            return await DownloadDocumentById(dnDocumentId.Value, "DeliveryNote");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading Delivery Note for batch {BatchId}", id);
            return StatusCode(500, new { message = $"Failed to download Delivery Note: {ex.Message}" });
        }
    }

    private async Task<long?> GenerateAndSavePODDocument(Guid batchId, string podNumber)
    {
        try
        {
            // Get batch directly from database to avoid dynamic object issues
            var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
            var batchEntity = await phase3Db.DispatchBatches
                .Include(b => b.Devices)
                .FirstOrDefaultAsync(b => b.BatchId == batchId);
            
            if (batchEntity == null)
            {
                _logger.LogWarning("Batch {BatchId} not found for POD generation", batchId);
                return null;
            }

            // Get device serials from Phase2
            var deviceIds = batchEntity.Devices.Select(bd => bd.DeviceId).ToList();
            var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
            var devices = await phase2Db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => d.Serial)
                .ToListAsync();

            if (!devices.Any())
            {
                _logger.LogWarning("No device serials found for batch {BatchId}", batchId);
                return null;
            }

            _logger.LogInformation("Generating POD document for batch {BatchId} with {Count} devices", batchId, devices.Count);

            var docService = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Services.DispatchDocumentService>();
            var (podDocId, dnDocId, podFileName, dnFileName) = await docService.CreatePodAndDeliveryNoteAsync(
                podNumber, 
                batchEntity.SchoolName, 
                batchEntity.StockType, 
                batchEntity.SourceReference ?? "", 
                devices);

            // Update batch with BOTH document IDs (they're generated together)
            batchEntity.PODDocumentId = podDocId;
            batchEntity.DeliveryNoteDocumentId = dnDocId;
            await phase3Db.SaveChangesAsync();
            _logger.LogInformation("Saved POD document ID {PodDocId} and Delivery Note document ID {DnDocId} to batch {BatchId}", 
                podDocId, dnDocId, batchId);

            return podDocId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating POD document for batch {BatchId}: {Error}", batchId, ex.Message);
            throw;
        }
    }

    private async Task<long?> GenerateAndSaveDeliveryNoteDocument(Guid batchId, string podNumber)
    {
        try
        {
            // Get batch directly from database to avoid dynamic object issues
            var phase3Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Data.Phase3DbContext>();
            var batchEntity = await phase3Db.DispatchBatches
                .Include(b => b.Devices)
                .FirstOrDefaultAsync(b => b.BatchId == batchId);
            
            if (batchEntity == null)
            {
                _logger.LogWarning("Batch {BatchId} not found for Delivery Note generation", batchId);
                return null;
            }

            // Get device serials from Phase2
            var deviceIds = batchEntity.Devices.Select(bd => bd.DeviceId).ToList();
            var phase2Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase2.Data.Phase2DbContext>();
            var devices = await phase2Db.Devices
                .Where(d => deviceIds.Contains(d.Id))
                .Select(d => d.Serial)
                .ToListAsync();

            if (!devices.Any())
            {
                _logger.LogWarning("No device serials found for batch {BatchId}", batchId);
                return null;
            }

            _logger.LogInformation("Generating Delivery Note document for batch {BatchId} with {Count} devices", batchId, devices.Count);

            // Check if documents already exist from POD generation
            if (batchEntity.PODDocumentId.HasValue && batchEntity.DeliveryNoteDocumentId.HasValue)
            {
                _logger.LogInformation("Documents already exist for batch {BatchId}, using existing Delivery Note document ID {DocId}", 
                    batchId, batchEntity.DeliveryNoteDocumentId.Value);
                return batchEntity.DeliveryNoteDocumentId.Value;
            }

            var docService = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Modules.Phase3.Services.DispatchDocumentService>();
            var (podDocId, dnDocId, podFileName, dnFileName) = await docService.CreatePodAndDeliveryNoteAsync(
                podNumber, 
                batchEntity.SchoolName, 
                batchEntity.StockType, 
                batchEntity.SourceReference ?? "", 
                devices);

            // Update batch with BOTH document IDs (they're generated together)
            batchEntity.PODDocumentId = podDocId;
            batchEntity.DeliveryNoteDocumentId = dnDocId;
            await phase3Db.SaveChangesAsync();
            _logger.LogInformation("Saved POD document ID {PodDocId} and Delivery Note document ID {DnDocId} to batch {BatchId}", 
                podDocId, dnDocId, batchId);

            return dnDocId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Delivery Note document for batch {BatchId}: {Error}", batchId, ex.Message);
            throw;
        }
    }

    private async Task<IActionResult> DownloadDocumentById(long documentId, string docType)
    {
        try
        {
            var phase0Db = HttpContext.RequestServices.GetRequiredService<DeviceDesk.Infrastructure.Data.DeviceDeskDbContext>();
            var document = await phase0Db.Documents.FindAsync(documentId);
            
            if (document == null)
            {
                _logger.LogWarning("{DocType} document with ID {DocId} not found in database", docType, documentId);
                return NotFound(new { message = $"{docType} document not found" });
            }

            if (document.FileData == null || document.FileData.Length == 0)
            {
                _logger.LogWarning("{DocType} document with ID {DocId} has no file data", docType, documentId);
                return NotFound(new { message = $"{docType} document is empty" });
            }

            _logger.LogInformation("Serving {DocType} document with ID {DocId}, size {Size} bytes", docType, documentId, document.FileData.Length);
            return File(document.FileData, document.ContentType ?? "application/pdf", document.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {DocType} document with ID {DocId}: {Error}", docType, documentId, ex.Message);
            return StatusCode(500, new { message = $"Failed to retrieve {docType} document: {ex.Message}" });
        }
    }
}

// DTOs
public record CreateBatchRequest(List<int> DeviceIds, string StockType, string? SourceReference);
public record AddDevicesRequest(List<int> DeviceIds);
public record UpdateBatchDetailsRequest(
    string? District,
    string? EmisCode,
    string? TripReference,
    string? DriverName,
    string? DriverUserId,
    string? VehicleReg
);
public record PerformAuditRequest(List<string> ScannedSerials);
public record CompleteDebriefRequest(
    bool SchoolSigned,
    string? SchoolSignatoryName,
    string? DebriefNotes,
    bool HasExceptions,
    string? ExceptionNotes
);
