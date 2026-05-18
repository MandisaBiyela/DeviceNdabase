using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Infrastructure.Identity;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.SuperAdmin.Models;
using DeviceDesk.Modules.SuperAdmin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DeviceDesk.Modules.SuperAdmin.Controllers;

[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = UserRoles.SuperAdmin)]
public class SuperAdminController : ControllerBase
{
    private readonly SuperAdminService _service;
    private readonly ExportService _exportService;
    private readonly DeviceDeskDbContext _phase0Db;
    private readonly Phase1DbContext _phase1Db;
    private readonly Phase2DbContext _phase2Db;
    private readonly Phase3DbContext _phase3Db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<SuperAdminController> _logger;

    public SuperAdminController(
        SuperAdminService service,
        ExportService exportService,
        DeviceDeskDbContext phase0Db,
        Phase1DbContext phase1Db,
        Phase2DbContext phase2Db,
        Phase3DbContext phase3Db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<SuperAdminController> logger)
    {
        _service = service;
        _exportService = exportService;
        _phase0Db = phase0Db;
        _phase1Db = phase1Db;
        _phase2Db = phase2Db;
        _phase3Db = phase3Db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        try
        {
            // Ask the service for all the other stats
            var stats = await _service.GetDashboardStatsAsync(fromDate, toDate);

            // Force TotalSchools from dbo.Schools
            var schoolCount = await _phase0Db.Schools.CountAsync();
            stats.TotalSchools = schoolCount;

            _logger.LogInformation(
                "[SuperAdmin] Dashboard stats - TotalSchools forced from DB: {Count}", 
                schoolCount
            );

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            var errorDetails = new
            {
                error = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            };
            return StatusCode(500, errorDetails);
        }
    }

