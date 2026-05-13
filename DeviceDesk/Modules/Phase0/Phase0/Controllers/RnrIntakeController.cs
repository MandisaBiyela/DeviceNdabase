using DeviceDesk.Modules.Phase0.Models;
using DeviceDesk.Modules.Phase0.Services;
using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/rnr")]
    public class RnrIntakeController : ControllerBase
    {
        private readonly CsvImportService _csv;
        private readonly DocumentService _docs;
        private readonly DeviceDeskDbContext _db;
        private readonly RnrBatchService _rnrBatchService;
        
        public RnrIntakeController(
            CsvImportService csv, 
            DocumentService docs, 
            DeviceDeskDbContext db,
            RnrBatchService rnrBatchService)
        {
            _csv = csv; 
            _docs = docs; 
            _db = db;
            _rnrBatchService = rnrBatchService;
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
                var result = await _csv.ImportAsync(file, "RNR", ct);
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
                var batch = new DeviceImportBatch { Source = "RNR", FileName = pack?.FileName ?? "manual-entry" };
                _db.Batches.Add(batch);

                var seenInUpload = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in items)
                {
                    var key = string.IsNullOrWhiteSpace(it.serial) ? it.imei?.Trim() : it.serial?.Trim();
                    if (string.IsNullOrWhiteSpace(key)) { batch.Invalid++; batch.Total++; continue; }
                    if (!seenInUpload.Add(key)) { batch.Duplicates++; batch.Total++; continue; }
                    bool exists = await _db.Devices.AnyAsync(d => d.SerialNumber == key || d.IMEI == key, ct);
                    if (exists) { batch.Duplicates++; batch.Total++; continue; }

                    var brand = it.brand?.Trim();
                    var model = it.model?.Trim();

                    // Single device per unique key (ignore Qty when key present)
                    var dev = new Device
                    {
                        Id = Guid.NewGuid(),
                        Source = "RNR",
                        Brand = brand,
                        Model = model,
                        BatchId = batch.BatchId
                    };
                    if (IsImei(key)) dev.IMEI = key; else dev.SerialNumber = key;
                    _db.Devices.Add(dev);

                    batch.Added += 1;
                    batch.Total += 1;
                }

                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Manual Import Error] {ex.InnerException?.Message ?? ex.Message}");
                    Console.WriteLine($"[Manual Import Stack] {ex}");
                    throw new InvalidOperationException($"Database save failed: {ex.InnerException?.Message ?? ex.Message}", ex);
                }

                bool packUploaded = false;
                if (pack != null)
                {
                    try { var _ = await _docs.SaveForBatchAsync(batch.BatchId, pack, "RNR_HANDOVER", ct); packUploaded = true; }
                    catch { packUploaded = false; }
                }

                return Ok(new { batchId = batch.BatchId, added = batch.Added, duplicates = batch.Duplicates, invalid = batch.Invalid, total = batch.Total, packUploaded });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        private static bool IsImei(string s) => s.All(char.IsDigit) && s.Length >= 10;
        private record ManualItem(string? serial, string? imei, string? brand, string? model, int qty);
        private record ManualItemsWrapper(List<ManualItem> items);
        
        /// <summary>
        /// Get available R&R collection slips for Phase 1 receiving
        /// </summary>
        [HttpGet("collection-slips")]
        public async Task<ActionResult<List<RnrBatchDto>>> GetCollectionSlips(CancellationToken ct)
        {
            try
            {
                // Get batches that are pending scan (available for Phase 1)
                var batches = await _rnrBatchService.GetBatchesAsync(RnrBatchStatus.PendingScan, ct);
                return Ok(batches);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to retrieve collection slips", details = ex.Message });
            }
        }
        
        [HttpPost("documents")]
        public async Task<IActionResult> UploadDoc([FromQuery] Guid batchId, IFormFile file, [FromQuery] string docType = "RNR_HANDOVER", CancellationToken ct = default)
        {
            try
            {
                var (id, fileName, dt) = await _docs.SaveForBatchAsync(batchId, file, docType, ct);
                return Ok(new { documentId = id, fileName, docType = dt });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        [HttpGet("batches")]
        public async Task<IActionResult> GetBatches(int page = 1, int pageSize = 10, string? orderNumber = null)
        {
            try
            {
                page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);

                var batches = _db.Batches.Where(b => b.Source == "RNR");
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
                        UploadedBy = "System", // TODO: Add user tracking
                        SourceFileName = b.FileName,
                        OrderNumber = b.OrderNumber,
                        Items = _db.Devices.Count(d => d.BatchId == b.BatchId)
                    });

                var total = await query.CountAsync();
                var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
                return Ok(new { total, page, pageSize, rows });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetBatches Error] {ex.InnerException?.Message ?? ex.Message}");
                Console.WriteLine($"[GetBatches Stack] {ex}");
                return StatusCode(500, new { error = "Failed to load batches", details = ex.InnerException?.Message ?? ex.Message });
            }
        }

        [HttpGet("batches/{id}/items")]
        public async Task<IActionResult> GetBatchItems(Guid id, int page = 1, int pageSize = 25, string? q = null)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

            var batch = await _db.Batches.FirstOrDefaultAsync(x => x.BatchId == id);
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
                    Qty = 1, // Devices are individual items
                    EMIS = "", // TODO: Add EMIS field to Device model if needed
                    CreatedAt = x.ImportedAt
                })
                .ToListAsync();

            // quick stats
            var stats = new {
                total,
                brands = await _db.Devices.Where(i => i.BatchId == id && i.Brand != null).Select(i => i.Brand!).Distinct().CountAsync(),
                models = await _db.Devices.Where(i => i.BatchId == id && i.Model != null).Select(i => i.Model!).Distinct().CountAsync(),
                qtySum = await _db.Devices.Where(i => i.BatchId == id).CountAsync()
            };

            return Ok(new { 
                batch = new { 
                    Id = batch.BatchId, 
                    batch.CreatedAt, 
                    UploadedBy = "System", // TODO: Add user tracking
                    SourceFileName = batch.FileName 
                }, 
                stats, 
                page, 
                pageSize, 
                total, 
                rows 
            });
        }

        [HttpGet("items")]
        public async Task<IActionResult> GetAllRnrItems(int page = 1, int pageSize = 25, string? q = null, DateTime? from = null, DateTime? to = null)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

            var items = _db.Devices.Where(i => i.Source == "RNR");
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
                    i.Id, i.BatchId, Serial = i.SerialNumber, i.IMEI, i.Brand, i.Model, Qty = 1, EMIS = "", ImportedAt = i.ImportedAt,
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

        [HttpGet("items/export")]
        public async Task<IActionResult> ExportRnrItemsCsv(string? q = null, DateTime? from = null, DateTime? to = null)
        {
            var items = _db.Devices.Where(i => i.Source == "RNR");
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
                .Select(i => new { i.BatchId, Serial = i.SerialNumber, i.IMEI, i.Brand, i.Model, Qty = 1, EMIS = "", ImportedAt = i.ImportedAt })
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("BatchId,Serial,IMEI,Brand,Model,Qty,EMIS,ImportedAtUtc");
            foreach (var r in list)
                sb.AppendLine($"{r.BatchId},{r.Serial},{r.IMEI},{r.Brand},{r.Model},{r.Qty},{r.EMIS},{r.ImportedAt.ToUniversalTime():o}");
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"RNR_All_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv");
        }
    }
}