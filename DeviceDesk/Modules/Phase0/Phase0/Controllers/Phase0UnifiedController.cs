using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0")]
    public class Phase0UnifiedController : ControllerBase
    {
        private readonly DeviceDeskDbContext _db;
        public Phase0UnifiedController(DeviceDeskDbContext db)
        {
            _db = db;
        }

        // GET /api/phase0/devices/{type}  where type = rnr | new
        [HttpGet("devices/{type}")]
        public async Task<IActionResult> GetDevices(string type, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? q = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var source = NormalizeType(type);
            if (source == null) return BadRequest(new { error = "invalid type" });

            var query = _db.Devices.AsNoTracking().Where(d => d.Source == source);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(d =>
                    (d.SerialNumber != null && d.SerialNumber.ToLower().Contains(term)) ||
                    (d.IMEI != null && d.IMEI.ToLower().Contains(term)) ||
                    (d.Brand != null && d.Brand.ToLower().Contains(term)) ||
                    (d.Model != null && d.Model.ToLower().Contains(term))
                );
            }
            if (from.HasValue)
            {
                var fromUtc = new DateTimeOffset(DateTime.SpecifyKind(from.Value, DateTimeKind.Utc));
                query = query.Where(d => d.ImportedAt >= fromUtc);
            }
            if (to.HasValue)
            {
                var toUtc = new DateTimeOffset(DateTime.SpecifyKind(to.Value, DateTimeKind.Utc));
                query = query.Where(d => d.ImportedAt <= toUtc);
            }

            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(d => d.ImportedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    serial = d.SerialNumber,
                    imei = d.IMEI,
                    brand = d.Brand,
                    model = d.Model,
                    qty = 1,
                    importedAt = d.ImportedAt,
                    batchId = d.BatchId,
                    batchFile = _db.Batches.Where(b => b.BatchId == d.BatchId).Select(b => b.FileName).FirstOrDefault()
                })
                .ToListAsync();

            var stats = new
            {
                total,
                brands = await query.Where(d => d.Brand != null).Select(d => d.Brand!).Distinct().CountAsync(),
                models = await query.Where(d => d.Model != null).Select(d => d.Model!).Distinct().CountAsync()
            };

            return Ok(new { page, pageSize, total, stats, rows });
        }

        // GET /api/phase0/batches/{type}  where type = rnr | new
        [HttpGet("batches/{type}")]
        public async Task<IActionResult> GetBatches(string type, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            var source = NormalizeType(type);
            if (source == null) return BadRequest(new { error = "invalid type" });

            var query = _db.Batches.AsNoTracking().Where(b => b.Source == source);
            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    id = b.BatchId,
                    createdAt = b.CreatedAt,
                    uploadedBy = "System",
                    sourceFile = b.FileName,
                    items = _db.Devices.Count(d => d.BatchId == b.BatchId)
                })
                .ToListAsync();

            return Ok(new { page, pageSize, total, rows });
        }

        // GET /api/phase0/batches/{id}/items
        [HttpGet("batches/{id}/items")]
        public async Task<IActionResult> GetBatchItems(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(b => b.BatchId == id);
            if (batch == null) return NotFound(new { error = "batch not found" });

            var query = _db.Devices.AsNoTracking().Where(d => d.BatchId == id);
            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(d => d.ImportedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    serial = d.SerialNumber,
                    imei = d.IMEI,
                    brand = d.Brand,
                    model = d.Model,
                    qty = 1,
                    importedAt = d.ImportedAt,
                    batchId = d.BatchId,
                    batchFile = batch.FileName
                })
                .ToListAsync();

            var stats = new
            {
                total,
                brands = await query.Where(d => d.Brand != null).Select(d => d.Brand!).Distinct().CountAsync(),
                models = await query.Where(d => d.Model != null).Select(d => d.Model!).Distinct().CountAsync()
            };

            return Ok(new { page, pageSize, total, stats, rows });
        }

        // GET /api/phase0/batches/{id}/export
        [HttpGet("batches/{id}/export")]
        public async Task<IActionResult> ExportBatch(Guid id)
        {
            var batch = await _db.Batches.AsNoTracking().FirstOrDefaultAsync(b => b.BatchId == id);
            if (batch == null) return NotFound();

            var rows = await _db.Devices.AsNoTracking().Where(d => d.BatchId == id)
                .OrderBy(d => d.SerialNumber)
                .Select(d => new { d.SerialNumber, d.IMEI, d.Brand, d.Model })
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("SerialNumber,IMEI,Brand,Model");
            foreach (var r in rows)
            {
                sb.AppendLine($"{Escape(r.SerialNumber)},{Escape(r.IMEI)},{Escape(r.Brand)},{Escape(r.Model)}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = (batch.FileName ?? $"batch-{id}").Replace(' ', '_') + "-export.csv";
            return File(bytes, "text/csv", fileName);
        }

        private static string? Escape(string? v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            if (v.Contains(',') || v.Contains('"'))
            {
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }
            return v;
        }

        private static string? NormalizeType(string type)
        {
            type = type.Trim().ToLowerInvariant();
            return type switch
            {
                "rnr" => "RNR",
                "new" => "NEW",
                _ => null
            };
        }
    }
}