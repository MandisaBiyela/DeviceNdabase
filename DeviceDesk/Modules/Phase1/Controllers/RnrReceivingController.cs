using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase1.Services;
using DeviceDesk.Modules.Phase0.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.ComponentModel.DataAnnotations;
using ClosedXML.Excel;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/rnr")]
    public class RnrReceivingController : ControllerBase
    {
        private readonly Phase1DbContext _db;
        private readonly DeviceDeskDbContext _coreDb;
        private readonly RnrBatchService _rnrBatchService;
        private readonly RnrBlindCopyService _rnrBlindCopyService;
        private readonly RnrGrvService _rnrGrvService;

        public RnrReceivingController(
            Phase1DbContext db,
            DeviceDeskDbContext coreDb,
            RnrBatchService rnrBatchService,
            RnrBlindCopyService rnrBlindCopyService,
            RnrGrvService rnrGrvService)
        {
            _db = db;
            _coreDb = coreDb;
            _rnrBatchService = rnrBatchService;
            _rnrBlindCopyService = rnrBlindCopyService;
            _rnrGrvService = rnrGrvService;
        }

        // DTO for RnR header information
        public sealed class RnrBatchHeaderDto
        {
            public Guid BatchId { get; set; }
            public string? SlipNumber { get; set; }
            public string? SchoolName { get; set; }
            public string? EmisCode { get; set; }
            public DateTimeOffset? CollectionDate { get; set; }
            public string? CollectedBy { get; set; }
            public int ExpectedCount { get; set; }
            public int ScannedCount { get; set; }
            public int MissingCount { get; set; }
        }

        /// <summary>
        /// Health check endpoint to verify RnR controller is working
        /// </summary>
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { 
                status = "Phase1 RnR service running", 
                timestamp = DateTimeOffset.UtcNow,
                dbContext = "Phase1DbContext"
            });
        }

        /// <summary>
        /// Get available R&R collection slips from Phase 0 for dropdown
        /// This enables Phase 0 → Phase 1 integration for R&R workflow
        /// </summary>
        [HttpGet("collection-slips")]
        public async Task<IActionResult> GetAvailableCollectionSlips(CancellationToken ct)
        {
            try
            {
                // Get R&R batches from Phase 0 that are pending scan
                var batches = await _rnrBatchService.GetBatchesAsync(
                    Infrastructure.Data.RnrBatchStatus.PendingScan, 
                    ct);
                
                // Transform to dropdown format
                var slips = batches.Select(b => new
                {
                    batchId = b.BatchId,
                    collectionSlipNumber = b.CollectionSlipNumber,
                    batchNumber = b.BatchNumber,
                    schoolName = b.SchoolName,
                    totalQuantityExpected = b.TotalQuantityExpected,
                    createdAt = b.CreatedAt,
                    status = b.Status.ToString()
                }).ToList();

                return Ok(slips);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R&R Collection Slips] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to fetch R&R collection slips from Phase 0", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate blind copy PDF for R&R batch (PAN copy with hidden quantities)
        /// </summary>
        [HttpGet("batches/{batchId:guid}/blind-copy")]
        public async Task<IActionResult> GenerateBlindCopy(Guid batchId, CancellationToken ct)
        {
            try
            {
                var pdfBytes = await _rnrBlindCopyService.GenerateRnrBlindCopyPdfAsync(batchId, ct);
                
                return File(pdfBytes, "application/pdf", $"RNR_BlindCopy_{batchId}.pdf");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R&R Blind Copy] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to generate R&R blind copy PDF", details = ex.Message });
            }
        }

        /// <summary>
        /// Record a scanned device for R&R batch (Phase 0 integration)
        /// Simple quantity-based scanning - increments counts per item type
        /// </summary>
        [HttpPost("batches/{batchId:guid}/scan-item")]
        public async Task<IActionResult> ScanRnrItem(
            Guid batchId, 
            [FromBody] RnrScanItemRequest request, 
            CancellationToken ct)
        {
            try
            {
                // Increment the scan count in Phase 0 database
                var updatedBatch = await _rnrBatchService.IncrementItemScanAsync(
                    batchId,
                    request.Brand,
                    request.Model,
                    request.DeviceType,
                    ct);

                if (updatedBatch == null)
                {
                    return BadRequest(new { 
                        error = "Failed to increment scan - item not found or already complete", 
                        code = "SCAN_FAILED" 
                    });
                }

                // Find the updated item
                var matchingItem = updatedBatch.Items.FirstOrDefault(i =>
                    string.Equals(i.Brand, request.Brand, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.Model, request.Model, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(i.DeviceType, request.DeviceType, StringComparison.OrdinalIgnoreCase));

                var result = new
                {
                    success = true,
                    item = new
                    {
                        brand = matchingItem?.Brand,
                        model = matchingItem?.Model,
                        deviceType = matchingItem?.DeviceType,
                        quantityExpected = matchingItem?.QuantityExpected ?? 0,
                        quantityScanned = matchingItem?.QuantityScanned ?? 0,
                        remaining = (matchingItem?.QuantityExpected ?? 0) - (matchingItem?.QuantityScanned ?? 0)
                    },
                    batch = new
                    {
                        batchId = updatedBatch.BatchId,
                        totalExpected = updatedBatch.TotalQuantityExpected,
                        totalScanned = updatedBatch.TotalQuantityScanned,
                        progress = (updatedBatch.TotalQuantityScanned * 100.0 / updatedBatch.TotalQuantityExpected),
                        status = updatedBatch.Status.ToString()
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R&R Scan] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to record scan", details = ex.Message });
            }
        }

        /// <summary>
        /// Complete R&R batch scanning and trigger verification
        /// </summary>
        [HttpPost("batches/{batchId:guid}/complete-scan")]
        public async Task<IActionResult> CompleteScan(Guid batchId, CancellationToken ct)
        {
            try
            {
                // Get current batch status
                var batch = await _rnrBatchService.GetBatchDetailsAsync(batchId, ct);
                if (batch == null)
                {
                    return NotFound(new { error = "R&R batch not found" });
                }

                // Check if counts match
                bool countsMatch = batch.TotalQuantityScanned == batch.TotalQuantityExpected;

                // Set appropriate status
                var newStatus = countsMatch 
                    ? Infrastructure.Data.RnrBatchStatus.Verified 
                    : Infrastructure.Data.RnrBatchStatus.VarianceDetected;

                // Update batch status in database
                await _rnrBatchService.UpdateBatchStatusAsync(batchId, newStatus, null, null, ct);

                var result = new
                {
                    success = true,
                    verification = new
                    {
                        countsMatch = countsMatch,
                        expectedTotal = batch.TotalQuantityExpected,
                        scannedTotal = batch.TotalQuantityScanned,
                        variance = batch.TotalQuantityScanned - batch.TotalQuantityExpected,
                        status = newStatus.ToString()
                    },
                    items = batch.Items.Select(i => new
                    {
                        brand = i.Brand,
                        model = i.Model,
                        deviceType = i.DeviceType,
                        expected = i.QuantityExpected,
                        scanned = i.QuantityScanned,
                        match = i.QuantityScanned == i.QuantityExpected
                    }).ToList(),
                    nextAction = countsMatch ? "GENERATE_GRV" : "RECOUNT_OR_ESCALATE"
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R&R Complete Scan] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to complete scanning", details = ex.Message });
            }
        }

        /// <summary>
        /// Generate GRV PDF for verified R&R batch
        /// </summary>
        [HttpPost("batches/{batchId:guid}/generate-grv")]
        public async Task<IActionResult> GenerateGrv(Guid batchId, CancellationToken ct)
        {
            try
            {
                // Get batch details
                var batch = await _rnrBatchService.GetBatchDetailsAsync(batchId, ct);
                if (batch == null)
                {
                    return NotFound(new { error = "R&R batch not found" });
                }

                // Verify batch is in Verified status
                if (batch.Status != Infrastructure.Data.RnrBatchStatus.Verified)
                {
                    return BadRequest(new 
                    { 
                        error = "GRV can only be generated for verified batches",
                        currentStatus = batch.Status.ToString(),
                        message = "Complete scanning and verification before generating GRV"
                    });
                }

                // Generate GRV number (format: GRV-RNR-YYYYMMDD-XXXX)
                var grvNumber = $"GRV-RNR-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}";

                // Update batch status to GRVIssued with GRV number
                await _rnrBatchService.UpdateBatchStatusAsync(
                    batchId, 
                    Infrastructure.Data.RnrBatchStatus.GRVIssued, 
                    grvNumber, 
                    User.Identity?.Name ?? "System", 
                    ct);

                // Generate GRV PDF
                var pdfBytes = await _rnrGrvService.GenerateRnrGrvPdfAsync(batchId, grvNumber, ct);

                // Return PDF with appropriate filename
                var filename = $"GRV_{batch.BatchNumber}_{DateTime.Now:yyyyMMdd}.pdf";
                return File(pdfBytes, "application/pdf", filename);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[R&R GRV] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to generate GRV", details = ex.Message });
            }
        }

        public class RnrScanItemRequest
        {
            public string? Brand { get; set; }
            public string? Model { get; set; }
            public string? DeviceType { get; set; }
        }

        // --- Strict-scan helpers -----------------------------------------------------
        // Removed: Using centralized SerialNormalizer.Normalize instead

        /// <summary>Expected serials for a batch (normalized, unique).</summary>
        [HttpGet("batches/{batchId:guid}/expected-serials")]
        public async Task<IActionResult> GetExpectedSerials(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                // Get expected items from RnrExpectedItems table
                var serials = await _db.RnrExpectedItems
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.Serial)
                    .ToListAsync(ct);

                var normalizedSet = serials
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(SerialNormalizer.Normalize)
                    .Distinct()
                    .ToList();

                Console.WriteLine($"[GetExpectedSerials] Found {normalizedSet.Count} expected serials for batch {batchId}");
                return Ok(new { expected = normalizedSet });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetExpectedSerials] Error: {ex.Message}");
                return StatusCode(500, new { error = "Failed to load expected serials" });
            }
        }

        public enum RnrMode { Normal = 1, Emergency = 2 }

        public class ImportForm
        {
            [Required]
            public int SchoolId { get; set; }
            public string? SchoolName { get; set; } // optional, display only
            public string SlipNumber { get; set; } = default!;
            public DateTime SlipDate { get; set; }
            public RnrMode Mode { get; set; } = RnrMode.Normal;
            public string? Notes { get; set; }
            public string? ReceivedBy { get; set; }
        }


        /// <summary>
        /// Import RnR slip and create receiving batch (IDEMPOTENT - handles re-imports and duplicates)
        /// </summary>
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Import([FromForm] ImportForm meta, IFormFile file, CancellationToken ct = default)
        {
            try
            {
                Console.WriteLine($"[RnR Import] Starting idempotent import - File: {file?.FileName}, Slip: {meta?.SlipNumber}, SchoolId: {meta?.SchoolId}");
                
                if (meta == null)
                {
                    Console.WriteLine("[RnR Import] ERROR: meta is null");
                    return BadRequest(new { ok = false, error = "Import metadata is required." });
                }
                
                // Log received form data for debugging
                Console.WriteLine($"[RnR Import] Received form data - SchoolId: {meta.SchoolId}, SlipNumber: {meta.SlipNumber}, SlipDate: {meta.SlipDate}, Mode: {meta.Mode}");
                
                // Check ModelState for binding errors
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .Where(x => x.Value != null && x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value!.Errors.Select(e => e.ErrorMessage) })
                        .ToList();
                    Console.WriteLine($"[RnR Import] ModelState errors: {System.Text.Json.JsonSerializer.Serialize(errors)}");
                    return BadRequest(ModelState);
                }
                    
                if (file == null || file.Length == 0)
                    return BadRequest(new { ok = false, error = "File is required." });

                if (file.Length > 10 * 1024 * 1024)
                    return BadRequest(new { ok = false, error = "File size must be less than 10MB." });

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                var allowedExtensions = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".csv", ".xlsx" };
                
                if (!allowedExtensions.Contains(ext))
                    return BadRequest(new { ok = false, error = "Unsupported file type. Allowed: PDF, PNG, JPG, CSV, XLSX" });

                // 1) Parse file and extract raw serials, models, and device names
                var rawItems = new List<(string Serial, string? Model, string? DeviceName)>();
                
                if (ext == ".csv")
                {
                    rawItems = await ReadItemsFromCsv(file);
                }
                else if (ext == ".xlsx")
                {
                    rawItems = await ReadItemsFromXlsx(file, ct);
                }
                // PDF/Images don't contain serials - they're just reference files
                
                Console.WriteLine($"[RnR Import] Parsed {rawItems.Count} items from file: {file.FileName}");

                // 2) Normalize and detect in-file duplicates
                var normalized = rawItems
                    .Select(item => new { 
                        raw = item.Serial?.Trim(), 
                        norm = SerialNormalizer.Normalize(item.Serial),
                        model = item.Model?.Trim(),
                        deviceName = item.DeviceName?.Trim()
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.norm))
                    .ToList();

                // Debug: Log all raw items and their normalized versions
                Console.WriteLine($"[RnR Import] Total items parsed: {rawItems.Count}");
                Console.WriteLine($"[RnR Import] After normalization: {normalized.Count} items");
                if (normalized.Count <= 20) // Only log if small number for debugging
                {
                    foreach (var item in normalized)
                    {
                        Console.WriteLine($"[RnR Import] Raw: '{item.raw}' -> Normalized: '{item.norm}'");
                    }
                }

                // Group by normalized serial to find duplicates
                var dupInFile = normalized
                    .GroupBy(x => x.norm, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => new { 
                        NormalizedSerial = g.Key, 
                        Count = g.Count(),
                        RawSerials = g.Select(x => x.raw).Distinct().ToList() // Show original serials that normalized to the same value
                    })
                    .ToList();

                if (dupInFile.Any())
                {
                    var dupList = string.Join(", ", dupInFile.Select(d => $"{d.NormalizedSerial} (x{d.Count})"));
                    Console.WriteLine($"[RnR Import] Duplicates detected after normalization: {dupList}");
                    
                    // Log the raw serials for debugging
                    foreach (var dup in dupInFile)
                    {
                        Console.WriteLine($"[RnR Import] Normalized '{dup.NormalizedSerial}' comes from raw serials: {string.Join(", ", dup.RawSerials)}");
                    }
                    
                    var duplicateDetails = dupInFile.Select(d => new { 
                        Serial = d.NormalizedSerial, // Show normalized version
                        RawSerials = d.RawSerials, // Also include original serials for clarity
                        Count = d.Count 
                    }).ToList();
                    
                    var message = dupInFile.Count == 1
                        ? $"Duplicate serial found: '{string.Join("', '", dupInFile[0].RawSerials)}' all normalize to the same value. Please ensure each serial is unique."
                        : $"Found {dupInFile.Count} duplicate serial(s) in your file. Please ensure each serial is unique.";
                    
                    return BadRequest(new {
                        ok = false,
                        code = "DUPLICATE_SERIALS",
                        message = message,
                        duplicates = duplicateDetails
                    });
                }

                await using var tx = await _db.Database.BeginTransactionAsync(ct);

                // Validate required fields
                if (string.IsNullOrWhiteSpace(meta.SlipNumber))
                {
                    return BadRequest(new { success = false, message = "SlipNumber is required" });
                }

                // Validate SchoolId
                if (meta.SchoolId <= 0)
                {
                    ModelState.AddModelError("SchoolId", "Please select a school from the list.");
                    return BadRequest(ModelState);
                }

                // Look up school from dbo.Schools
                var school = await _coreDb.Schools
                    .FirstOrDefaultAsync(s => s.SchoolId == meta.SchoolId, ct);

                if (school == null)
                {
                    ModelState.AddModelError("SchoolId", "Selected school was not found.");
                    return BadRequest(ModelState);
                }

                // 3) Get or create CollectionSlip by SlipNumber (idempotent)
                var slip = await _db.CollectionSlips
                    .SingleOrDefaultAsync(s => s.SlipNumber == meta.SlipNumber, ct);

                if (slip is null)
                {
                    slip = new CollectionSlip {
                        CollectionSlipId = Guid.NewGuid(),
                        SlipNumber = meta.SlipNumber,
                        SchoolId = school.SchoolId, // SchoolId is long, no cast needed
                        SchoolName = school.Name,
                        EmisCode = school.EmisCode,
                        SourceType = meta.Mode == RnrMode.Normal ? ReceivingSourceType.RnrNormal : ReceivingSourceType.RnrEmergency,
                        CollectionDate = new DateTimeOffset(meta.SlipDate),
                        CollectedBy = meta.ReceivedBy ?? "Unknown",
                        Notes = meta.Notes,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    
                    // Handle PDF/Image reference
                    if (new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(ext))
                    {
                        var fileInfo = $"Collection slip file: {file.FileName} ({file.Length} bytes, uploaded {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm})";
                        slip.Notes = string.IsNullOrEmpty(slip.Notes) ? fileInfo : $"{slip.Notes}\n{fileInfo}";
                    }
                    
                    _db.CollectionSlips.Add(slip);
                    await _db.SaveChangesAsync(ct);
                    Console.WriteLine($"[RnR Import] Created new collection slip: {slip.SlipNumber}");
                }
                else
                {
                    // ALWAYS update existing slip with school info (new OR existing)
                    slip.SchoolId = school.SchoolId; // SchoolId is long, no cast needed
                    slip.SchoolName = school.Name ?? string.Empty;
                    slip.EmisCode = school.EmisCode ?? string.Empty;
                    slip.SlipNumber = meta.SlipNumber?.Trim() ?? string.Empty;
                    
                    // Handle PDF/Image reference for existing slip
                    if (new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(ext))
                    {
                        var fileInfo = $"Collection slip file: {file.FileName} ({file.Length} bytes, uploaded {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm})";
                        slip.Notes = string.IsNullOrEmpty(slip.Notes) ? fileInfo : $"{slip.Notes}\n{fileInfo}";
                    }
                    
                    await _db.SaveChangesAsync(ct);
                    Console.WriteLine($"[RnR Import] Updated existing collection slip: {slip.SlipNumber}");
                }

                // 4) Get or create ReceivingBatch for this slip
                var batch = await _db.ReceivingBatches
                    .SingleOrDefaultAsync(b => b.CollectionSlipId == slip.CollectionSlipId, ct);

                if (batch is null)
                {
                    batch = new ReceivingBatch {
                        ReceivingBatchId = Guid.NewGuid(),
                        CollectionSlipId = slip.CollectionSlipId,
                        SchoolId = slip.SchoolId,
                        SourceType = slip.SourceType,
                        Status = ReceivingBatchStatus.ScanningInProgress,
                        ReceivedBy = meta.ReceivedBy ?? "Unknown",
                        Notes = meta.Notes,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ExpectedCount = 0
                    };
                    _db.ReceivingBatches.Add(batch);
                    await _db.SaveChangesAsync(ct);
                    Console.WriteLine($"[RnR Import] Created new receiving batch: {batch.ReceivingBatchId}");
                }
                else
                {
                    // ALWAYS update existing batch with school info from slip
                    batch.SchoolId = slip.SchoolId;
                    batch.UpdatedAt = DateTimeOffset.UtcNow;
                    await _db.SaveChangesAsync(ct);
                    Console.WriteLine($"[RnR Import] Updated existing receiving batch: {batch.ReceivingBatchId}");
                }

                // 5) Insert only NEW expected items for this batch (avoid unique index violation)
                var batchId = batch.ReceivingBatchId;

                var existingSerials = await _db.RnrExpectedItems
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.Serial)
                    .ToListAsync(ct);

                var existingNormalized = existingSerials.Select(SerialNormalizer.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var newItemsData = normalized
                    .Where(x => !existingNormalized.Contains(x.norm))
                    .GroupBy(x => x.norm, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new { norm = g.Key, model = g.First().model, deviceName = g.First().deviceName })
                    .ToList();

                // If nothing new, just return summary
                if (!newItemsData.Any())
                {
                    var summary = new {
                        ok = true,
                        message = "Slip already imported. No new serials to add.",
                        batchId = batchId,
                        expectedCount = existingSerials.Count,
                        added = 0,
                        skippedExisting = normalized.Count,
                        nextUrl = $"/phase1/rnr-scanning.html?batchId={batchId}"
                    };
                    await tx.CommitAsync(ct);
                    Console.WriteLine($"[RnR Import] No new serials to add - {normalized.Count} already exist");
                    return Ok(summary);
                }

                // 6) Bulk add new expected items
                var newItems = newItemsData.Select(n => new RnrExpectedItem {
                    RnrExpectedItemId = Guid.NewGuid(),
                    BatchId = batchId,
                    Serial = n.norm, // Store normalized version
                    Model = n.model, // Store model from CSV/Excel
                    Notes = null,
                    SchoolId = slip.SchoolId // Copy school from collection slip
                });

                await _db.RnrExpectedItems.AddRangeAsync(newItems, ct);
                await _db.SaveChangesAsync(ct);

                // 7) Update ExpectedCount (server of truth)
                batch.ExpectedCount = await _db.RnrExpectedItems
                    .CountAsync(x => x.BatchId == batchId, ct);

                batch.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                Console.WriteLine($"[RnR Import] Success - Added {newItemsData.Count} new serials, total expected: {batch.ExpectedCount}");

                return Ok(new {
                    ok = true,
                    batchId = batchId,
                    added = newItemsData.Count,
                    skippedExisting = normalized.Count - newItemsData.Count,
                    expectedCount = batch.ExpectedCount,
                    nextUrl = $"/phase1/rnr-scanning.html?batchId={batchId}"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnR Import Error] {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"[RnR Import Stack] {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[RnR Import Inner Exception] {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
                
                // Provide more detailed error information
                var errorDetails = ex.Message;
                if (ex.InnerException != null)
                {
                    errorDetails += $" | Inner: {ex.InnerException.Message}";
                }
                
                return StatusCode(500, new { 
                    ok = false,
                    error = "Failed to import RnR slip", 
                    details = errorDetails,
                    exceptionType = ex.GetType().Name,
                    timestamp = DateTimeOffset.UtcNow
                });
            }
        }

        private async Task<List<(string Serial, string? Model, string? DeviceName)>> ReadItemsFromCsv(IFormFile file)
        {
            var items = new List<(string Serial, string? Model, string? DeviceName)>();
            using var sr = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            bool header = true;
            int serialCol = 0, modelCol = -1, deviceNameCol = -1;

            while (!sr.EndOfStream)
            {
                var line = (await sr.ReadLineAsync())?.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                var parts = line.Split(',').Select(p => p.Trim().Trim('"')).ToArray();
                
                if (header)
                {
                    // Find column indices
                    for (int i = 0; i < parts.Length; i++)
                    {
                        var headerName = parts[i].ToLowerInvariant();
                        if (headerName.Contains("serial") || headerName.Contains("imei"))
                            serialCol = i;
                        else if (headerName.Contains("model"))
                            modelCol = i;
                        else if (headerName.Contains("device name") || headerName.Contains("devicename") || headerName.Contains("device"))
                            deviceNameCol = i;
                    }
                    header = false;
                    continue;
                }

                var serial = parts.ElementAtOrDefault(serialCol)?.Trim() ?? "";
                var model = modelCol >= 0 && parts.Length > modelCol ? parts[modelCol]?.Trim() : null;
                var deviceName = deviceNameCol >= 0 && parts.Length > deviceNameCol ? parts[deviceNameCol]?.Trim() : null;
                
                if (!string.IsNullOrWhiteSpace(serial))
                {
                    int qty = int.TryParse(parts.ElementAtOrDefault(2), out var q) && q > 0 ? q : 1;
                    for (int i = 0; i < qty; i++)
                    {
                        items.Add((qty > 1 ? $"{serial}-{i + 1}" : serial, model, deviceName));
                    }
                }
            }
            return items;
        }

        private async Task<List<(string Serial, string? Model, string? DeviceName)>> ReadItemsFromXlsx(IFormFile file, CancellationToken ct)
        {
            var items = new List<(string Serial, string? Model, string? DeviceName)>();
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheets.First();

            // Find columns
            var header = ws.FirstRowUsed();
            if (header == null) throw new InvalidOperationException("XLSX file has no data.");
            
            int serialCol = 0, qtyCol = 0, modelCol = 0, deviceNameCol = 0;

            foreach (var cell in header.CellsUsed())
            {
                var name = (cell.GetString() ?? "").Trim().ToLowerInvariant();
                if (name is "serial" or "imei" or "serial number") 
                    serialCol = cell.Address.ColumnNumber;
                else if (name is "qty" or "quantity" or "quantity in stock") 
                    qtyCol = cell.Address.ColumnNumber;
                else if (name.Contains("model"))
                    modelCol = cell.Address.ColumnNumber;
                else if (name.Contains("device name") || name.Contains("devicename") || (name.Contains("device") && !name.Contains("serial")))
                    deviceNameCol = cell.Address.ColumnNumber;
            }

            if (serialCol == 0) throw new InvalidOperationException("XLSX must include a 'Serial' column.");

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var serial = (row.Cell(serialCol).GetString() ?? "").Trim();
                if (string.IsNullOrEmpty(serial)) continue;

                var model = modelCol > 0 ? (row.Cell(modelCol).GetString() ?? "").Trim() : null;
                if (string.IsNullOrWhiteSpace(model)) model = null;

                var deviceName = deviceNameCol > 0 ? (row.Cell(deviceNameCol).GetString() ?? "").Trim() : null;
                if (string.IsNullOrWhiteSpace(deviceName)) deviceName = null;

                var qty = 1;
                if (qtyCol > 0)
                {
                    if (row.Cell(qtyCol).TryGetValue<int>(out var q) && q > 0) qty = q;
                    else if (int.TryParse((row.Cell(qtyCol).GetString() ?? "").Trim(), out var qs) && qs > 0) qty = qs;
                }

                for (int i = 0; i < qty; i++)
                {
                    items.Add((qty > 1 ? $"{serial}-{i + 1}" : serial, model, deviceName));
                }
            }
            return items;
        }

        public record ScanDto(string Serial, string? Clerk);

        /// <summary>
        /// Scan a device for RnR batch with bulletproof normalization and structured responses
        /// </summary>
        [HttpPost("batches/{id:guid}/scan")]
        public async Task<IActionResult> Scan(Guid id, [FromBody] ScanDto dto, CancellationToken ct = default)
        {
            var raw = dto.Serial ?? "";
            var normalized = SerialNormalizer.Normalize(raw);
            
            if (normalized.Length == 0) 
                return BadRequest(new { code = "INVALID_SERIAL", message = "Serial required." });

            try
            {
                // Load batch to update scanning metadata
                var batch = await _db.ReceivingBatches
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);
                if (batch == null)
                {
                    return NotFound(new { code = "BATCH_NOT_FOUND", message = "Batch not found." });
                }

                // Check for duplicate using normalized comparison
                var existingScans = await _db.ReceivingBatchScans
                    .Where(s => s.BatchId == id)
                    .Select(s => s.Serial)
                    .ToListAsync(ct);

                var alreadyScanned = existingScans.Any(s => SerialNormalizer.Normalize(s) == normalized);
                
                if (alreadyScanned)
                {
                    return Conflict(new { 
                        code = "DUPLICATE_IN_BATCH", 
                        message = "This device was already scanned for this batch."
                    });
                }

                // Check if on slip using normalized comparison
                var expectedSerials = await _db.RnrExpectedItems
                    .Where(e => e.BatchId == id)
                    .Select(e => e.Serial)
                    .ToListAsync(ct);

                var isOnSlip = expectedSerials.Any(e => SerialNormalizer.Normalize(e) == normalized);
                
                // REJECT unexpected items - don't record them at all
                if (!isOnSlip)
                {
                    Console.WriteLine($"[Scan Rejected] {raw} -> Not on collection slip (Normalized: {normalized})");
                    return BadRequest(new { 
                        code = "NOT_ON_SLIP", 
                        message = "Device not found on collection slip. Only devices listed on the slip can be scanned.",
                        serial = raw
                    });
                }

                // Set scanning metadata on first successful scan
                if (batch.ScanningStartedAt == null)
                {
                    batch.ScanningStartedAt = DateTimeOffset.UtcNow;
                    batch.ScanningOfficer ??= dto.Clerk ?? User?.Identity?.Name ?? "Unknown";
                    batch.ReceivedDate ??= DateTimeOffset.UtcNow;
                }

                // Create scan record ONLY for expected items
                var scan = new ReceivingBatchScan
                {
                    BatchId = id,
                    Serial = raw, // Store original user input
                    Status = RnrScanStatus.Matched, // Always matched since we rejected unexpected
                    ScannedAt = DateTimeOffset.UtcNow,
                    SchoolId = batch.SchoolId // Copy school from batch
                };

                _db.ReceivingBatchScans.Add(scan);
                await _db.SaveChangesAsync(ct);

                // Calculate simple counters (no unexpected items since we reject them)
                var scannedCount = await _db.ReceivingBatchScans
                    .CountAsync(s => s.BatchId == id, ct);

                // Mirror current scanned count into batch live and bump updated timestamp
                batch.ActualCount = scannedCount;
                batch.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                Console.WriteLine($"[Scan] {raw} -> Matched (OnSlip: true, Normalized: {normalized})");

                return Ok(new {
                    ok = true,
                    item = new {
                        serial = raw,
                        onSlip = true, // Always true since we reject unexpected
                        status = "Matched"
                    },
                    counters = new {
                        expectedCount = expectedSerials.Count,
                        onSlipMatched = scannedCount,
                        missing = expectedSerials.Count - scannedCount,
                        unexpected = 0 // Always 0 since we reject unexpected items
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Scan Error] {ex.Message}");
                return StatusCode(500, new { 
                    code = "SCAN_FAILED", 
                    message = "Failed to record scan",
                    details = ex.Message
                });
            }
        }

        private async Task<object> GetBatchCounters(Guid batchId, CancellationToken ct = default)
        {
            var expectedCount = await _db.RnrExpectedItems.CountAsync(e => e.BatchId == batchId, ct);
            var scannedCount = await _db.ReceivingBatchScans.CountAsync(s => s.BatchId == batchId, ct);
            var onSlipMatched = await _db.ReceivingBatchScans.CountAsync(s => s.BatchId == batchId && s.Status == RnrScanStatus.Matched, ct);
            var unexpected = await _db.ReceivingBatchScans.CountAsync(s => s.BatchId == batchId && s.Status == RnrScanStatus.Unexpected, ct);
            var missing = expectedCount - onSlipMatched;

            return new {
                expectedCount = expectedCount,
                scannedCount = scannedCount,
                onSlipMatched = onSlipMatched,
                unexpected = unexpected,
                missing = missing
            };
        }

        [HttpPost("batches/{id:guid}/complete-scanning")]
        public async Task<IActionResult> CompleteScanning(Guid id, CancellationToken ct = default)
        {
            // Helper to normalize device identifiers for robust matching
            static string Norm(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var filtered = new string(s.Where(char.IsLetterOrDigit).ToArray());
                return filtered.ToUpperInvariant();
            }

            var expectedRaw = await _db.RnrExpectedItems
                .Where(e => e.BatchId == id)
                .Select(e => e.Serial)
                .ToListAsync(ct);

            var scannedRaw = await _db.ReceivingBatchScans
                .Where(s => s.BatchId == id && s.Status != RnrScanStatus.Duplicate)
                .Select(s => s.Serial)
                .ToListAsync(ct);

            var expNorm = expectedRaw.Select(Norm).ToHashSet();
            var scnNorm = scannedRaw.Select(Norm).ToHashSet();

            var missingNorm = expNorm.Except(scnNorm).ToList();
            var unexpectedNorm = scnNorm.Except(expNorm).ToList();

            // Return human-friendly values from originals that correspond to normalized diffs
            var missing = expectedRaw.Where(e => missingNorm.Contains(Norm(e))).Distinct().ToList();
            var unexpected = scannedRaw.Where(s => unexpectedNorm.Contains(Norm(s))).Distinct().ToList();

            var actualCount = scannedRaw.Count;
            var varianceCount = missing.Count + unexpected.Count;
            var hasVariance = varianceCount > 0;
            var ok = !hasVariance;

            var batch = await _db.ReceivingBatches.FindAsync(id);
            if (batch != null)
            {
                // Persist reconciliation summary and completion timestamp
                batch.ExpectedCount = expectedRaw.Count;
                batch.ActualCount = actualCount;
                batch.VarianceCount = varianceCount;
                batch.HasVariance = hasVariance;
                batch.ScanningCompletedAt = DateTimeOffset.UtcNow;
                batch.UpdatedAt = DateTimeOffset.UtcNow;

                // Maintain existing behavior: verify if no variance, else pending verification
                batch.Status = ok ? ReceivingBatchStatus.Verified : ReceivingBatchStatus.PendingVerification;

                await _db.SaveChangesAsync(ct);
            }

            var nextUrl = $"/phase1/rnr-verification.html?batchId={id}"; // proceed to verification either way

            return Ok(new { ok, missing, unexpected, nextUrl });
        }


        /// <summary>
        /// Get batch summary - shortage-centric (unexpected items are rejected at scan time)
        /// </summary>
        [HttpGet("batches/{id:guid}/summary")]
        public async Task<IActionResult> GetBatchSummary(Guid id, CancellationToken ct = default)
        {
            try
            {
                var expectedItems = await _db.RnrExpectedItems
                    .Where(e => e.BatchId == id)
                    .Select(e => e.Serial)
                    .ToListAsync(ct);

                var expectedSet = expectedItems
                    .Select(SerialNormalizer.Normalize)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var scannedItems = await _db.ReceivingBatchScans
                    .Where(s => s.BatchId == id)
                    .Select(s => s.Serial)
                    .ToListAsync(ct);

                var scannedSet = scannedItems
                    .Select(SerialNormalizer.Normalize)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Find missing items (expected but not scanned)
                var missingList = expectedItems
                    .Where(e => !scannedSet.Contains(SerialNormalizer.Normalize(e)))
                    .ToList();

                var onSlipScanned = expectedItems.Count - missingList.Count;

                Console.WriteLine($"[Summary] Batch {id}: Expected={expectedItems.Count}, Scanned={onSlipScanned}, Missing={missingList.Count}, Unexpected=0");

                // Unexpected always zero because we reject them at scan time
                return Ok(new {
                    expectedCount = expectedItems.Count,
                    onSlipScanned = onSlipScanned,
                    missing = missingList.Count,
                    unexpected = 0, // Always 0 - unexpected items rejected at scan
                    missingList = missingList,
                    unexpectedList = Array.Empty<string>()
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Summary Error] {ex.Message}");
                return StatusCode(500, new { error = "Failed to get batch summary", details = ex.Message });
            }
        }

        /// <summary>
        /// Get header details for RnR scanning page (Collection Slip card)
        /// </summary>
        [HttpGet("batches/{batchId:guid}/header")]
        public async Task<ActionResult<RnrBatchHeaderDto>> GetHeader(Guid batchId, CancellationToken ct)
        {
            // Load batch with slip info
            var batch = await _db.ReceivingBatches
                .Include(b => b.CollectionSlip)
                .FirstOrDefaultAsync(b => b.ReceivingBatchId == batchId, ct);

            if (batch == null)
                return NotFound();

            var slip = batch.CollectionSlip;

            // Expected items come from RnrExpectedItems (CSV import); fall back to 0 if none
            var expected = await _db.RnrExpectedItems
                .Where(e => e.BatchId == batchId)
                .CountAsync(ct);

            // MUST use scans count, not stale batch.ActualCount
            var scanned = await _db.ReceivingBatchScans
                .Where(s => s.BatchId == batchId && s.Status != RnrScanStatus.Duplicate)
                .CountAsync(ct);

            var dto = new RnrBatchHeaderDto
            {
                BatchId = batch.ReceivingBatchId,
                SlipNumber = slip?.SlipNumber ?? "N/A",
                SchoolName = slip?.SchoolName ?? "N/A",
                EmisCode = slip?.EmisCode ?? "N/A",
                CollectionDate = slip?.CollectionDate,
                CollectedBy = slip?.CollectedBy,
                ExpectedCount = expected,
                ScannedCount = scanned,
                MissingCount = Math.Max(0, expected - scanned)
            };

            return Ok(dto);
        }

        /// <summary>
        /// Get reconciliation summary for RnR batch
        /// </summary>
        [HttpGet("batches/{id:guid}/reconcile")]
        public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct = default)
        {
            try
            {
                var batch = await _db.ReceivingBatches
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);

                if (batch == null)
                    return NotFound(new { error = "Batch not found." });

                var scannedItems = batch.Items.Where(i => !string.IsNullOrEmpty(i.SerialNumber)).ToList();
                var scannedSerials = scannedItems.Select(i => i.SerialNumber!.ToUpperInvariant()).ToHashSet();

                // For RnR, we might not have expected items if no CSV was provided
                var expectedItems = batch.Items.Where(i => string.IsNullOrEmpty(i.Notes) || !i.Notes.Contains("Scanned by")).ToList();
                var expectedSerials = expectedItems.Select(i => i.SerialNumber!.ToUpperInvariant()).ToHashSet();

                var matched = scannedSerials.Count(s => expectedSerials.Contains(s));
                var missing = expectedSerials.Count(s => !scannedSerials.Contains(s));
                var unexpected = scannedSerials.Count(s => !expectedSerials.Contains(s));

                return Ok(new
                {
                    expected = expectedSerials.Count,
                    scanned = scannedSerials.Count,
                    matched = matched,
                    missing = missing,
                    unexpected = unexpected,
                    hasVariance = batch.HasVariance,
                    varianceCount = batch.VarianceCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnR Reconcile Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to reconcile batch", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Complete RnR batch
        /// </summary>
        [HttpPost("batches/{id:guid}/complete")]
        public async Task<IActionResult> Complete(Guid id, CancellationToken ct = default)
        {
            try
            {
                var batch = await _db.ReceivingBatches
                    .Include(b => b.Items)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);

                if (batch == null)
                    return NotFound(new { error = "Batch not found." });

                // Simple completion rule - can be enhanced based on business requirements
                if (batch.HasVariance && batch.VarianceResolution == null)
                    return BadRequest(new { error = "Cannot complete: variance must be resolved first." });

                batch.Status = ReceivingBatchStatus.Completed;
                batch.UpdatedAt = DateTimeOffset.UtcNow;

                await _db.SaveChangesAsync(ct);

                return Ok(new { status = "Completed", message = "RnR batch completed successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnR Complete Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to complete batch", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Generate Blind Transfer Copy for RnR batch
        /// </summary>
        [HttpGet("batches/{id:guid}/blind-transfer")]
        public async Task<IActionResult> BlindTransfer(Guid id, CancellationToken ct = default)
        {
            try
            {
                var batch = await _db.ReceivingBatches
                    .Include(b => b.CollectionSlip)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);

                if (batch == null)
                    return NotFound(new { error = "Batch not found." });

                // Get expected items from RnrExpectedItems table (where we actually save them)
                var expectedItems = await _db.RnrExpectedItems
                    .Where(e => e.BatchId == id)
                    .OrderBy(e => e.Serial)
                    .ToListAsync(ct);

                Console.WriteLine($"[BlindTransfer] Found {expectedItems.Count} expected items for batch {id}");

                var html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""UTF-8"">
                    <title>Blind Transfer Copy - RnR Receiving</title>
                    <style>
                        @media print {{
                            @page {{
                                size: A4;
                                margin: 1.5cm;
                            }}
                            body {{
                                margin: 0;
                                padding: 0;
                            }}
                            .no-print {{
                                display: none;
                            }}
                        }}
                        * {{
                            margin: 0;
                            padding: 0;
                            box-sizing: border-box;
                        }}
                        body {{
                            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                            margin: 40px;
                            background: #f5f5f5;
                            color: #333;
                        }}
                        .document-container {{
                            max-width: 210mm;
                            margin: 0 auto;
                            background: white;
                            padding: 40px;
                            box-shadow: 0 0 20px rgba(0,0,0,0.1);
                        }}
                        .header {{
                            border-bottom: 4px solid #0d6efd;
                            padding-bottom: 20px;
                            margin-bottom: 30px;
                        }}
                        .header h1 {{
                            color: #0d6efd;
                            font-size: 28px;
                            font-weight: 700;
                            margin-bottom: 8px;
                            letter-spacing: -0.5px;
                        }}
                        .header .subtitle {{
                            color: #666;
                            font-size: 14px;
                            font-style: italic;
                            margin-top: 5px;
                        }}
                        .meta-section {{
                            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                            color: white;
                            padding: 20px;
                            border-radius: 8px;
                            margin-bottom: 25px;
                            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
                        }}
                        .meta-grid {{
                            display: grid;
                            grid-template-columns: repeat(2, 1fr);
                            gap: 15px;
                            margin-top: 15px;
                        }}
                        .meta-item {{
                            display: flex;
                            flex-direction: column;
                        }}
                        .meta-label {{
                            font-size: 11px;
                            text-transform: uppercase;
                            letter-spacing: 0.5px;
                            opacity: 0.9;
                            margin-bottom: 4px;
                        }}
                        .meta-value {{
                            font-size: 14px;
                            font-weight: 600;
                        }}
                        .warning-box {{
                            background: #fff3cd;
                            border-left: 4px solid #ffc107;
                            padding: 15px;
                            margin: 25px 0;
                            border-radius: 4px;
                        }}
                        .warning-box p {{
                            color: #856404;
                            font-size: 13px;
                            margin: 0;
                            font-weight: 500;
                        }}
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin: 25px 0;
                            font-size: 13px;
                            box-shadow: 0 2px 8px rgba(0,0,0,0.05);
                        }}
                        thead {{
                            background: linear-gradient(135deg, #0d6efd 0%, #0056b3 100%);
                            color: white;
                        }}
                        th {{
                            padding: 14px 12px;
                            text-align: left;
                            font-weight: 600;
                            font-size: 12px;
                            text-transform: uppercase;
                            letter-spacing: 0.5px;
                            border: none;
                        }}
                        tbody tr {{
                            border-bottom: 1px solid #e0e0e0;
                            transition: background-color 0.2s;
                        }}
                        tbody tr:nth-child(even) {{
                            background-color: #f8f9fa;
                        }}
                        tbody tr:hover {{
                            background-color: #e3f2fd;
                        }}
                        td {{
                            padding: 12px;
                            border: none;
                        }}
                        td:first-child {{
                            font-weight: 600;
                            color: #0d6efd;
                            text-align: center;
                            width: 50px;
                        }}
                        .signature-section {{
                            margin-top: 50px;
                            padding-top: 30px;
                            border-top: 2px solid #e0e0e0;
                        }}
                        .signature-grid {{
                            display: grid;
                            grid-template-columns: repeat(2, 1fr);
                            gap: 40px;
                            margin-bottom: 30px;
                        }}
                        .signature-box {{
                            border: 2px dashed #ccc;
                            padding: 20px;
                            min-height: 100px;
                            border-radius: 4px;
                        }}
                        .signature-label {{
                            font-weight: 600;
                            font-size: 13px;
                            margin-bottom: 8px;
                            color: #333;
                        }}
                        .signature-line {{
                            border-bottom: 2px solid #333;
                            margin: 30px 0 10px 0;
                            height: 2px;
                        }}
                        .signature-date {{
                            font-size: 11px;
                            color: #666;
                            margin-top: 5px;
                        }}
                        .summary-box {{
                            background: #f8f9fa;
                            padding: 15px;
                            border-radius: 6px;
                            margin-top: 20px;
                            text-align: center;
                        }}
                        .summary-box strong {{
                            color: #0d6efd;
                            font-size: 16px;
                        }}
                        .print-button {{
                            position: fixed;
                            top: 20px;
                            right: 20px;
                            background: #0d6efd;
                            color: white;
                            border: none;
                            padding: 12px 24px;
                            border-radius: 6px;
                            cursor: pointer;
                            font-size: 14px;
                            font-weight: 600;
                            box-shadow: 0 4px 6px rgba(0,0,0,0.2);
                            z-index: 1000;
                        }}
                        .print-button:hover {{
                            background: #0056b3;
                            transform: translateY(-2px);
                            box-shadow: 0 6px 8px rgba(0,0,0,0.3);
                        }}
                    </style>
                </head>
                <body>
                    <button class=""print-button no-print"" onclick=""window.print()"">🖨️ Print Document</button>
                    <div class=""document-container"">
                        <div class=""header"">
                            <h1>Blind Transfer Copy - RnR Receiving</h1>
                            <div class=""subtitle"">Physical Handover Document - No Pricing Information</div>
                        </div>
                        
                        <div class=""meta-section"">
                            <div class=""meta-grid"">
                                <div class=""meta-item"">
                                    <div class=""meta-label"">Batch ID</div>
                                    <div class=""meta-value"">{batch.ReceivingBatchId}</div>
                                </div>
                                <div class=""meta-item"">
                                    <div class=""meta-label"">School</div>
                                    <div class=""meta-value"">{batch.CollectionSlip?.SchoolName ?? "Unknown"}</div>
                                </div>
                                <div class=""meta-item"">
                                    <div class=""meta-label"">Slip Number</div>
                                    <div class=""meta-value"">{batch.CollectionSlip?.SlipNumber ?? "N/A"}</div>
                                </div>
                                <div class=""meta-item"">
                                    <div class=""meta-label"">Collection Date</div>
                                    <div class=""meta-value"">{batch.CollectionSlip?.CollectionDate.ToString("yyyy-MM-dd") ?? "N/A"}</div>
                                </div>
                                <div class=""meta-item"">
                                    <div class=""meta-label"">Received By</div>
                                    <div class=""meta-value"">{batch.ReceivedBy ?? "N/A"}</div>
                                </div>
                                <div class=""meta-item"">
                                    <div class=""meta-label"">Type</div>
                                    <div class=""meta-value"">{(batch.SourceType == ReceivingSourceType.RnrEmergency ? "Emergency RnR" : "Normal RnR")}</div>
                                </div>
                            </div>
                        </div>
                        
                        <div class=""warning-box"">
                            <p>⚠️ <strong>Note:</strong> No pricing information. Use for physical handover and signatures only.</p>
                        </div>
                        
                        <table>
                            <thead>
                                <tr>
                                    <th>#</th>
                                    <th>Serial Number</th>
                                    <th>Model</th>
                                    <th>Notes</th>
                                </tr>
                            </thead>
                            <tbody>";

                int counter = 1;
                foreach (var item in expectedItems)
                {
                    html += $@"
                                <tr>
                                    <td>{counter++}</td>
                                    <td>{item.Serial ?? "N/A"}</td>
                                    <td>{item.Model ?? "N/A"}</td>
                                    <td>{item.Notes ?? ""}</td>
                                </tr>";
                }

                html += $@"
                            </tbody>
                        </table>
                        
                        <div class=""signature-section"">
                            <div class=""signature-grid"">
                                <div class=""signature-box"">
                                    <div class=""signature-label"">Receiver Signature</div>
                                    <div class=""signature-line""></div>
                                    <div class=""signature-date"">Date: _______________</div>
                                </div>
                                <div class=""signature-box"">
                                    <div class=""signature-label"">School Representative</div>
                                    <div class=""signature-line""></div>
                                    <div class=""signature-date"">Date: _______________</div>
                                </div>
                            </div>
                            
                            <div class=""summary-box"">
                                <strong>Total Expected Items: {expectedItems.Count}</strong>
                            </div>
                        </div>
                    </div>
                </body>
                </html>";

                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnR BTC Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to generate Blind Transfer Copy", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Get scanned items for RnR batch
        /// </summary>
        [HttpGet("batches/{id:guid}/items")]
        public async Task<IActionResult> GetBatchItems(Guid id, CancellationToken ct = default)
        {
            try
            {
                // Get batch with CollectionSlip to access school info
                var batch = await _db.ReceivingBatches
                    .Include(b => b.CollectionSlip)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);

                // Get school info from batch/slip
                long? schoolId = batch?.CollectionSlip?.SchoolId ?? batch?.SchoolId;
                string? schoolName = batch?.CollectionSlip?.SchoolName;
                string? emisCode = batch?.CollectionSlip?.EmisCode;

                var items = await _db.ReceivingBatchItems
                    .Where(i => i.ReceivingBatchId == id)
                    .OrderBy(i => i.CreatedAt)
                    .Select(i => new
                    {
                        i.ReceivingBatchItemId,
                        i.SerialNumber,
                        i.IMEI,
                        i.Brand,
                        i.Model,
                        i.Notes,
                        i.CreatedAt,
                        IsScanned = !string.IsNullOrEmpty(i.Notes) && i.Notes.Contains("Scanned by"),
                        // Include school info from batch/slip
                        SchoolId = schoolId,
                        SchoolName = schoolName,
                        EmisCode = emisCode
                    })
                    .ToListAsync(ct);

                return Ok(items);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RnR Get Items Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to get batch items", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Get batch data for verification page (includes school info and scanned count)
        /// </summary>
        [HttpGet("batches/{batchId:guid}")]
        public async Task<IActionResult> GetBatchData(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                // Load batch with CollectionSlip
                var batch = await _db.ReceivingBatches
                    .Include(b => b.CollectionSlip)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == batchId, ct);

                if (batch == null)
                    return NotFound(new { error = "Batch not found" });

                var slip = batch.CollectionSlip;

                // Calculate scanned count from ReceivingBatchScans table
                var scanned = await _db.ReceivingBatchScans
                    .CountAsync(s => s.BatchId == batchId, ct);

                return Ok(new
                {
                    batchId = batch.ReceivingBatchId,
                    type = batch.SourceType.ToString(),
                    status = batch.Status.ToString(),
                    collectionSlip = new
                    {
                        slipNumber = slip?.SlipNumber ?? "N/A",
                        schoolName = slip?.SchoolName ?? "N/A",
                        emisCode = slip?.EmisCode ?? "N/A"
                    },
                    devicesScanned = scanned,
                    scanningOfficer = batch.ReceivedBy,
                    scanningCompletedAt = batch.ScanningCompletedAt
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Get Batch Data Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to get batch data", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Get scanned devices for a batch (for table display) - Shows actual school name
        /// </summary>
        [HttpGet("batches/{batchId:guid}/scans")]
        public async Task<IActionResult> GetScans(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                // Load batch with CollectionSlip to get school info
                var batch = await _db.ReceivingBatches
                    .Include(b => b.CollectionSlip)
                    .FirstOrDefaultAsync(b => b.ReceivingBatchId == batchId, ct);

                if (batch == null)
                    return NotFound(new { error = "Batch not found" });

                var scans = await _db.ReceivingBatchScans
                    .Where(x => x.BatchId == batchId)
                    .OrderByDescending(x => x.ScannedAt)
                    .ToListAsync(ct);

                // Get the school name for this batch
                string schoolName = "Unknown School";
                var batchSchoolId = batch.CollectionSlip?.SchoolId ?? batch.SchoolId;
                
                if (batchSchoolId.HasValue)
                {
                    var school = await _coreDb.Schools
                        .FirstOrDefaultAsync(s => s.SchoolId == batchSchoolId.Value, ct);
                    schoolName = school?.Name ?? "Unknown School";
                }

                // Get serials and load expected items to get DeviceName + Model
                var serials = scans.Select(s => s.Serial).ToList();
                var expectedItems = new Dictionary<string, RnrExpectedItem>();
                
                if (serials.Count > 0)
                {
                    // Load expected items for device info (DeviceName + Model)
                    var items = await _db.RnrExpectedItems
                        .AsNoTracking()
                        .Where(e => e.BatchId == batchId && serials.Contains(e.Serial))
                        .ToListAsync(ct);
                    
                    foreach (var item in items)
                    {
                        if (!string.IsNullOrEmpty(item.Serial))
                            expectedItems[item.Serial] = item;
                    }
                }

                // Also load core devices as fallback
                var devices = new Dictionary<string, DeviceDesk.Infrastructure.Data.Device>();
                
                if (serials.Count > 0)
                {
                    var coreDevices = await _coreDb.Devices
                        .AsNoTracking()
                        .Where(d => d.SerialNumber != null && serials.Contains(d.SerialNumber))
                        .ToListAsync(ct);
                    
                    foreach (var dev in coreDevices)
                    {
                        if (dev.SerialNumber != null)
                            devices[dev.SerialNumber] = dev;
                    }
                }

                // Build response - combine DeviceName + Model for deviceInfo
                var result = scans.Select(s =>
                {
                    expectedItems.TryGetValue(s.Serial, out var expected);
                    devices.TryGetValue(s.Serial, out var dev);
                    
                    // Build device info: prefer DeviceName + Model from expected items
                    string deviceInfo = "Unknown";
                    if (expected != null)
                    {
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(expected.DeviceName))
                            parts.Add(expected.DeviceName);
                        if (!string.IsNullOrWhiteSpace(expected.Model))
                            parts.Add(expected.Model);
                        deviceInfo = parts.Count > 0 ? string.Join(" ", parts) : "Unknown";
                    }
                    else if (dev != null)
                    {
                        deviceInfo = dev.Model ?? "Unknown";
                    }
                    else if (!string.IsNullOrWhiteSpace(s.DeviceInfo))
                    {
                        deviceInfo = s.DeviceInfo;
                    }
                    
                    return new
                    {
                        serial = s.Serial,
                        deviceInfo = deviceInfo,
                        schoolMatch = schoolName, // Show the actual school name for all devices in batch
                        status = s.Status.ToString(),
                        scannedAt = s.ScannedAt
                    };
                }).ToList();

                Console.WriteLine($"[GetScans] Returning {result.Count} scans with school: {schoolName}");
                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Get Scans Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to get scans", details = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }
}
