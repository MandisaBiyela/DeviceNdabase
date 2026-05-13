using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase1.Services;
using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Services;
using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DeviceDesk.Modules.Phase1.Controllers
{
    [ApiController]
    [Route("api/phase1/receiving")]
    public class ReceivingController : ControllerBase
    {
        private readonly ReceivingService _service;
        private readonly BlindCopyService _blindCopy;
        private readonly DocumentService _documentService;
        private readonly SpreadsheetParserService _spreadsheetParser;
        private readonly NewStockBatchService _newStockBatchService;
        private readonly Phase1DbContext _phase1Db;
        private readonly DeviceDeskDbContext _coreDb;
        private readonly ILogger<ReceivingController> _logger;
        // private readonly OrderIntegrationService _orderIntegration; // Commented out - using NewStockBatch workflow now

        public ReceivingController(
            ReceivingService service, 
            BlindCopyService blindCopy, 
            DocumentService documentService, 
            SpreadsheetParserService spreadsheetParser,
            NewStockBatchService newStockBatchService,
            Phase1DbContext phase1Db,
            DeviceDeskDbContext coreDb,
            ILogger<ReceivingController> logger)
            // OrderIntegrationService orderIntegration) // Commented out
        {
            _service = service;
            _blindCopy = blindCopy;
            _documentService = documentService;
            _spreadsheetParser = spreadsheetParser;
            _newStockBatchService = newStockBatchService;
            _phase1Db = phase1Db;
            _coreDb = coreDb;
            _logger = logger;
            // _orderIntegration = orderIntegration; // Commented out
        }

        /// <summary>
        /// Get all receiving batches with statistics for the receiving list page
        /// </summary>
        [HttpGet("list")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllReceivingBatches(CancellationToken ct)
        {
            try
            {
                var batches = await _service.GetAllReceivingBatchesAsync(ct);
                return Ok(batches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch receiving batches", details = ex.Message });
            }
        }

        // Commented out - using NewStockBatch workflow now
        // /// <summary>
        // /// Get approved orders from Phase 0 available for receiving in Phase 1
        // /// This is the bridge between Phase 0 (Orders) and Phase 1 (Receiving)
        // /// </summary>
        // [HttpGet("orders")]
        // public async Task<IActionResult> GetAvailableOrders(CancellationToken ct)
        // {
        //     var orders = await _orderIntegration.GetApprovedOrdersForReceivingAsync(ct);
        //     return Ok(orders);
        // }

        // /// <summary>
        // /// Get specific order details for batch creation
        // /// </summary>
        // [HttpGet("orders/{orderId}")]
        // public async Task<IActionResult> GetOrderDetails(Guid orderId, CancellationToken ct)
        // {
        //     var order = await _orderIntegration.GetOrderDetailsAsync(orderId, ct);
        //     
        //     if (order == null)
        //     {
        //         return NotFound(new { error = "Order not found or not approved" });
        //     }

        //     return Ok(order);
        // }

        /// <summary>
        /// Get NewStockBatches from Phase 0 available for receiving in Phase 1
        /// This enables Phase 0 → Phase 1 integration for New Stock workflow
        /// </summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetAvailableNewStockBatches(CancellationToken ct)
        {
            try
            {
                // Pull PendingScan batches with their items so the receiving UI can show
                // PO/Project/Financial Year + school breakdown without an extra round-trip.
                var batches = await _coreDb.NewStockBatches
                    .AsNoTracking()
                    .Where(b => b.Status == Modules.Phase0.Models.NewStockBatchStatus.PendingScan)
                    .Include(b => b.Items)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync(ct);

                var orders = batches.Select(b =>
                {
                    var schoolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in b.Items)
                    {
                        if (string.IsNullOrWhiteSpace(item.SchoolBreakdownJson)) continue;
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(item.SchoolBreakdownJson);
                            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                if (el.TryGetProperty("schoolName", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var name = n.GetString();
                                    if (!string.IsNullOrWhiteSpace(name)) schoolNames.Add(name);
                                }
                            }
                        }
                        catch
                        {
                            // ignore malformed JSON; surface what we can
                        }
                    }

                    var summary = schoolNames.Count > 0
                        ? $"{schoolNames.Count} school{(schoolNames.Count == 1 ? "" : "s")}, {b.TotalQuantityExpected} device{(b.TotalQuantityExpected == 1 ? "" : "s")}"
                        : $"{b.TotalQuantityExpected} device{(b.TotalQuantityExpected == 1 ? "" : "s")}";

                    return new
                    {
                        orderId = b.BatchId,
                        orderNumber = b.BatchNumber,
                        invoiceNumber = b.InvoiceNumber,
                        supplierName = b.SupplierName,
                        orderDate = b.CreatedAt,
                        status = b.Status.ToString(),
                        totalQuantity = b.TotalQuantityExpected,
                        receivedQuantity = b.TotalQuantityScanned,
                        poNumber = b.PoNumber,
                        projectName = b.ProjectName,
                        financialYear = b.FinancialYear,
                        procurementOrderId = b.ProcurementOrderId,
                        schoolCount = schoolNames.Count,
                        schoolNames = schoolNames.OrderBy(x => x).ToArray(),
                        breakdownSummary = summary
                    };
                }).ToList();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch Phase 0 batches", details = ex.Message });
            }
        }

        /// <summary>
        /// Get specific NewStockBatch details for batch creation
        /// </summary>
        [HttpGet("orders/{batchId}")]
        public async Task<IActionResult> GetNewStockBatchDetails(Guid batchId, CancellationToken ct)
        {
            try
            {
                var batch = await _newStockBatchService.GetBatchDetailsAsync(batchId, ct);
                
                if (batch == null)
                {
                    return NotFound(new { error = "Batch not found" });
                }

                // Transform to match expected order format for frontend
                var orderDetails = new
                {
                    orderId = batch.BatchId,
                    orderNumber = batch.BatchNumber,
                    invoiceNumber = batch.InvoiceNumber,
                    supplierName = batch.SupplierName,
                    orderDate = batch.CreatedAt,
                    status = batch.StatusText,
                    totalQuantity = batch.TotalQuantityExpected,
                    receivedQuantity = batch.TotalQuantityScanned,
                    lines = batch.Items.Select(i => new
                    {
                        lineId = i.ItemId,
                        brand = i.Brand,
                        model = i.Model,
                        deviceType = i.DeviceType,
                        description = i.Description,
                        quantityOrdered = i.QuantityExpected,
                        quantityReceived = i.QuantityScanned
                    }).ToList()
                };

                return Ok(orderDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch batch details", details = ex.Message });
            }
        }

        /// <summary>
        /// Get available collection slips for RnR receiving
        /// </summary>
        [HttpGet("collection-slips")]
        public async Task<ActionResult<List<CollectionSlipDto>>> GetAvailableCollectionSlips(
            [FromQuery] ReceivingSourceType? sourceType,
            CancellationToken ct)
        {
            try
            {
                var slips = await _service.GetAvailableCollectionSlipsAsync(sourceType, ct);
                return Ok(slips);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Create a new receiving batch
        /// </summary>
        [HttpPost("batches")]
        public async Task<ActionResult<ReceivingBatchDto>> CreateReceivingBatch(
            [FromBody] CreateReceivingBatchRequest request,
            CancellationToken ct)
        {
            try
            {
                var batch = await _service.CreateReceivingBatchAsync(request, ct);
                var dto = await _service.GetReceivingBatchAsync(batch.ReceivingBatchId, ct);
                return Ok(dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                // Log the full exception for debugging
                _logger.LogError(ex, "Error creating receiving batch: {Message}", ex.Message);
                return StatusCode(500, new { error = "An error occurred while creating the receiving batch.", details = ex.Message, innerException = ex.InnerException?.Message });
            }
        }

        /// <summary>
        /// Get receiving batch details
        /// </summary>
        [HttpGet("batches/{id}")]
        public async Task<ActionResult<ReceivingBatchDto>> GetReceivingBatch(Guid id, CancellationToken ct)
        {
            try
            {
                var batch = await _service.GetReceivingBatchAsync(id, ct);
                if (batch == null)
                    return NotFound(new { error = "Receiving batch not found." });

                // For RnR batches, enhance the response with school info and scanned count
                if (batch.SourceType == ReceivingSourceType.RnrNormal || batch.SourceType == ReceivingSourceType.RnrEmergency)
                {
                    // Load batch with slip to get full details
                    var fullBatch = await _phase1Db.ReceivingBatches
                        .Include(b => b.CollectionSlip)
                        .FirstOrDefaultAsync(b => b.ReceivingBatchId == id, ct);

                    if (fullBatch != null)
                    {
                        var slip = fullBatch.CollectionSlip;
                        
                        // Calculate scanned count from ReceivingBatchScans table
                        var scanned = await _phase1Db.ReceivingBatchScans
                            .CountAsync(s => s.BatchId == id, ct);

                        // Get school name - prefer from slip, fallback to Schools table if SchoolId exists
                        string schoolName = slip?.SchoolName ?? "N/A";
                        if ((string.IsNullOrWhiteSpace(schoolName) || schoolName == "N/A") && slip != null && slip.SchoolId > 0)
                        {
                            var school = await _coreDb.Schools
                                .AsNoTracking()
                                .FirstOrDefaultAsync(s => s.SchoolId == slip.SchoolId, ct);
                            if (school != null)
                            {
                                schoolName = school.Name;
                            }
                        }

                        // Return enhanced data for verification page
                        return Ok(new
                        {
                            batchId = batch.ReceivingBatchId,
                            type = batch.SourceTypeName,
                            status = batch.StatusName,
                            collectionSlip = new
                            {
                                slipNumber = slip?.SlipNumber ?? "N/A",
                                schoolName = schoolName,
                                emisCode = slip?.EmisCode ?? "N/A"
                            },
                            devicesScanned = scanned,
                            scanningOfficer = batch.ReceivedBy,
                            scanningCompletedAt = fullBatch.ScanningCompletedAt
                        });
                    }
                }

                return Ok(batch);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Generate Blind Copy PDF for a receiving batch
        /// </summary>
        [HttpGet("batches/{id}/blind-copy")]
        public async Task<IActionResult> GenerateBlindCopy(Guid id, CancellationToken ct)
        {
            try
            {
                var pdfBytes = await _blindCopy.GenerateBlindCopyPdfAsync(id, ct);
                
                var fileName = $"BlindCopy_{id}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate blind copy PDF.", details = ex.Message });
            }
        }

        /// <summary>
        /// Upload invoice/document for a receiving batch
        /// </summary>
        [HttpPost("batches/{batchId}/documents")]
        public async Task<IActionResult> UploadDocument(
            Guid batchId, 
            IFormFile file, 
            [FromQuery] string docType = "INVOICE",
            CancellationToken ct = default)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file uploaded. Please select a file." });
                }

                // Validate batch exists
                var batch = await _service.GetReceivingBatchAsync(batchId, ct);
                if (batch == null)
                {
                    return NotFound(new { error = "Receiving batch not found." });
                }

                // Save document using Phase0 DocumentService
                var (documentId, fileName, documentType) = await _documentService.SaveForBatchAsync(batchId, file, docType, ct);
                
                return Ok(new { 
                    documentId, 
                    fileName, 
                    docType = documentType,
                    message = "Document uploaded successfully" 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Document Upload Error] {ex.InnerException?.Message ?? ex.Message}");
                Console.WriteLine($"[Document Upload Stack] {ex}");
                return StatusCode(500, new { error = "Failed to upload document", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Get documents for a receiving batch
        /// </summary>
        [HttpGet("batches/{batchId}/documents")]
        public async Task<IActionResult> GetBatchDocuments(Guid batchId, CancellationToken ct = default)
        {
            try
            {
                // Validate batch exists
                var batch = await _service.GetReceivingBatchAsync(batchId, ct);
                if (batch == null)
                {
                    return NotFound(new { error = "Receiving batch not found." });
                }

                var documents = await _documentService.GetDocumentsForBatchAsync(batchId, ct);
                return Ok(documents);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Get Documents Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to retrieve documents", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Download a document
        /// </summary>
        [HttpGet("documents/{documentId}/download")]
        public async Task<IActionResult> DownloadDocument(long documentId, CancellationToken ct = default)
        {
            try
            {
                var document = await _documentService.GetDocumentAsync(documentId, ct);
                if (document == null)
                {
                    return NotFound(new { error = "Document not found." });
                }

                return File(document.FileData, document.ContentType, document.FileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Download Document Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to download document", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Delete a document
        /// </summary>
        [HttpDelete("documents/{documentId}")]
        public async Task<IActionResult> DeleteDocument(long documentId, CancellationToken ct = default)
        {
            try
            {
                var deleted = await _documentService.DeleteDocumentAsync(documentId, ct);
                if (!deleted)
                {
                    return NotFound(new { error = "Document not found." });
                }

                return Ok(new { message = "Document deleted successfully" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Delete Document Error] {ex.InnerException?.Message ?? ex.Message}");
                return StatusCode(500, new { error = "Failed to delete document", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        /// <summary>
        /// Upload and parse spreadsheet for bulk device entry
        /// </summary>
        /// <summary>
        /// Upload and parse spreadsheet for bulk device entry
        /// Validates file format, parses data, and saves as document
        /// Errors are handled by GlobalErrorHandlingMiddleware
        /// </summary>
        [HttpPost("batches/{batchId}/spreadsheet")]
        public async Task<IActionResult> UploadSpreadsheet(
            Guid batchId,
            IFormFile file,
            CancellationToken ct = default)
        {
            // Validation is now handled by middleware - just throw exceptions
            DeviceDesk.Services.ValidationService.ValidateGuid(batchId, nameof(batchId));
            DeviceDesk.Services.ValidationService.ValidateFile(file, new[] { ".xlsx", ".xls", ".csv" });

            // Validate batch exists
            var batch = await _service.GetReceivingBatchAsync(batchId, ct);
            DeviceDesk.Services.ValidationService.EnsureExists(batch, "ReceivingBatch", batchId);

            // Parse spreadsheet
            using var stream = file.OpenReadStream();
            var parseResult = await _spreadsheetParser.ParseSpreadsheetAsync(stream, file.FileName, ct);

            // If no valid rows, throw validation exception
            if (parseResult.Errors.Any() && parseResult.ValidRows == 0)
            {
                throw new DeviceDesk.Middleware.ValidationException(
                    parseResult.Errors.Select(e => new DeviceDesk.Middleware.ValidationError("spreadsheet", e)).ToList()
                );
            }

            // Save the spreadsheet as a document for reference
            using var saveStream = file.OpenReadStream();
            var (documentId, fileName, documentType) = await _documentService.SaveForBatchAsync(batchId, file, "SPREADSHEET", ct);

            return Ok(new
            {
                message = "Spreadsheet parsed successfully",
                documentId,
                fileName,
                totalRows = parseResult.TotalRows,
                validRows = parseResult.ValidRows,
                devices = parseResult.Devices,
                errors = parseResult.Errors,
                warnings = parseResult.Errors.Any() ? "Some rows had errors and were skipped" : null
            });
        }

        /// <summary>
        /// Get dashboard statistics for Phase 1 receiving
        /// </summary>
        [HttpGet("dashboard/stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
        {
            try
            {
                var totalBatches = await _phase1Db.ReceivingBatches.CountAsync(ct);
                var completedBatches = await _phase1Db.ReceivingBatches
                    .CountAsync(b => b.Status == ReceivingBatchStatus.Completed, ct);
                var inProgressBatches = await _phase1Db.ReceivingBatches
                    .CountAsync(b => b.Status != ReceivingBatchStatus.Completed && b.Status != ReceivingBatchStatus.Cancelled, ct);
                
                // Total devices: sum of ActualCount or count from ReceivingBatchItems
                var totalDevices = await _phase1Db.ReceivingBatches
                    .SumAsync(b => (int?)b.ActualCount ?? 0, ct);
                
                // If ActualCount is not set, count from items
                if (totalDevices == 0)
                {
                    totalDevices = await _phase1Db.ReceivingBatchItems.CountAsync(ct);
                }

                // Source breakdown
                var newStockCount = await _phase1Db.ReceivingBatches
                    .CountAsync(b => b.SourceType == ReceivingSourceType.NewStock, ct);
                var rnrNormalCount = await _phase1Db.ReceivingBatches
                    .CountAsync(b => b.SourceType == ReceivingSourceType.RnrNormal, ct);
                var rnrEmergencyCount = await _phase1Db.ReceivingBatches
                    .CountAsync(b => b.SourceType == ReceivingSourceType.RnrEmergency, ct);

                return Ok(new
                {
                    totalBatches,
                    completedBatches,
                    inProgressBatches,
                    totalDevices,
                    newStockCount,
                    rnrNormalCount,
                    rnrEmergencyCount
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch dashboard stats", details = ex.Message });
            }
        }

        /// <summary>
        /// Get recent activity (last 20 batches) for dashboard
        /// </summary>
        [HttpGet("dashboard/recent")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecentActivity(CancellationToken ct)
        {
            try
            {
                var batches = await _phase1Db.ReceivingBatches
                    .Include(b => b.CollectionSlip)
                    .Include(b => b.Order)
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(20)
                    .Select(b => new
                    {
                        batchId = b.ReceivingBatchId.ToString(),
                        sourceType = b.SourceType.ToString(),
                        sourceTypeName = b.SourceType == ReceivingSourceType.NewStock ? "New Stock"
                            : b.SourceType == ReceivingSourceType.RnrNormal ? "RnR Normal"
                            : "RnR Emergency",
                        status = b.Status.ToString(),
                        statusName = b.Status.ToString(),
                        deviceCount = b.ActualCount > 0 ? b.ActualCount : b.ExpectedCount,
                        createdAt = b.CreatedAt,
                        schoolName = b.SourceType == ReceivingSourceType.NewStock 
                            ? (b.Order != null ? b.Order.SupplierName : "")
                            : (b.CollectionSlip != null ? b.CollectionSlip.SchoolName : ""),
                        documentNumber = b.SourceType == ReceivingSourceType.NewStock
                            ? (b.Order != null ? b.Order.InvoiceNumber ?? b.Order.OrderNumber : "")
                            : (b.CollectionSlip != null ? b.CollectionSlip.SlipNumber : "")
                    })
                    .ToListAsync(ct);

                return Ok(batches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to fetch recent activity", details = ex.Message });
            }
        }

        /// <summary>
        /// Export dashboard batch data as CSV with date filter.
        /// range: today | week | month | all | custom
        /// </summary>
        [HttpGet("dashboard/export")]
        [AllowAnonymous]
        public async Task<IActionResult> ExportDashboardCsv(
            [FromQuery] string? range = "today",
            [FromQuery] DateTimeOffset? fromDate = null,
            [FromQuery] DateTimeOffset? toDate = null,
            CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var selectedRange = (range ?? "today").Trim().ToLowerInvariant();

            DateTimeOffset from;
            DateTimeOffset to;
            switch (selectedRange)
            {
                case "today":
                    from = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
                    to = from.AddDays(1).AddTicks(-1);
                    break;
                case "week":
                    var dayOffset = ((int)now.DayOfWeek + 6) % 7;
                    var weekStart = now.Date.AddDays(-dayOffset);
                    from = new DateTimeOffset(weekStart, TimeSpan.Zero);
                    to = from.AddDays(7).AddTicks(-1);
                    break;
                case "month":
                    from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
                    to = from.AddMonths(1).AddTicks(-1);
                    break;
                case "custom":
                    if (!fromDate.HasValue || !toDate.HasValue)
                        return BadRequest(new { error = "fromDate and toDate are required when range=custom" });
                    from = fromDate.Value;
                    to = toDate.Value;
                    break;
                case "all":
                default:
                    from = DateTimeOffset.MinValue;
                    to = DateTimeOffset.MaxValue;
                    break;
            }

            var rows = await _phase1Db.ReceivingBatches
                .Include(b => b.CollectionSlip)
                .Include(b => b.Order)
                .Where(b => b.CreatedAt >= from && b.CreatedAt <= to)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new
                {
                    b.ReceivingBatchId,
                    SourceType = b.SourceType == ReceivingSourceType.NewStock ? "New Stock"
                        : b.SourceType == ReceivingSourceType.RnrNormal ? "RnR Normal"
                        : "RnR Emergency",
                    Status = b.Status.ToString(),
                    SupplierOrSchool = b.SourceType == ReceivingSourceType.NewStock
                        ? (b.Order != null ? b.Order.SupplierName : "")
                        : (b.CollectionSlip != null ? b.CollectionSlip.SchoolName : ""),
                    DocumentNumber = b.SourceType == ReceivingSourceType.NewStock
                        ? (b.Order != null ? b.Order.InvoiceNumber ?? b.Order.OrderNumber : "")
                        : (b.CollectionSlip != null ? b.CollectionSlip.SlipNumber : ""),
                    DeviceCount = b.ActualCount > 0 ? b.ActualCount : b.ExpectedCount,
                    b.CreatedAt
                })
                .ToListAsync(ct);

            var csv = new StringBuilder();
            csv.AppendLine("BatchId,SourceType,Status,SupplierOrSchool,DocumentNumber,DeviceCount,CreatedAtUtc");
            foreach (var row in rows)
            {
                csv.AppendLine(
                    $"{EscapeCsv(row.ReceivingBatchId.ToString())}," +
                    $"{EscapeCsv(row.SourceType)}," +
                    $"{EscapeCsv(row.Status)}," +
                    $"{EscapeCsv(row.SupplierOrSchool)}," +
                    $"{EscapeCsv(row.DocumentNumber)}," +
                    $"{row.DeviceCount}," +
                    $"{row.CreatedAt:O}"
                );
            }

            var fileName = $"phase1_dashboard_{selectedRange}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        private static string EscapeCsv(string? raw)
        {
            var value = raw ?? string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}
