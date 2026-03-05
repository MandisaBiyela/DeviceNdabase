using System.Security.Cryptography;
using DeviceDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceDesk.Modules.Phase0.Controllers
{
    [ApiController]
    [Route("api/phase0/readiness")]
    public class ReadinessController : ControllerBase
    {
        private readonly DeviceDeskDbContext _db;
        private readonly IWebHostEnvironment _env;
        public ReadinessController(DeviceDeskDbContext db, IWebHostEnvironment env)
        {
            _db = db; _env = env;
        }

        public record CreateReportDto(string EmisCode, string SchoolName, string? District, string SubmittedByUserId);
        public record PatchReportDto(ReadinessState? State, DateTimeOffset? SubmittedAt);
        public record CreateRoomDto(string RoomCode, string RoomName, int Index = 0);
        public record UpsertItemDto(string ChecklistKey, bool Value, string? Notes, IssueSeverity? Severity);
        public record PatchEvidenceDto(string? Caption, bool? IsPrimary, bool? ForReview);

        [HttpPost("reports")] // create new report
        public async Task<IActionResult> CreateReport([FromBody] CreateReportDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.EmisCode) || string.IsNullOrWhiteSpace(dto.SchoolName))
                return BadRequest(new { error = "EMIS and SchoolName required" });

            var r = new ReadinessReport
            {
                EmisCode = dto.EmisCode.Trim(),
                SchoolName = dto.SchoolName.Trim(),
                District = dto.District?.Trim() ?? string.Empty,
                SubmittedByUserId = dto.SubmittedByUserId,
                State = ReadinessState.Draft,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.ReadinessReports.Add(r);
            await _db.SaveChangesAsync();
            return Ok(new { id = r.Id, r.EmisCode, r.SchoolName, r.District, r.State, r.CreatedAt });
        }

        // GET /api/phase0/readiness/reports?emis=700123&state=5&page=1&pageSize=25
        [HttpGet("reports")] // list reports, optional EMIS/state filter
        public async Task<IActionResult> ListReports([FromQuery] string? emis, [FromQuery] ReadinessState? state, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = _db.ReadinessReports.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(emis))
                q = q.Where(r => r.EmisCode == emis.Trim());
            if (state.HasValue)
                q = q.Where(r => r.State == state.Value);

            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.EmisCode,
                    r.SchoolName,
                    r.State,
                    r.CreatedAt,
                    r.SubmittedAt,
                    Rooms = _db.ReadinessRooms.Count(x => x.ReportId == r.Id),
                    Evidence = _db.ReadinessEvidence.Count(x => x.ReportId == r.Id)
                })
                .ToListAsync();

            return Ok(new { page, pageSize, total, rows });
        }

        // GET /api/phase0/readiness/reports/{id}
        [HttpGet("reports/{reportId:guid}")] // report detail with rooms and items
        public async Task<IActionResult> GetReport(Guid reportId)
        {
            var r = await _db.ReadinessReports.AsNoTracking().FirstOrDefaultAsync(x => x.Id == reportId);
            if (r == null) return NotFound(new { error = "report not found" });

            var rooms = await _db.ReadinessRooms.AsNoTracking()
                .Where(x => x.ReportId == reportId)
                .OrderBy(x => x.Index)
                .Select(x => new { x.Id, x.RoomCode, x.RoomName, x.Index })
                .ToListAsync();

            var roomIds = rooms.Select(x => x.Id).ToList();
            var items = await _db.ReadinessRoomItems.AsNoTracking()
                .Where(i => roomIds.Contains(i.RoomId))
                .Select(i => new { i.RoomId, i.ChecklistKey, i.Value, i.Notes, i.Severity })
                .ToListAsync();

            var evCount = await _db.ReadinessEvidence.AsNoTracking().CountAsync(e => e.ReportId == reportId);

            var roomsWithItems = rooms.Select(room => new
            {
                id = room.Id,
                code = room.RoomCode,
                name = room.RoomName,
                index = room.Index,
                items = items.Where(it => it.RoomId == room.Id)
            });

            return Ok(new
            {
                report = new
                {
                    r.Id,
                    r.EmisCode,
                    r.SchoolName,
                    r.District,
                    r.State,
                    r.CreatedAt,
                    r.SubmittedAt
                },
                rooms = roomsWithItems,
                evidenceCount = evCount
            });
        }

        // PATCH /api/phase0/readiness/reports/{id}
        [HttpPatch("reports/{id:guid}")]
        public async Task<IActionResult> PatchReport(Guid id, [FromBody] PatchReportDto dto)
        {
            var report = await _db.ReadinessReports.FirstOrDefaultAsync(x => x.Id == id);
            if (report == null) return NotFound(new { error = "report not found" });

            if (dto.State.HasValue)
            {
                report.State = dto.State.Value;
                if (dto.State == ReadinessState.Submitted || dto.State == ReadinessState.NeedsReview)
                {
                    report.SubmittedAt = dto.SubmittedAt ?? DateTimeOffset.UtcNow;
                }
            }
            report.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { id = report.Id, report.State, report.SubmittedAt, report.UpdatedAt });
        }

        [HttpPost("reports/{reportId:guid}/rooms")] // add room to report
        public async Task<IActionResult> AddRoom(Guid reportId, [FromBody] CreateRoomDto dto)
        {
            var report = await _db.ReadinessReports.FirstOrDefaultAsync(x => x.Id == reportId);
            if (report == null) return NotFound(new { error = "report not found" });

            var exists = await _db.ReadinessRooms.AnyAsync(x => x.ReportId == reportId && x.RoomCode == dto.RoomCode);
            if (exists) return Conflict(new { error = "room code exists" });

            var room = new ReadinessRoom
            {
                ReportId = reportId,
                RoomCode = dto.RoomCode.Trim(),
                RoomName = dto.RoomName.Trim(),
                Index = dto.Index,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.ReadinessRooms.Add(room);
            await _db.SaveChangesAsync();
            return Ok(new { id = room.Id, room.RoomCode, room.RoomName, room.Index });
        }

        [HttpPost("rooms/{roomId:guid}/items")] // upsert item in a room by checklistKey
        public async Task<IActionResult> UpsertItem(Guid roomId, [FromBody] UpsertItemDto dto)
        {
            var room = await _db.ReadinessRooms.FirstOrDefaultAsync(x => x.Id == roomId);
            if (room == null) return NotFound(new { error = "room not found" });

            var key = dto.ChecklistKey.Trim();
            var item = await _db.ReadinessRoomItems.FirstOrDefaultAsync(x => x.RoomId == roomId && x.ChecklistKey == key);
            if (item == null)
            {
                item = new ReadinessRoomItem
                {
                    RoomId = roomId,
                    ChecklistKey = key,
                    Value = dto.Value,
                    Notes = dto.Notes,
                    Severity = dto.Severity,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _db.ReadinessRoomItems.Add(item);
            }
            else
            {
                item.Value = dto.Value; item.Notes = dto.Notes; item.Severity = dto.Severity; item.UpdatedAt = DateTimeOffset.UtcNow;
            }
            await _db.SaveChangesAsync();
            return Ok(new { id = item.Id, item.ChecklistKey, item.Value, item.Notes, item.Severity });
        }

        [HttpPost("reports/{reportId:guid}/evidence")] // multipart form upload
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadEvidence(Guid reportId, [FromForm] EvidenceKind kind, [FromForm] string? caption, [FromForm] Guid? roomId, [FromForm] Guid? roomItemId, [FromForm] DateTimeOffset? takenAt, [FromForm] double? gpsLat, [FromForm] double? gpsLng, [FromForm] IFormFile file)
        {
            var report = await _db.ReadinessReports.FirstOrDefaultAsync(x => x.Id == reportId);
            if (report == null) return NotFound(new { error = "report not found" });
            if (file == null || file.Length == 0) return BadRequest(new { error = "file required" });

            ReadinessRoom? room = null; ReadinessRoomItem? roomItem = null;
            if (roomId.HasValue)
            {
                room = await _db.ReadinessRooms.FirstOrDefaultAsync(x => x.Id == roomId.Value && x.ReportId == reportId);
                if (room == null) return BadRequest(new { error = "invalid roomId" });
            }
            if (roomItemId.HasValue)
            {
                roomItem = await _db.ReadinessRoomItems.FirstOrDefaultAsync(x => x.Id == roomItemId.Value);
                if (roomItem == null || (room != null && roomItem.RoomId != room.Id)) return BadRequest(new { error = "invalid roomItemId" });
            }

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
            }
            string sha;
            using (var sha256 = SHA256.Create())
            {
                sha = Convert.ToHexString(sha256.ComputeHash(bytes));
            }

            var dup = await _db.ReadinessEvidence.AnyAsync(x => x.ReportId == reportId && x.Sha256 == sha);
            if (dup) return Conflict(new { error = "duplicate evidence (sha256)" });

            var dt = DateTimeOffset.UtcNow;
            var yyyy = dt.UtcDateTime.Year.ToString();
            var mm = dt.UtcDateTime.Month.ToString("00");
            var root = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "evidence", report.EmisCode, yyyy, mm);
            Directory.CreateDirectory(root);
            var safeName = Path.GetFileName(file.FileName).Replace(' ', '_');
            var storedName = $"{Guid.NewGuid():N}_{safeName}";
            var relPath = Path.Combine("evidence", report.EmisCode, yyyy, mm, storedName);
            var absPath = Path.Combine(root, storedName);
            await System.IO.File.WriteAllBytesAsync(absPath, bytes);

            var ev = new ReadinessEvidence
            {
                ReportId = reportId,
                RoomId = room?.Id,
                RoomItemId = roomItem?.Id,
                Kind = kind,
                StoragePath = relPath.Replace("\\", "/"),
                ContentType = file.ContentType ?? "application/octet-stream",
                SizeBytes = bytes.LongLength,
                Caption = caption,
                IsPrimary = false,
                ForReview = false,
                Sha256 = sha,
                TakenAt = takenAt ?? DateTimeOffset.UtcNow,
                GpsLat = gpsLat,
                GpsLng = gpsLng,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.ReadinessEvidence.Add(ev);
            await _db.SaveChangesAsync();

            var url = $"/{ev.StoragePath}";
            return Ok(new { id = ev.Id, ev.Kind, ev.Caption, ev.IsPrimary, ev.ForReview, ev.StoragePath, url, ev.ContentType, ev.SizeBytes, ev.TakenAt });
        }

        [HttpGet("reports/{reportId:guid}/evidence")] // list evidence for report
        public async Task<IActionResult> ListEvidence(Guid reportId)
        {
            var rows = await _db.ReadinessEvidence.AsNoTracking().Where(x => x.ReportId == reportId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { id = x.Id, x.Kind, x.Caption, x.IsPrimary, x.ForReview, x.RoomId, x.RoomItemId, x.TakenAt, url = "/" + x.StoragePath })
                .ToListAsync();
            return Ok(new { total = rows.Count, rows });
        }

        [HttpPatch("evidence/{id:guid}")] // update caption/flags
        public async Task<IActionResult> PatchEvidence(Guid id, [FromBody] PatchEvidenceDto dto)
        {
            var ev = await _db.ReadinessEvidence.FirstOrDefaultAsync(x => x.Id == id);
            if (ev == null) return NotFound(new { error = "evidence not found" });
            if (dto.Caption != null) ev.Caption = dto.Caption;
            if (dto.IsPrimary.HasValue) ev.IsPrimary = dto.IsPrimary.Value;
            if (dto.ForReview.HasValue) ev.ForReview = dto.ForReview.Value;
            ev.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(new { ok = true });
        }
    }
}