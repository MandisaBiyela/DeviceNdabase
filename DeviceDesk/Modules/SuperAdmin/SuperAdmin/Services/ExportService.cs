using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.SuperAdmin.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DeviceDesk.Modules.SuperAdmin.Services;

public class ExportService
{
    private readonly DeviceDeskDbContext _phase0Db;
    private readonly Infrastructure.Data.Phase1DbContext _phase1Db;
    private readonly Phase2DbContext _phase2Db;
    private readonly Phase3DbContext _phase3Db;
    private readonly SuperAdminDbContext _superAdminDb;

    public ExportService(
        DeviceDeskDbContext phase0Db,
        Infrastructure.Data.Phase1DbContext phase1Db,
        Phase2DbContext phase2Db,
        Phase3DbContext phase3Db,
        SuperAdminDbContext superAdminDb)
    {
        _phase0Db = phase0Db;
        _phase1Db = phase1Db;
        _phase2Db = phase2Db;
        _phase3Db = phase3Db;
        _superAdminDb = superAdminDb;
    }

    public async Task<byte[]> ExportDevicesAsync(
        string? phase = null,
        string? stage = null,
        string? zone = null,
        string? school = null,
        string? serial = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string format = "CSV")
    {
        var devices = new List<object>();

        // Phase 0 devices (DeviceDeskDbContext.Devices table - NEW and RNR stock)
        if (phase == null || phase == "Phase0")
        {
            var phase0Query = _phase0Db.Devices.AsNoTracking().AsQueryable();
            if (fromDate != null) phase0Query = phase0Query.Where(d => d.ImportedAt >= fromDate);
            if (toDate != null) phase0Query = phase0Query.Where(d => d.ImportedAt <= toDate);
            if (serial != null) phase0Query = phase0Query.Where(d => d.SerialNumber != null && d.SerialNumber.Contains(serial));
            if (school != null) phase0Query = phase0Query.Where(d => d.SchoolName != null && d.SchoolName.Contains(school));

            var phase0Items = await phase0Query
                .Select(d => new
                {
                    Serial = d.SerialNumber ?? "",
                    Phase = "Phase0",
                    Stage = d.Source ?? "Unknown",
                    Zone = d.Source ?? "",
                    SchoolName = d.SchoolName ?? "",
                    IMEI = d.IMEI ?? "",
                    Brand = d.Brand ?? "",
                    Model = d.Model ?? "",
                    CreatedAt = d.ImportedAt.DateTime
                })
                .ToListAsync();
            devices.AddRange(phase0Items);
        }

        // Phase 1 devices
        if (phase == null || phase == "Phase1")
        {
            var phase1Query = _phase1Db.ReceivingBatchItems.AsQueryable();
            if (fromDate != null) phase1Query = phase1Query.Where(i => i.CreatedAt >= fromDate);
            if (toDate != null) phase1Query = phase1Query.Where(i => i.CreatedAt <= toDate);
            if (serial != null) phase1Query = phase1Query.Where(i => i.SerialNumber!.Contains(serial));

            var phase1Items = await phase1Query
                .Select(i => new
                {
                    Serial = i.SerialNumber ?? "",
                    Phase = "Phase1",
                    Stage = "Received",
                    Zone = "",
                    SchoolName = "",
                    IMEI = i.IMEI ?? "",
                    Brand = i.Brand ?? "",
                    Model = i.Model ?? "",
                    CreatedAt = i.CreatedAt
                })
                .ToListAsync();
            devices.AddRange(phase1Items);
        }

        // Phase 2 devices
        if (phase == null || phase == "Phase2")
        {
            var phase2Query = _phase2Db.Devices.AsQueryable();
            if (stage != null && Enum.TryParse<DeviceDesk.Modules.Phase2.Models.Phase2Stage>(stage, out var stageEnum))
                phase2Query = phase2Query.Where(d => d.Stage == stageEnum);
            if (zone != null && Enum.TryParse<DeviceDesk.Modules.Phase2.Models.Phase2Zone>(zone, out var zoneEnum))
                phase2Query = phase2Query.Where(d => d.Zone == zoneEnum);
            if (school != null) phase2Query = phase2Query.Where(d => d.SchoolName!.Contains(school));
            if (serial != null) phase2Query = phase2Query.Where(d => d.Serial.Contains(serial));
            if (fromDate != null) phase2Query = phase2Query.Where(d => d.CreatedAt >= fromDate);
            if (toDate != null) phase2Query = phase2Query.Where(d => d.CreatedAt <= toDate);

            var phase2Devices = await phase2Query
                .Select(d => new
                {
                    Serial = d.Serial,
                    Phase = "Phase2",
                    Stage = d.Stage.ToString(),
                    Zone = d.Zone.ToString(),
                    SchoolName = d.SchoolName ?? "",
                    IMEI = "",
                    Brand = "",
                    Model = "",
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();
            devices.AddRange(phase2Devices);
        }

        // Phase 3 devices (PODs)
        if (phase == null || phase == "Phase3")
        {
            var phase3Query = _phase3Db.DispatchPODs.AsQueryable();
            if (fromDate != null) phase3Query = phase3Query.Where(p => p.CreatedAt >= fromDate);
            if (toDate != null) phase3Query = phase3Query.Where(p => p.CreatedAt <= toDate);
            if (school != null) phase3Query = phase3Query.Where(p => p.SchoolName.Contains(school));

            var phase3Pods = await phase3Query
                .Select(p => new
                {
                    Serial = p.PODNumber,
                    Phase = "Phase3",
                    Stage = p.Status.ToString(),
                    Zone = "",
                    SchoolName = p.SchoolName,
                    IMEI = "",
                    Brand = "",
                    Model = "",
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();
            devices.AddRange(phase3Pods);
        }

        // SuperAdmin Imported Devices (Siyanda)
        var importedQuery = _superAdminDb.ImportedDevices.AsQueryable();
        if (school != null) importedQuery = importedQuery.Where(d => d.SchoolName != null && d.SchoolName.Contains(school));
        if (serial != null) importedQuery = importedQuery.Where(d => d.Serial.Contains(serial));
        if (fromDate != null) importedQuery = importedQuery.Where(d => d.DateReceived >= fromDate || d.CreatedAt >= fromDate);
        if (toDate != null) importedQuery = importedQuery.Where(d => (d.DateReceived != null && d.DateReceived <= toDate) || d.CreatedAt <= toDate);

        var importedDevices = await importedQuery
            .Select(d => new
            {
                Serial = d.Serial,
                Phase = "Imported",
                Stage = "Siyanda",
                Zone = d.District ?? "",
                SchoolName = d.SchoolName ?? "",
                IMEI = "",
                Brand = "",
                Model = d.ItemDescription ?? "",
                CreatedAt = d.DateReceived ?? d.CreatedAt
            })
            .ToListAsync();
        devices.AddRange(importedDevices);

        return format.ToUpper() switch
        {
            "CSV" => GenerateCsv(devices, "Serial,Phase,Stage,Zone,SchoolName,IMEI,Brand,Model,CreatedAt"),
            "XLSX" => GenerateCsv(devices, "Serial,Phase,Stage,Zone,SchoolName,IMEI,Brand,Model,CreatedAt"), // Placeholder - would use EPPlus
            "PDF" => GenerateCsv(devices, "Serial,Phase,Stage,Zone,SchoolName,IMEI,Brand,Model,CreatedAt"), // Placeholder - would use PDF library
            _ => GenerateCsv(devices, "Serial,Phase,Stage,Zone,SchoolName,IMEI,Brand,Model,CreatedAt")
        };
    }

    public async Task<byte[]> ExportGRVsAsync(DateTime? fromDate = null, DateTime? toDate = null, string format = "CSV")
    {
        var query = _phase1Db.GoodsReceivedNotes.AsQueryable();
        if (fromDate != null) query = query.Where(g => g.CreatedAt >= fromDate);
        if (toDate != null) query = query.Where(g => g.CreatedAt <= toDate);

        var grvs = await query
            .Select(g => new
            {
                g.GRVNumber,
                g.GRVDate,
                g.SupplierName,
                g.OrderNumber,
                g.InvoiceNumber,
                g.TotalQuantity,
                g.ReceivedBy,
                g.VerifiedBy,
                CreatedAt = g.CreatedAt
            })
            .ToListAsync();

        return GenerateCsv(grvs, "GRVNumber,GRVDate,SupplierName,OrderNumber,InvoiceNumber,TotalQuantity,ReceivedBy,VerifiedBy,CreatedAt");
    }

    public async Task<byte[]> ExportPODsAsync(DateTime? fromDate = null, DateTime? toDate = null, string format = "CSV")
    {
        var allPods = new List<object>();

        // Export Phase 3 PODs (newer system)
        var phase3Query = _phase3Db.DispatchPODs.AsNoTracking().AsQueryable();
        if (fromDate != null) phase3Query = phase3Query.Where(p => p.CreatedAt >= fromDate);
        if (toDate != null) phase3Query = phase3Query.Where(p => p.CreatedAt <= toDate);

        var phase3Pods = await phase3Query
            .Select(p => new
            {
                p.PODNumber,
                p.DeliveryNoteNumber,
                p.SchoolName,
                p.District,
                p.EmisCode,
                p.StockType,
                Status = p.Status.ToString(),
                CreatedAt = p.CreatedAt.DateTime
            })
            .ToListAsync();
        allPods.AddRange(phase3Pods);

        // Export Phase 0 PODs (legacy system)
        var phase0Query = _phase0Db.DispatchPods.AsNoTracking().AsQueryable();
        if (fromDate != null) phase0Query = phase0Query.Where(p => p.CreatedAt >= fromDate);
        if (toDate != null) phase0Query = phase0Query.Where(p => p.CreatedAt <= toDate);

        var phase0Pods = await phase0Query
            .Select(p => new
            {
                p.PodNumber,
                p.DeliveryNoteNumber,
                p.SchoolName,
                p.District,
                EmisCode = p.EmisCode ?? "",
                StockType = p.StockType ?? "",
                Status = p.Status.ToString(),
                CreatedAt = p.CreatedAt.DateTime
            })
            .ToListAsync();
        allPods.AddRange(phase0Pods);

        return GenerateCsv(allPods, "PODNumber,DeliveryNoteNumber,SchoolName,District,EmisCode,StockType,Status,CreatedAt");
    }

    public async Task<byte[]> ExportTripsAsync(DateTime? fromDate = null, DateTime? toDate = null, string format = "CSV")
    {
        var allTrips = new List<object>();

        // Export Phase 3 Trips (newer system)
        var phase3Query = _phase3Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt <= toDate);

        var phase3Trips = await phase3Query
            .Select(t => new
            {
                t.TripRef,
                t.DriverName,
                t.VehicleReg,
                Status = t.Status.ToString(),
                t.DriverAccepted,
                t.Completed,
                CreatedAt = t.CreatedAt.DateTime
            })
            .ToListAsync();
        allTrips.AddRange(phase3Trips);

        // Export Phase 0 Trips (legacy system)
        var phase0Query = _phase0Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt <= toDate);

        var phase0Trips = await phase0Query
            .Select(t => new
            {
                t.TripRef,
                t.DriverName,
                t.VehicleReg,
                t.Status,
                DriverAccepted = false,
                Completed = t.CompletedAt.HasValue,
                CreatedAt = t.CreatedAt.DateTime
            })
            .ToListAsync();
        allTrips.AddRange(phase0Trips);

        return GenerateCsv(allTrips, "TripRef,DriverName,VehicleReg,Status,DriverAccepted,Completed,CreatedAt");
    }

    public async Task<byte[]> ExportAuditLogsAsync(
        string? userId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string format = "CSV")
    {
        var allLogs = new List<object>();

        // Export System-wide audit logs
        var systemLogsQuery = _phase0Db.AuditLogs.AsQueryable();
        if (userId != null)
        {
            systemLogsQuery = systemLogsQuery.Where(a => a.UserId.Contains(userId) || (a.UserName != null && a.UserName.Contains(userId)));
        }
        if (action != null)
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
            .Select(a => new
            {
                Id = a.Id.ToString(),
                Timestamp = a.TimestampUtc,
                UserId = a.UserId,
                UserName = a.UserName ?? "",
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId ?? "",
                DeviceId = "",
                DeviceSerial = "",
                Details = a.MetaJson ?? "",
                Source = "System"
            })
            .ToListAsync();

        allLogs.AddRange(systemLogs);

        // Export Phase 2 audit logs
        var phase2LogsQuery = _phase2Db.AuditLogs.AsQueryable();
        if (userId != null) phase2LogsQuery = phase2LogsQuery.Where(a => a.UserId.Contains(userId));
        if (action != null) phase2LogsQuery = phase2LogsQuery.Where(a => a.Action.Contains(action));
        if (fromDate != null) phase2LogsQuery = phase2LogsQuery.Where(a => a.Timestamp >= fromDate);
        if (toDate != null) phase2LogsQuery = phase2LogsQuery.Where(a => a.Timestamp <= toDate.Value.AddDays(1));

        var phase2Logs = await phase2LogsQuery
            .OrderByDescending(a => a.Timestamp)
            .Select(a => new
            {
                Id = a.Id.ToString(),
                Timestamp = a.Timestamp,
                UserId = a.UserId,
                UserName = "",
                Action = a.Action,
                EntityType = "",
                EntityId = "",
                DeviceId = a.DeviceId.HasValue ? a.DeviceId.Value.ToString() : "",
                DeviceSerial = a.DeviceSerial ?? "",
                Details = a.Details ?? "",
                Source = "Phase2"
            })
            .ToListAsync();

        allLogs.AddRange(phase2Logs);

        // Sort by timestamp descending
        var sortedLogs = allLogs.OrderByDescending(l => 
        {
            var prop = l.GetType().GetProperty("Timestamp");
            return prop != null ? (DateTime)prop.GetValue(l)! : DateTime.MinValue;
        }).ToList();

        return GenerateCsv(sortedLogs, "Id,Timestamp,UserId,UserName,Action,EntityType,EntityId,DeviceId,DeviceSerial,Details,Source");
    }

    public async Task<byte[]> ExportSchoolsAsync(string format = "CSV")
    {
        var schools = await _phase0Db.Schools
            .Select(s => new
            {
                s.SchoolId,
                s.EmisCode,
                s.Name,
                s.District,
                s.Cmc,
                s.Circuit,
                s.Address
            })
            .ToListAsync();

        return GenerateCsv(schools, "SchoolId,EmisCode,Name,District,Cmc,Circuit,Address");
    }

    public async Task<byte[]> ExportDriversAsync(DateTime? fromDate = null, DateTime? toDate = null, string format = "CSV")
    {
        var allTrips = new List<(string DriverName, DateTime CreatedAt, bool Completed, string Status)>();

        // Get Phase 3 trips
        var phase3Query = _phase3Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt <= toDate);

        var phase3Trips = await phase3Query
            .Select(t => new { t.DriverName, t.CreatedAt, t.Completed, t.Status })
            .ToListAsync();
        
        foreach (var t in phase3Trips)
        {
            allTrips.Add((t.DriverName, t.CreatedAt.DateTime, t.Completed, t.Status.ToString()));
        }

        // Get Phase 0 trips
        var phase0Query = _phase0Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt <= toDate);

        var phase0Trips = await phase0Query
            .Select(t => new { t.DriverName, t.CreatedAt, t.CompletedAt, t.Status })
            .ToListAsync();
        
        foreach (var t in phase0Trips)
        {
            allTrips.Add((t.DriverName, t.CreatedAt.DateTime, t.CompletedAt.HasValue, t.Status));
        }

        var drivers = allTrips
            .GroupBy(t => t.DriverName)
            .Select(g => new
            {
                DriverName = g.Key,
                TotalTrips = g.Count(),
                CompletedTrips = g.Count(t => t.Completed),
                InTransitTrips = g.Count(t => t.Status.Contains("Transit") || t.Status.Contains("InTransit")),
                PendingTrips = g.Count(t => t.Status.Contains("Pending") || t.Status.Contains("Scheduled")),
                FirstTripDate = g.Min(t => t.CreatedAt),
                LastTripDate = g.Max(t => t.CreatedAt)
            })
            .OrderByDescending(d => d.TotalTrips)
            .ToList();

        return GenerateCsv(drivers, "DriverName,TotalTrips,CompletedTrips,InTransitTrips,PendingTrips,FirstTripDate,LastTripDate");
    }

