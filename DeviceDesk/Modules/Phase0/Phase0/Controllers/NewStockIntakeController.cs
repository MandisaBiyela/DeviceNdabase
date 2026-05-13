using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/new")]
    public class NewStockIntakeController : ControllerBase
    {
        private readonly CsvImportService _csv;
        private readonly DocumentService _docs;
        private readonly DeviceDeskDbContext _db;
        public NewStockIntakeController(CsvImportService csv, DocumentService docs, DeviceDeskDbContext db)
        {
            _csv = csv; _docs = docs; _db = db;
        }

        [HttpPost("import")]
        public async Task<ActionResult<ImportResultDto>> ImportCsv(CancellationToken ct)
        {
            // Get file from form
            var file = Request.Form.Files.GetFile("file");
            
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { error = "No file uploaded. Please select a file to import." });
            }

            try
            {
                var result = await _csv.ImportAsync(file, "NEW", ct);
                return Ok(result);
            }
            catch (Exception ex) 
            { 
                return BadRequest(new { error = ex.Message, details = ex.ToString() }); 
            }
        }

        [HttpPost("import-manual")]
        public async Task<IActionResult> ImportManual([FromForm] string itemsJson, IFormFile? pack, CancellationToken ct)
        {
            try
            {
                var itemsWrapper = System.Text.Json.JsonSerializer.Deserialize<ManualItemsWrapper>(itemsJson);
                var items = itemsWrapper?.items ?? new List<ManualItem>();
                var batch = new DeviceImportBatch { Source = "NEW", FileName = pack?.FileName ?? "manual-entry" };
                _db.Batches.Add(batch);
                var seenInUpload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Promote the first non-empty per-item OrderNumber onto the batch so
                // it shows up in batch lists and links back to procurement orders.
                var firstOrderNumber = items
                    .Select(x => x.orderNumber?.Trim())
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (!string.IsNullOrWhiteSpace(firstOrderNumber))
                {
                    batch.OrderNumber = firstOrderNumber;
                }

                foreach (var it in items)
                {
                    var brand = it.brand?.Trim();
                    var model = it.model?.Trim();
                    var deviceType = it.deviceType?.Trim();
                    var orderNumber = string.IsNullOrWhiteSpace(it.orderNumber) ? null : it.orderNumber.Trim();
                    var qty = it.qty > 0 ? it.qty : 1;

                    // Check if this is order-style (no serial/IMEI but has deviceType and qty)
                    bool isOrderStyle = string.IsNullOrWhiteSpace(it.serial) &&
                                       string.IsNullOrWhiteSpace(it.imei) &&
                                       !string.IsNullOrWhiteSpace(deviceType);

                    if (isOrderStyle)
                    {
                        // Order-style: Create multiple devices based on quantity
                        for (int i = 0; i < qty; i++)
                        {
                            // Generate a truly unique placeholder serial using GUID
                            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                            var orderTag = string.IsNullOrWhiteSpace(orderNumber) ? "MANUAL" : orderNumber;
                            var placeholderSerial = $"PENDING-{orderTag}-{deviceType}-{uniqueId}";

                            var dev = new Device
                            {
                                Id = Guid.NewGuid(),
                                Source = "NEW",
                                Brand = brand,
                                Model = model,
                                DeviceType = deviceType,
                                OrderNumber = orderNumber,
                                BatchId = batch.BatchId,
                                SerialNumber = placeholderSerial
                            };
                            _db.Devices.Add(dev);
                            batch.Added++;
                            batch.Total++;
                        }
                    }
                    else
                    {
                        // Device-style: Traditional serial/IMEI based import
                        var key = string.IsNullOrWhiteSpace(it.serial) ? it.imei?.Trim() : it.serial?.Trim();
                        if (string.IsNullOrWhiteSpace(key)) { batch.Invalid++; batch.Total++; continue; }
                        if (!seenInUpload.Add(key)) { batch.Duplicates++; batch.Total++; continue; }
                        bool exists = await _db.Devices.AnyAsync(d => d.SerialNumber == key || d.IMEI == key, ct);
                        if (exists) { batch.Duplicates++; batch.Total++; continue; }

                        // Single device per unique key
                        var dev = new Device { Id = Guid.NewGuid(), Source = "NEW", Brand = brand, Model = model, BatchId = batch.BatchId };
                        if (IsImei(key)) dev.IMEI = key; else dev.SerialNumber = key;
                        _db.Devices.Add(dev);

                        batch.Added += 1;
                        batch.Total += 1;
                    }
                }
                
                await _db.SaveChangesAsync(ct);
                bool packUploaded = false;
                if (pack != null)
                {
                    try { var _ = await _docs.SaveForBatchAsync(batch.BatchId, pack, "NEW_HANDOVER", ct); packUploaded = true; }
                    catch { packUploaded = false; }
                }
                return Ok(new { batchId = batch.BatchId, added = batch.Added, duplicates = batch.Duplicates, invalid = batch.Invalid, total = batch.Total, packUploaded });
            }
            catch (Exception ex) { return BadRequest(new { error = ex.Message, details = ex.ToString() }); }
        }

        private static bool IsImei(string s) => s.All(char.IsDigit) && s.Length >= 10;
        private record ManualItem(string? serial, string? imei, string? brand, string? model, string? deviceType, int qty, string? orderNumber = null);
        private record ManualItemsWrapper(List<ManualItem> items);
        [HttpPost("documents")]
        public async Task<IActionResult> UploadDoc(IFormFile file, [FromQuery] string docType = "PO", CancellationToken ct = default)
        {
            try
            {
                var (id, fileName, dt) = await _docs.SaveLooseAsync(file, docType, ct);
                return Ok(new { documentId = id, fileName, docType = dt });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // GET /api/phase0/new/batches
        // Optional filter ?orderNumber=PO-XYZ to list only batches linked to a procurement order.
        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches(int page = 1, int pageSize = 10, string? orderNumber = null)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);

            var batches = _db.Batches.Where(b => b.Source == "NEW");

            if (!string.IsNullOrWhiteSpace(orderNumber))
            {
                var needle = orderNumber.Trim();
                batches = batches.Where(b => b.OrderNumber == needle);
            }

            var query = batches
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new {
                    Id = b.BatchId,
                    b.CreatedAt,
                    UploadedBy = "System",
                    SourceFileName = b.FileName,
                    OrderNumber = b.OrderNumber,
                    Items = _db.Devices.Count(d => d.BatchId == b.BatchId)
                });

            var total = await query.CountAsync();
            var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return Ok(new { total, page, pageSize, rows });
        }

        // GET /api/phase0/new/batches/{id}/items
        [HttpGet("batches/{id}/items")]
        public async Task<IActionResult> GetBatchItems(Guid id, int page = 1, int pageSize = 25, string? q = null)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

            var batch = await _db.Batches.FirstOrDefaultAsync(x => x.BatchId == id && x.Source == "NEW");
            if (batch == null) return NotFound("Batch not found.");

            var itemsQuery = _db.Devices.Where(x => x.BatchId == id);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                itemsQuery = itemsQuery.Where(x =>
                    (x.SerialNumber != null && x.SerialNumber.Contains(q)) ||
                    (x.IMEI != null && x.IMEI.Contains(q)) ||
                    (x.Brand != null && x.Brand.Contains(q)) ||
                    (x.Model != null && x.Model.Contains(q)));
            }

            var total = await itemsQuery.CountAsync();
            var rows = await itemsQuery
                .OrderBy(x => x.Brand).ThenBy(x => x.Model).ThenBy(x => x.SerialNumber).ThenBy(x => x.IMEI)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new {
                    x.Id,
                    Serial = x.SerialNumber,
                    x.IMEI,
                    x.Brand,
                    x.Model,
                    Qty = 1,
                    EMIS = "",
                    CreatedAt = x.ImportedAt
                })
                .ToListAsync();

            var stats = new {
                total,
                brands = await _db.Devices.Where(i => i.BatchId == id && i.Brand != null).Select(i => i.Brand!).Distinct().CountAsync(),
                models = await _db.Devices.Where(i => i.BatchId == id && i.Model != null).Select(i => i.Model!).Distinct().CountAsync(),
                qtySum = await _db.Devices.Where(i => i.BatchId == id).CountAsync()
            };

            return Ok(new {
                batch = new { Id = batch.BatchId, batch.CreatedAt, UploadedBy = "System", SourceFileName = batch.FileName },
                stats,
                page,
                pageSize,
                total,
                rows
            });
        }

        // GET /api/phase0/new/items  (global list)
        [HttpGet("items")]
        public async Task<IActionResult> GetAllNewItems(int page = 1, int pageSize = 25, string? q = null, DateTime? from = null, DateTime? to = null)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

            var items = _db.Devices.Where(i => i.Source == "NEW");
            if (from.HasValue) items = items.Where(i => i.ImportedAt >= DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
            if (to.HasValue)   items = items.Where(i => i.ImportedAt <  DateTime.SpecifyKind(to.Value, DateTimeKind.Utc).AddDays(1));
            if (!string.IsNullOrWhiteSpace(q))
                items = items.Where(i =>
                    (i.SerialNumber != null && i.SerialNumber.Contains(q)) ||
                    (i.IMEI  != null && i.IMEI.Contains(q))   ||
                    (i.Brand != null && i.Brand.Contains(q))  ||
                    (i.Model != null && i.Model.Contains(q)));

            var total = await items.CountAsync();
            var rows = await items.OrderByDescending(i => i.ImportedAt).ThenBy(i => i.Brand).ThenBy(i => i.Model)
                .Skip((page-1)*pageSize).Take(pageSize)
                .Select(i => new {
                    i.Id, i.BatchId, Serial = i.SerialNumber, i.IMEI, i.Brand, i.Model, Qty = 1, EMIS = "", i.ImportedAt,
                    BatchFile = _db.Batches.Where(b => b.BatchId == i.BatchId).Select(b => b.FileName).FirstOrDefault(),
                    BatchAt = _db.Batches.Where(b => b.BatchId == i.BatchId).Select(b => b.CreatedAt).FirstOrDefault()
                }).ToListAsync();

            var stats = new {
                total,
                qtySum  = total,
                brands  = await items.Where(i => i.Brand != null).Select(i => i.Brand!).Distinct().CountAsync(),
                models  = await items.Where(i => i.Model != null).Select(i => i.Model!).Distinct().CountAsync()
            };

            return Ok(new { page, pageSize, total, stats, rows });
        }

        // GET /api/phase0/new/orders - List batches as "orders" for Phase 1 selection
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrdersForPhase1()
        {
            var batches = await _db.Batches
                .Where(b => b.Source == "NEW" && b.Added > 0)
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new {
                    orderId = b.BatchId,
                    orderNumber = b.OrderNumber ?? b.BatchId.ToString().Substring(0, 8),
                    supplierName = !string.IsNullOrEmpty(b.OrderNumber) ? $"Order {b.OrderNumber}" : "Manual Entry",
                    fileName = b.FileName,
                    totalDevices = b.Added,
                    createdAt = b.CreatedAt,
                    status = "Pending Scanning"
                })
                .ToListAsync();

            return Ok(batches);
        }

        // GET /api/phase0/new/orders/{id} - Get specific batch details for Phase 1
        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderDetailsForPhase1(Guid id)
        {
            var batch = await _db.Batches.FirstOrDefaultAsync(b => b.BatchId == id && b.Source == "NEW");
            if (batch == null) return NotFound("Batch not found.");

            var devices = await _db.Devices
                .Where(d => d.BatchId == id)
                .GroupBy(d => new { d.Brand, d.Model, d.DeviceType })
                .Select(g => new {
                    brand = g.Key.Brand,
                    model = g.Key.Model,
                    deviceType = g.Key.DeviceType,
                    quantityExpected = g.Count(),
                    quantityScanned = 0 // Will be updated during Phase 1 scanning
                })
                .ToListAsync();

            return Ok(new {
                orderId = batch.BatchId,
                orderNumber = batch.OrderNumber ?? batch.BatchId.ToString().Substring(0, 8),
                supplierName = !string.IsNullOrEmpty(batch.OrderNumber) ? $"Order {batch.OrderNumber}" : "Manual Entry",
                fileName = batch.FileName,
                totalDevices = batch.Added,
                createdAt = batch.CreatedAt,
                devices
            });
        }

        // GET /api/phase0/new/items/export
        [HttpGet("items/export")]
        public async Task<IActionResult> ExportNewItemsCsv(string? q = null, DateTime? from = null, DateTime? to = null)
        {
            var items = _db.Devices.Where(i => i.Source == "NEW");
            if (from.HasValue)
            {
                var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
                items = items.Where(i => i.ImportedAt >= fromUtc);
            }
            if (to.HasValue)
            {
                var toUtcEnd = new DateTimeOffset(DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)).AddDays(1);
                items = items.Where(i => i.ImportedAt < toUtcEnd);
            }
            if (!string.IsNullOrWhiteSpace(q))
                items = items.Where(i =>
                    (i.SerialNumber != null && i.SerialNumber.Contains(q)) ||
                    (i.IMEI  != null && i.IMEI.Contains(q))   ||
                    (i.Brand != null && i.Brand.Contains(q))  ||
                    (i.Model != null && i.Model.Contains(q)));

            var list = await items.OrderByDescending(i => i.ImportedAt)
                .Select(i => new { i.BatchId, Serial = i.SerialNumber, i.IMEI, i.Brand, i.Model, Qty = 1, EMIS = "", i.ImportedAt })
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BatchId,Serial,IMEI,Brand,Model,Qty,EMIS,ImportedAtUtc");
            foreach (var r in list)
                sb.AppendLine($"{r.BatchId},{r.Serial},{r.IMEI},{r.Brand},{r.Model},{r.Qty},{r.EMIS},{r.ImportedAt.ToUniversalTime():o}");
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"NEW_All_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }
    }
}