    [HttpGet("dashboard/phase0/stats")]
    public async Task<IActionResult> GetPhase0Stats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var stats = await _service.GetPhase0StatsAsync(fromDate, toDate);
        return Ok(stats);
    }

    [HttpGet("dashboard/phase1/stats")]
    public async Task<IActionResult> GetPhase1Stats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var stats = await _service.GetPhase1StatsAsync(fromDate, toDate);
        return Ok(stats);
    }

    [HttpGet("dashboard/phase2/stats")]
    public async Task<IActionResult> GetPhase2Stats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var stats = await _service.GetPhase2StatsAsync(fromDate, toDate);
        return Ok(stats);
    }

    [HttpGet("dashboard/phase2/qastats")]
    public async Task<IActionResult> GetPhase2QAStats()
    {
        var stats = await _service.GetPhase2StatsAsync();
        return Ok(new
        {
            passed = stats.QAPassed,
            failed = stats.QAFailed,
            pending = stats.QAPending
        });
    }

    [HttpGet("dashboard/phase3/stats")]
    public async Task<IActionResult> GetPhase3Stats([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var stats = await _service.GetPhase3StatsAsync(fromDate, toDate);
        return Ok(stats);
    }

    [HttpGet("dashboard/schools")]
    public async Task<IActionResult> GetSchoolStats()
    {
        var stats = await _service.GetSchoolStatsAsync();
        return Ok(stats);
    }

    [HttpGet("dashboard/debug/schools")]
    public async Task<IActionResult> DebugSchoolCount()
    {
        try
        {
            // Test direct query
            var count = await _phase0Db.Schools.CountAsync();
            
            // Test with raw SQL to verify table exists
            var rawCount = 0;
            try
            {
                var connection = _phase0Db.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Schools";
                var result = await command.ExecuteScalarAsync();
                rawCount = result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception sqlEx)
            {
                _logger.LogWarning(sqlEx, "Raw SQL query failed");
            }
            
            var sample = await _phase0Db.Schools
                .Take(5)
                .Select(s => new { s.SchoolId, s.EmisCode, s.Name, s.District })
                .ToListAsync();
            
            // Check database name
            var dbName = _phase0Db.Database.GetDbConnection().Database;
            
            return Ok(new
            {
                totalSchools = count,
                rawSqlCount = rawCount,
                databaseName = dbName,
                sample = sample,
                sampleCount = sample.Count,
                message = count > 0 
                    ? $"Found {count} schools in database {dbName}" 
                    : $"No schools found in database {dbName} (raw SQL count: {rawCount})"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DebugSchoolCount");
            return StatusCode(500, new 
            { 
                error = ex.Message, 
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace 
            });
        }
    }

    [HttpGet("dashboard/drivers")]
    public async Task<IActionResult> GetDriverStats()
    {
        var stats = await _service.GetDriverVehicleStatsAsync();
        return Ok(stats);
    }

    [HttpGet("dashboard/vehicles")]
    public async Task<IActionResult> GetVehicleStats()
    {
        var stats = await _service.GetDriverVehicleStatsAsync();
        return Ok(stats);
    }

    [HttpGet("summaries")]
    public async Task<IActionResult> GetManagementSummaries([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var summary = await _service.GetManagementSummaryAsync(fromDate, toDate);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting management summaries");
            var errorDetails = new
            {
                error = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            };
            return StatusCode(500, errorDetails);
        }
    }

    [HttpGet("dashboard/phase2/detailed-stats")]
    public async Task<ActionResult<Phase2DashboardStatsDto>> GetPhase2DashboardStats(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var stats = await _service.GetPhase2DashboardStatsAsync(from, to, ct);
        return Ok(stats);
    }

    [HttpGet("dashboard/phase2/detailed-summary")]
    public async Task<ActionResult<Phase2ManagementSummaryDto>> GetPhase2DashboardSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var summary = await _service.GetPhase2ManagementSummaryAsync(from, to, ct);
        return Ok(summary);
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? phase = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? zone = null,
        [FromQuery] string? school = null,
        [FromQuery] string? serial = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var devices = new List<DeviceListItemDto>();

        // Phase 1 devices
        if (phase == null || phase == "Phase1")
        {
            var phase1Items = await _phase1Db.ReceivingBatchItems
                .Where(i => (fromDate == null || i.CreatedAt >= fromDate) &&
                           (toDate == null || i.CreatedAt <= toDate) &&
                           (serial == null || i.SerialNumber!.Contains(serial)))
                .Select(i => new DeviceListItemDto
                {
                    Id = 0, // Phase 1 items don't have numeric IDs
                    Serial = i.SerialNumber ?? string.Empty,
                    Phase = "Phase1",
                    Stage = "Received",
                    Zone = string.Empty,
                    SchoolName = null,
                    CreatedAt = i.CreatedAt.DateTime,
                    UpdatedAt = i.CreatedAt.DateTime
                })
                .ToListAsync();
            devices.AddRange(phase1Items);
        }

        // Phase 2 devices
        if (phase == null || phase == "Phase2")
        {
            var phase2Query = _phase2Db.Devices.AsQueryable();
            
            if (stage != null)
            {
                if (Enum.TryParse<DeviceDesk.Modules.Phase2.Models.Phase2Stage>(stage, out var stageEnum))
                {
                    phase2Query = phase2Query.Where(d => d.Stage == stageEnum);
                }
            }
            
            if (zone != null)
            {
                if (Enum.TryParse<DeviceDesk.Modules.Phase2.Models.Phase2Zone>(zone, out var zoneEnum))
                {
                    phase2Query = phase2Query.Where(d => d.Zone == zoneEnum);
                }
            }
            
            if (school != null)
            {
                phase2Query = phase2Query.Where(d => d.SchoolName!.Contains(school));
            }
            
            if (serial != null)
            {
                phase2Query = phase2Query.Where(d => d.Serial.Contains(serial));
            }
            
            if (fromDate != null)
            {
                phase2Query = phase2Query.Where(d => d.CreatedAt >= fromDate);
            }
            
            if (toDate != null)
            {
                phase2Query = phase2Query.Where(d => d.CreatedAt <= toDate);
            }

            var totalCount = await phase2Query.CountAsync();
            var phase2Devices = await phase2Query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DeviceListItemDto
                {
                    Id = d.Id,
                    Serial = d.Serial,
                    Phase = "Phase2",
                    Stage = d.Stage.ToString(),
                    Zone = d.Zone.ToString(),
                    SchoolName = d.SchoolName,
                    CreatedAt = d.CreatedAt,
                    UpdatedAt = d.UpdatedAt
                })
                .ToListAsync();

            if (phase == "Phase2")
            {
                return Ok(new PaginatedResult<DeviceListItemDto>
                {
                    Items = phase2Devices,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
            }

            devices.AddRange(phase2Devices);
        }

        // Phase 3 devices (from PODs)
        if (phase == null || phase == "Phase3")
        {
            var phase3Pods = await _phase3Db.DispatchPODs
                .Where(p => (fromDate == null || p.CreatedAt >= fromDate) &&
                           (toDate == null || p.CreatedAt <= toDate) &&
                           (school == null || p.SchoolName.Contains(school)))
                .Select(p => new DeviceListItemDto
                {
                    Id = 0,
                    Serial = p.PODNumber,
                    Phase = "Phase3",
                    Stage = p.Status.ToString(),
                    Zone = string.Empty,
                    SchoolName = p.SchoolName,
                    CreatedAt = p.CreatedAt.DateTime,
                    UpdatedAt = null
                })
                .ToListAsync();
            devices.AddRange(phase3Pods);
        }

        var total = devices.Count;
        var paged = devices.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PaginatedResult<DeviceListItemDto>
        {
            Items = paged,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("devices/{serial}/lifecycle")]
    public async Task<IActionResult> GetDeviceLifecycle(string serial)
    {
        // Search across all phases
        var lifecycle = new
        {
            Serial = serial,
            Phase0 = await _phase0Db.NewStockScannedDevices
                .Where(i => i.SerialNumber == serial)
                .Select(i => new { BatchId = i.BatchId, CreatedAt = i.ScannedAt })
                .FirstOrDefaultAsync(),
            Phase1 = await _phase1Db.ReceivingBatchItems
                .Where(i => i.SerialNumber == serial)
                .Select(i => new { BatchId = i.ReceivingBatchId, CreatedAt = i.CreatedAt })
                .FirstOrDefaultAsync(),
            Phase2 = await _phase2Db.Devices
                .Where(d => d.Serial == serial)
                .Select(d => new
                {
                    d.Id,
                    d.Stage,
                    d.Zone,
                    d.SchoolName,
                    d.CreatedAt,
                    d.UpdatedAt,
                    d.ReceivingDate,
                    d.PreAssessmentPassed,
                    d.QaPassed
                })
                .FirstOrDefaultAsync(),
            Phase3 = await _phase3Db.DispatchPODs
                .Where(p => p.PODNumber.Contains(serial) || p.DeliveryNoteNumber.Contains(serial))
                .Select(p => new { p.PODId, p.Status, p.SchoolName, p.CreatedAt })
                .FirstOrDefaultAsync()
        };

        return Ok(lifecycle);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var unifiedLogs = new List<UnifiedAuditLogDto>();

            // Query System-wide audit logs (DeviceDeskDbContext)
            try
            {
                var systemLogsQuery = _phase0Db.AuditLogs.AsQueryable();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    systemLogsQuery = systemLogsQuery.Where(a => a.UserId.Contains(userId) || (a.UserName != null && a.UserName.Contains(userId)));
                }
                if (!string.IsNullOrWhiteSpace(action))
                {
                    systemLogsQuery = systemLogsQuery.Where(a => a.Action.Contains(action));
                }
                if (fromDate != null)
                {
                    systemLogsQuery = systemLogsQuery.Where(a => a.TimestampUtc >= fromDate);
                }
                if (toDate != null)
                {
                    systemLogsQuery = systemLogsQuery.Where(a => a.TimestampUtc <= toDate.Value.AddDays(1));
                }

                var systemLogs = await systemLogsQuery
                    .Select(a => new UnifiedAuditLogDto
                    {
                        SystemLogId = a.Id,
                        UserId = a.UserId,
                        UserName = a.UserName,
                        Action = a.Action,
                        EntityType = a.EntityType,
                        EntityId = a.EntityId,
                        MetaJson = a.MetaJson,
                        Timestamp = a.TimestampUtc,
                        Source = "System"
                    })
                    .ToListAsync();

                unifiedLogs.AddRange(systemLogs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying system audit logs, continuing with Phase2 logs only");
            }

            // Query Phase 2 audit logs (Phase2DbContext)
            try
            {
                var phase2LogsQuery = _phase2Db.AuditLogs.AsQueryable();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    phase2LogsQuery = phase2LogsQuery.Where(a => a.UserId.Contains(userId));
                }
                if (!string.IsNullOrWhiteSpace(action))
                {
                    phase2LogsQuery = phase2LogsQuery.Where(a => a.Action.Contains(action));
                }
                if (fromDate != null)
                {
                    phase2LogsQuery = phase2LogsQuery.Where(a => a.Timestamp >= fromDate);
                }
                if (toDate != null)
                {
                    phase2LogsQuery = phase2LogsQuery.Where(a => a.Timestamp <= toDate.Value.AddDays(1));
                }

                var phase2Logs = await phase2LogsQuery
                    .Select(a => new UnifiedAuditLogDto
                    {
                        Id = a.Id,
                        UserId = a.UserId,
                        Action = a.Action,
                        DeviceId = a.DeviceId,
                        DeviceSerial = a.DeviceSerial,
                        Details = a.Details,
                        Timestamp = a.Timestamp,
                        Source = "Phase2"
                    })
                    .ToListAsync();

                unifiedLogs.AddRange(phase2Logs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error querying Phase2 audit logs, continuing with system logs only");
            }

            // Sort by timestamp descending and paginate
            var totalCount = unifiedLogs.Count;
            var pagedLogs = unifiedLogs
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Ok(new
            {
                items = pagedLogs.Select(l => new
                {
                    id = l.Id,
                    systemLogId = l.SystemLogId,
                    timestamp = l.Timestamp,
                    userId = l.UserId,
                    userName = l.UserName,
                    action = l.Action,
                    deviceId = l.DeviceId,
                    deviceSerial = l.DeviceSerial,
                    details = l.Details ?? l.MetaJson,
                    source = l.Source
                }),
                total = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audit logs");
            var errorDetails = new
            {
                error = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            };
            return StatusCode(500, errorDetails);
        }
    }

    [HttpGet("export/datasets")]
    public IActionResult GetAvailableDatasets()
    {
        return Ok(new
        {
            datasets = new[]
            {
                new { id = "devices", name = "Devices", description = "All devices across all phases" },
                new { id = "grvs", name = "GRVs", description = "Goods Received Notes" },
                new { id = "pods", name = "PODs", description = "Proof of Delivery documents" },
                new { id = "trips", name = "Trips", description = "Dispatch trips" },
                new { id = "audit", name = "Audit Logs", description = "System audit logs" },
                new { id = "schools", name = "Schools", description = "School information" },
                new { id = "drivers", name = "Drivers", description = "Driver information" },
                new { id = "vehicles", name = "Vehicles", description = "Vehicle information" }
            }
        });
    }

    [HttpPost("export/generate")]
    public async Task<IActionResult> GenerateExport([FromBody] ExportRequest request)
    {
        try
        {
            byte[] data;
            string fileName;
            string contentType;

            switch (request.Dataset.ToLower())
            {
                case "devices":
                    data = await _exportService.ExportDevicesAsync(
                        request.Filters?.GetValueOrDefault("phase")?.ToString(),
                        request.Filters?.GetValueOrDefault("stage")?.ToString(),
                        request.Filters?.GetValueOrDefault("zone")?.ToString(),
                        request.Filters?.GetValueOrDefault("school")?.ToString(),
                        request.Filters?.GetValueOrDefault("serial")?.ToString(),
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"devices_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "grvs":
                    data = await _exportService.ExportGRVsAsync(
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"grvs_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "pods":
                    data = await _exportService.ExportPODsAsync(
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"pods_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "trips":
                    data = await _exportService.ExportTripsAsync(
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"trips_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "audit":
                    data = await _exportService.ExportAuditLogsAsync(
                        request.Filters?.GetValueOrDefault("userId")?.ToString(),
                        request.Filters?.GetValueOrDefault("action")?.ToString(),
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"audit_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "schools":
                    data = await _exportService.ExportSchoolsAsync(request.Format);
                    fileName = $"schools_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "drivers":
                    data = await _exportService.ExportDriversAsync(
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"drivers_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                case "vehicles":
                    data = await _exportService.ExportVehiclesAsync(
                        request.Filters?.GetValueOrDefault("fromDate") != null 
                            ? DateTime.Parse(request.Filters["fromDate"].ToString()!) 
                            : null,
                        request.Filters?.GetValueOrDefault("toDate") != null 
                            ? DateTime.Parse(request.Filters["toDate"].ToString()!) 
                            : null,
                        request.Format);
                    fileName = $"vehicles_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
                    contentType = "text/csv";
                    break;

                default:
                    return BadRequest(new { error = $"Unknown dataset: {request.Dataset}" });
            }

            return File(data, contentType, fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Export failed: {ex.Message}" });
        }
    }

    [HttpGet("export/devices")]
    public async Task<IActionResult> ExportDevicesDirect(
        [FromQuery] string? phase = null,
        [FromQuery] string? stage = null,
        [FromQuery] string? zone = null,
        [FromQuery] string? school = null,
        [FromQuery] string? serial = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string format = "CSV")
    {
        var data = await _exportService.ExportDevicesAsync(phase, stage, zone, school, serial, fromDate, toDate, format);
        var fileName = $"devices_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(data, "text/csv", fileName);
    }

    [HttpGet("export/audit")]
    public async Task<IActionResult> ExportAuditDirect(
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string format = "CSV")
    {
        var data = await _exportService.ExportAuditLogsAsync(userId, action, fromDate, toDate, format);
        var fileName = $"audit_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(data, "text/csv", fileName);
    }

    // User Management Endpoints
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? role = null, [FromQuery] string? search = null)
    {
        var users = _userManager.Users.AsQueryable();
        
        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            users = users.Where(u => 
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                (u.EmployeeNumber != null && u.EmployeeNumber.ToLower().Contains(search)));
        }

        var result = new List<SuperAdminUserDto>();

        foreach (var user in users.ToList())
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (!string.IsNullOrEmpty(role) && !roles.Contains(role))
                continue;

            var isActive = user.LockoutEnabled == false || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;

            result.Add(new SuperAdminUserDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                EmployeeNumber = user.EmployeeNumber,
                Department = user.Department,
                IsActive = isActive,
                Roles = roles.ToArray(),
                CreatedAt = user.CreatedAt,
                LastLogin = null // Identity doesn't track this by default
            });
        }

        return Ok(result.OrderBy(u => u.FullName));
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var isActive = user.LockoutEnabled == false || user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow;

        var dto = new SuperAdminUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            EmployeeNumber = user.EmployeeNumber,
            Department = user.Department,
            IsActive = isActive,
            Roles = roles.ToArray(),
            CreatedAt = user.CreatedAt,
            LastLogin = null
        };

        return Ok(dto);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null)
            return BadRequest(new { error = "A user with this email already exists." });

        // Validate that all requested roles exist (never create roles - they must be seeded)
        foreach (var roleName in request.Roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                return BadRequest(new { error = $"Role '{roleName}' does not exist. Roles must be seeded and cannot be created through this interface." });
            }
        }

        var password = request.Password ?? GenerateTempPassword();
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            Department = request.Department,
            EmployeeNumber = request.EmployeeNumber,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
            return BadRequest(new { error = "Failed to create user.", errors = createResult.Errors });

        // Add roles
        if (request.Roles.Length > 0)
        {
            var addRoleResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning("Failed to add roles to user {Email}", request.Email);
            }
        }

        // Require password reset if requested
        if (request.RequirePasswordReset)
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
        }

        var dto = new SuperAdminUserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            EmployeeNumber = user.EmployeeNumber,
            Department = user.Department,
            IsActive = !request.RequirePasswordReset,
            Roles = request.Roles,
            CreatedAt = user.CreatedAt
        };

        // Return temp password if it was auto-generated (for SuperAdmin to share securely)
        var response = new 
        { 
            id = dto.Id,
            email = dto.Email,
            fullName = dto.FullName,
            roles = dto.Roles,
            tempPassword = string.IsNullOrEmpty(request.Password) ? password : null
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, response);
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Update basic fields
        user.FullName = request.FullName;
        user.EmployeeNumber = request.EmployeeNumber;
        user.Department = request.Department;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return BadRequest(new { error = "Failed to update user.", errors = updateResult.Errors });

        // Update roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles).ToList();
        var rolesToAdd = request.Roles.Except(currentRoles).ToList();

        if (rolesToRemove.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                _logger.LogWarning("Failed to remove roles from user {Email}", user.Email);
            }
        }

        if (rolesToAdd.Any())
        {
            // Validate that all roles to add exist (never create roles - they must be seeded)
            foreach (var roleName in rolesToAdd)
            {
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    return BadRequest(new { error = $"Role '{roleName}' does not exist. Roles must be seeded and cannot be created through this interface." });
                }
            }

            var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                _logger.LogWarning("Failed to add roles to user {Email}", user.Email);
                return BadRequest(new { error = "Failed to add roles to user.", errors = addResult.Errors });
            }
        }

        return NoContent();
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Prevent deleting yourself
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (user.Id == currentUserId)
            return BadRequest(new { error = "You cannot delete your own account." });

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = "Failed to delete user.", errors = result.Errors });

        return NoContent();
    }

    [HttpPost("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Prevent deactivating yourself
        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (user.Id == currentUserId)
            return BadRequest(new { error = "You cannot deactivate your own account." });

        if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            user.LockoutEnd = null;
        }
        else
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { error = "Failed to update user status.", errors = result.Errors });

        return NoContent();
    }

    [HttpPost("users/{id}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(string id, [FromBody] ChangePasswordRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        
        if (!result.Succeeded)
            return BadRequest(new { error = "Failed to reset password.", errors = result.Errors });

        if (request.RequirePasswordChangeOnNextLogin)
        {
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);
        }

        return NoContent();
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        // Return only assignable roles (read-only list - never modify roles themselves)
        var assignableRoles = new[]
        {
            UserRoles.OrdersClerk,
            UserRoles.ReceivingClerk,
            UserRoles.IctClerk,
            UserRoles.IctInspector,
            UserRoles.IctTechnician,
            UserRoles.IctManager,
            UserRoles.IctAllocator,
            UserRoles.DispatchClerk,
            UserRoles.DispatchDriver,
            UserRoles.DispatchQA,
            UserRoles.DispatchManager,
            UserRoles.Admin,
            UserRoles.SuperAdmin
        };

        var result = new List<RoleDto>();

        // Role descriptions and dashboard mappings
        var roleInfo = new Dictionary<string, (string Description, string Dashboard)>
        {
            { UserRoles.OrdersClerk, ("Manages procurement orders and new stock batches", "/phase0/index.html") },
            { UserRoles.ReceivingClerk, ("Handles device receiving and GRV processing", "/phase1/dashboard.html") },
            { UserRoles.IctClerk, ("Receipts devices and performs initial verification", "/phase2/index.html") },
            { UserRoles.IctInspector, ("Performs pre-assessment and quality checks", "/phase2/index.html") },
            { UserRoles.IctTechnician, ("Conducts detailed inspection and repair routing", "/phase2/index.html") },
            { UserRoles.IctManager, ("Manages ICT operations and approves disposals", "/phase2/index.html") },
            { UserRoles.IctAllocator, ("Assigns storage locations to devices", "/phase2/index.html") },
            { UserRoles.DispatchClerk, ("Manages dispatch operations and PODs", "/dispatch/index.html") },
            { UserRoles.DispatchDriver, ("Handles delivery and proof of delivery", "/dispatch/index.html") },
            { UserRoles.DispatchQA, ("Performs quality checks on dispatch", "/dispatch/index.html") },
            { UserRoles.DispatchManager, ("Oversees dispatch operations", "/dispatch/index.html") },
            { UserRoles.Admin, ("System administrator with elevated permissions", "/admin/dashboard.html") },
            { UserRoles.SuperAdmin, ("Full system access and user management", "/superadmin/dashboard.html") }
        };

        foreach (var roleName in assignableRoles)
        {
            // Only include roles that exist (seeded roles)
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                var (description, dashboard) = roleInfo.GetValueOrDefault(roleName, ("System role", "/"));
                result.Add(new RoleDto
                {
                    Name = roleName,
                    UserCount = usersInRole.Count,
                    Description = description,
                    Dashboard = dashboard
                });
            }
        }

        return Ok(result.OrderBy(r => r.Name));
    }

    [HttpGet("imported-devices")]
    public async Task<ActionResult<ImportedDevicesResultDto>> GetImportedDevices(
        [FromQuery] ImportedDeviceFilterDto filter)
    {
        var result = await _service.GetImportedDevicesAsync(filter);
        return Ok(result);
    }

    [HttpGet("school-devices")]
    public async Task<ActionResult<SchoolDevicesDetailDto>> GetSchoolDevices(
        [FromQuery] long? schoolId,
        [FromQuery] string? emis)
    {
        if (!schoolId.HasValue && string.IsNullOrWhiteSpace(emis))
        {
            return BadRequest("Either schoolId or emis must be provided.");
        }

        var result = await _service.GetSchoolDevicesAsync(schoolId, emis);
        return Ok(result);
    }

    [HttpGet("provincial-analytics")]
    public async Task<ActionResult<ProvincialAnalyticsResultDto>> GetProvincialAnalytics()
    {
        var result = await _service.GetProvincialAnalyticsAsync();
        return Ok(result);
    }

    [HttpPost("imported-devices/reseed")]
    public async Task<IActionResult> ReseedImportedDevices(
        [FromQuery] bool clearExisting = false)
    {
        try
        {
            var result = await _service.ReseedImportedDevicesAsync(clearExisting);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reseeding imported devices");
            return StatusCode(500, new
            {
                error = ex.Message,
                innerException = ex.InnerException?.Message
            });
        }
    }

    private static string GenerateTempPassword()
    {
        var guid = Guid.NewGuid().ToString("N")[..10];
        return $"P@{guid}!";
    }
}

public record ExportRequest(
    string Dataset,
    Dictionary<string, object>? Filters = null,
    string Format = "CSV",
    List<string>? Columns = null
);