    public async Task<byte[]> ExportVehiclesAsync(DateTime? fromDate = null, DateTime? toDate = null, string format = "CSV")
    {
        var allTrips = new List<(string VehicleReg, DateTime CreatedAt, bool Completed, string Status)>();

        // Get Phase 3 trips
        var phase3Query = _phase3Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase3Query = phase3Query.Where(t => t.CreatedAt <= toDate);

        var phase3Trips = await phase3Query
            .Select(t => new { t.VehicleReg, t.CreatedAt, t.Completed, t.Status })
            .ToListAsync();
        
        foreach (var t in phase3Trips)
        {
            allTrips.Add((t.VehicleReg, t.CreatedAt.DateTime, t.Completed, t.Status.ToString()));
        }

        // Get Phase 0 trips
        var phase0Query = _phase0Db.DispatchTrips.AsNoTracking().AsQueryable();
        if (fromDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt >= fromDate);
        if (toDate != null) phase0Query = phase0Query.Where(t => t.CreatedAt <= toDate);

        var phase0Trips = await phase0Query
            .Select(t => new { t.VehicleReg, t.CreatedAt, t.CompletedAt, t.Status })
            .ToListAsync();
        
        foreach (var t in phase0Trips)
        {
            allTrips.Add((t.VehicleReg, t.CreatedAt.DateTime, t.CompletedAt.HasValue, t.Status));
        }

        var vehicles = allTrips
            .GroupBy(t => t.VehicleReg)
            .Select(g => new
            {
                VehicleRegistration = g.Key,
                TotalTrips = g.Count(),
                CompletedTrips = g.Count(t => t.Completed),
                InTransitTrips = g.Count(t => t.Status.Contains("Transit") || t.Status.Contains("InTransit")),
                PendingTrips = g.Count(t => t.Status.Contains("Pending") || t.Status.Contains("Scheduled")),
                FirstTripDate = g.Min(t => t.CreatedAt),
                LastTripDate = g.Max(t => t.CreatedAt)
            })
            .OrderByDescending(v => v.TotalTrips)
            .ToList();

        return GenerateCsv(vehicles, "VehicleRegistration,TotalTrips,CompletedTrips,InTransitTrips,PendingTrips,FirstTripDate,LastTripDate");
    }

    private byte[] GenerateCsv<T>(IEnumerable<T> data, string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine(header);

        foreach (var item in data)
        {
            var values = new List<string>();
            var props = typeof(T).GetProperties();
            foreach (var prop in props)
            {
                var value = prop.GetValue(item);
                var str = "";
                
                if (value == null)
                {
                    str = "";
                }
                else if (value is DateTime dt)
                {
                    str = dt.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else if (value is DateTimeOffset dto)
                {
                    str = dto.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else if (value is bool b)
                {
                    str = b ? "Yes" : "No";
                }
                else
                {
                    str = value.ToString() ?? "";
                }
                
                // Escape CSV values
                if (str.Contains(',') || str.Contains('"') || str.Contains('\n') || str.Contains('\r'))
                {
                    str = "\"" + str.Replace("\"", "\"\"") + "\"";
                }
                values.Add(str);
            }
            sb.AppendLine(string.Join(",", values));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

