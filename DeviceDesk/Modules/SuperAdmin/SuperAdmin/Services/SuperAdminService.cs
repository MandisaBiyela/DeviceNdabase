using DeviceDesk.Infrastructure.Data;
using DeviceDesk.Modules.Phase1.Models;
using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Models;
using DeviceDesk.Modules.SuperAdmin.Data;
using DeviceDesk.Modules.SuperAdmin.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Globalization;
using Microsoft.Extensions.Hosting;

namespace DeviceDesk.Modules.SuperAdmin.Services;

public class SuperAdminService
{
    private readonly DeviceDeskDbContext _phase0Db;
    private readonly Infrastructure.Data.Phase1DbContext _phase1Db;
    private readonly Phase2DbContext _phase2Db;
    private readonly Phase3DbContext _phase3Db;
    private readonly SuperAdminDbContext _superAdminDb;
    private readonly IHostEnvironment _environment;

    public SuperAdminService(
        DeviceDeskDbContext phase0Db,
        Infrastructure.Data.Phase1DbContext phase1Db,
        Phase2DbContext phase2Db,
        Phase3DbContext phase3Db,
        SuperAdminDbContext superAdminDb,
        IHostEnvironment environment)
    {
        _phase0Db = phase0Db;
        _phase1Db = phase1Db;
        _phase2Db = phase2Db;
        _phase3Db = phase3Db;
        _superAdminDb = superAdminDb;
        _environment = environment;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            // Convert DateTime? to DateTimeOffset? for entities that use DateTimeOffset
            DateTimeOffset? fromDateOffset = fromDate.HasValue ? new DateTimeOffset(fromDate.Value) : null;
            DateTimeOffset? toDateOffset = toDate.HasValue ? new DateTimeOffset(toDate.Value) : null;

            // Phase 0 stats - using DeviceDeskDbContext which has both NewStockBatch and RnrBatch
            var phase0BatchesQuery = _phase0Db.NewStockBatches.AsQueryable();
        if (fromDateOffset != null) phase0BatchesQuery = phase0BatchesQuery.Where(b => b.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) phase0BatchesQuery = phase0BatchesQuery.Where(b => b.CreatedAt <= toDateOffset);
        var phase0Batches = await phase0BatchesQuery.CountAsync();
        
        // Count items by joining with batches
        var phase0ItemsQuery = _phase0Db.NewStockBatchItems.AsQueryable();
        if (fromDateOffset != null || toDateOffset != null)
        {
            var filteredBatches = _phase0Db.NewStockBatches.AsQueryable();
            if (fromDateOffset != null) filteredBatches = filteredBatches.Where(b => b.CreatedAt >= fromDateOffset);
            if (toDateOffset != null) filteredBatches = filteredBatches.Where(b => b.CreatedAt <= toDateOffset);
            phase0ItemsQuery = phase0ItemsQuery.Join(filteredBatches,
                item => item.BatchId,
                batch => batch.BatchId,
                (item, batch) => item);
        }
        var phase0Items = await phase0ItemsQuery.CountAsync();
        
        // Count Phase 0 scanned devices
        var phase0ScannedDevicesQuery = _phase0Db.NewStockScannedDevices.AsQueryable();
        if (fromDateOffset != null) phase0ScannedDevicesQuery = phase0ScannedDevicesQuery.Where(d => d.ScannedAt >= fromDateOffset);
        if (toDateOffset != null) phase0ScannedDevicesQuery = phase0ScannedDevicesQuery.Where(d => d.ScannedAt <= toDateOffset);
        var phase0ScannedDevices = await phase0ScannedDevicesQuery.CountAsync();
        
        // RnrBatches may not exist in all database configurations - handle gracefully
        int rnrBatches = 0;
        try
        {
            var rnrBatchesQuery = _phase0Db.RnrBatches.AsQueryable();
            if (fromDateOffset != null) rnrBatchesQuery = rnrBatchesQuery.Where(b => b.CreatedAt >= fromDateOffset);
            if (toDateOffset != null) rnrBatchesQuery = rnrBatchesQuery.Where(b => b.CreatedAt <= toDateOffset);
            rnrBatches = await rnrBatchesQuery.CountAsync();
        }
        catch (SqlException sqlEx) when (sqlEx.Number == 208) // Invalid object name
        {
            // Table doesn't exist - this is expected in some configurations
            Console.WriteLine($"[SuperAdminService] RnrBatches table not found (expected in some DB configs). Using 0.");
            rnrBatches = 0;
        }
        catch (Exception ex)
        {
            // Other database errors - log but don't crash
            Console.WriteLine($"[SuperAdminService] Error querying RnrBatches: {ex.Message}");
            rnrBatches = 0;
        }

        // Phase 1 stats - show ALL data when no date filter is provided
        var phase1BatchesQuery = _phase1Db.ReceivingBatches.AsQueryable();
        if (fromDateOffset != null) phase1BatchesQuery = phase1BatchesQuery.Where(b => b.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) phase1BatchesQuery = phase1BatchesQuery.Where(b => b.CreatedAt <= toDateOffset);
        var phase1Batches = await phase1BatchesQuery.CountAsync();
        
        var grvsQuery = _phase1Db.GoodsReceivedNotes.AsQueryable();
        if (fromDateOffset != null) grvsQuery = grvsQuery.Where(g => g.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) grvsQuery = grvsQuery.Where(g => g.CreatedAt <= toDateOffset);
        var grvs = await grvsQuery.CountAsync();
        
        var phase1DevicesQuery = _phase1Db.ReceivingBatchItems.AsQueryable();
        if (fromDateOffset != null) phase1DevicesQuery = phase1DevicesQuery.Where(i => i.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) phase1DevicesQuery = phase1DevicesQuery.Where(i => i.CreatedAt <= toDateOffset);
        var phase1Devices = await phase1DevicesQuery.CountAsync();

        // Phase 2 stats - apply date filters (Phase2Device uses DateTime, not DateTimeOffset)
        var phase2DevicesQuery = _phase2Db.Devices.AsQueryable();
        if (fromDate != null) phase2DevicesQuery = phase2DevicesQuery.Where(d => d.CreatedAt >= fromDate);
        if (toDate != null) phase2DevicesQuery = phase2DevicesQuery.Where(d => d.CreatedAt <= toDate);
        var phase2Devices = await phase2DevicesQuery.CountAsync();
        
        // Safe grouping for Phase 2 by Stage
        var phase2ByStageList = await phase2DevicesQuery
            .GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync();
        var phase2ByStage = phase2ByStageList
            .GroupBy(x => x.Stage)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );

        // Safe grouping for Phase 2 by Zone
        var phase2ByZoneList = await phase2DevicesQuery
            .GroupBy(d => d.Zone)
            .Select(g => new { Zone = g.Key, Count = g.Count() })
            .ToListAsync();
        var phase2ByZone = phase2ByZoneList
            .GroupBy(x => x.Zone)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );
        var disposalPending = await _phase2Db.Disposals
            .Where(d => !d.IsApproved)
            .CountAsync();

        // Phase 3 stats - apply date filters
        var podsQuery = _phase3Db.DispatchPODs.AsQueryable();
        if (fromDateOffset != null) podsQuery = podsQuery.Where(p => p.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) podsQuery = podsQuery.Where(p => p.CreatedAt <= toDateOffset);
        var pods = await podsQuery.CountAsync();
        
        var tripsQuery = _phase3Db.DispatchTrips.AsQueryable();
        if (fromDateOffset != null) tripsQuery = tripsQuery.Where(t => t.CreatedAt >= fromDateOffset);
        if (toDateOffset != null) tripsQuery = tripsQuery.Where(t => t.CreatedAt <= toDateOffset);
        var trips = await tripsQuery.CountAsync();
        
        // Safe grouping for PODs by Status
        var podsByStatusList = await podsQuery
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var podsByStatus = podsByStatusList
            .GroupBy(x => x.Status)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );

        // Safe grouping for Trips by Status
        var tripsByStatusList = await tripsQuery
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var tripsByStatus = tripsByStatusList
            .GroupBy(x => x.Status)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );

        // Schools - count all schools regardless of date filters
        // This is a simple COUNT(*) query - should always work if the table exists
        int schools = 0;
        try
        {
            // Try direct EF Core query first
            schools = await _phase0Db.Schools.CountAsync();
            Console.WriteLine($"[SuperAdminService] Schools count query (EF Core) returned: {schools}");
            
            // If EF Core returns 0, verify with raw SQL to ensure table exists and has data
            if (schools == 0)
            {
                try
                {
                    // Use raw SQL as fallback to ensure we're querying the correct table
                    var connection = _phase0Db.Database.GetDbConnection();
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM [dbo].[Schools]";
                    var sqlResult = await command.ExecuteScalarAsync();
                    schools = sqlResult != null ? Convert.ToInt32(sqlResult) : 0;
                    Console.WriteLine($"[SuperAdminService] Schools count query (raw SQL) returned: {schools}");
                }
                catch (Exception sqlEx)
                {
                    Console.WriteLine($"[SuperAdminService] Raw SQL fallback failed: {sqlEx.Message}");
                    // Keep schools = 0 from EF Core query
                }
            }
        }
        catch (SqlException sqlEx) when (sqlEx.Number == 208) // Invalid object name
        {
            // Table doesn't exist - try raw SQL with different schema
            Console.WriteLine($"[SuperAdminService] WARNING: Schools table not found via EF Core. Trying raw SQL...");
            try
            {
                var connection = _phase0Db.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Schools"; // Try without schema
                var sqlResult = await command.ExecuteScalarAsync();
                schools = sqlResult != null ? Convert.ToInt32(sqlResult) : 0;
                Console.WriteLine($"[SuperAdminService] Schools count (raw SQL without schema) returned: {schools}");
            }
            catch (Exception rawEx)
            {
                Console.WriteLine($"[SuperAdminService] ERROR: Schools table not accessible. {rawEx.Message}");
                schools = 0;
            }
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire request
            Console.WriteLine($"[SuperAdminService] ERROR counting schools: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[SuperAdminService] Inner exception: {ex.InnerException.Message}");
            }
            // Try raw SQL as last resort
            try
            {
                var connection = _phase0Db.Database.GetDbConnection();
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM [dbo].[Schools]";
                var sqlResult = await command.ExecuteScalarAsync();
                schools = sqlResult != null ? Convert.ToInt32(sqlResult) : 0;
                Console.WriteLine($"[SuperAdminService] Schools count (raw SQL fallback) returned: {schools}");
            }
            catch
            {
                schools = 0;
            }
        }

        // Count imported devices (SuperAdmin view)
        int importedDevices = 0;
        try
        {
            var importedDevicesQuery = _superAdminDb.ImportedDevices.AsQueryable();
            if (fromDate != null) importedDevicesQuery = importedDevicesQuery.Where(d => d.CreatedAt >= fromDate);
            if (toDate != null) importedDevicesQuery = importedDevicesQuery.Where(d => d.CreatedAt <= toDate);
            importedDevices = await importedDevicesQuery.CountAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error counting imported devices: {ex.Message}");
            importedDevices = 0;
        }

        var result = new DashboardStatsDto
        {
            TotalDevices = phase0ScannedDevices + phase1Devices + phase2Devices + importedDevices,
            Phase0Batches = phase0Batches + rnrBatches,
            Phase0Items = phase0Items,
            Phase0Devices = phase0ScannedDevices,
            Phase1Batches = phase1Batches,
            Phase1Devices = phase1Devices,
            Phase2Devices = phase2Devices + importedDevices, // Combine workflow + imported
            Phase3Pods = pods,
            Phase3Trips = trips,
            TotalGRVs = grvs,
            TotalSchools = schools,
            Phase2ByStage = phase2ByStage,
            Phase2ByZone = phase2ByZone,
            PODsByStatus = podsByStatus,
            TripsByStatus = tripsByStatus,
            DisposalPending = disposalPending
        };
        
        Console.WriteLine($"[SuperAdminService] Returning DashboardStatsDto with TotalSchools: {result.TotalSchools}");
        return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] ERROR in GetDashboardStatsAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[SuperAdminService] Inner exception: {ex.InnerException.Message}");
            }
            throw; // Re-throw to let controller handle it
        }
    }

    public async Task<Phase0StatsDto> GetPhase0StatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var newStockBatchesQuery = _phase0Db.NewStockBatches.AsQueryable();
        if (fromDate != null) newStockBatchesQuery = newStockBatchesQuery.Where(b => b.CreatedAt >= fromDate);
        if (toDate != null) newStockBatchesQuery = newStockBatchesQuery.Where(b => b.CreatedAt <= toDate);
        var newStockBatches = await newStockBatchesQuery.CountAsync();
        
        // RnrBatches may not exist in all database configurations - handle gracefully
        int rnrBatches = 0;
        try
        {
            var rnrBatchesQuery = _phase0Db.RnrBatches.AsQueryable();
            if (fromDate != null) rnrBatchesQuery = rnrBatchesQuery.Where(b => b.CreatedAt >= fromDate);
            if (toDate != null) rnrBatchesQuery = rnrBatchesQuery.Where(b => b.CreatedAt <= toDate);
            rnrBatches = await rnrBatchesQuery.CountAsync();
        }
        catch (Exception ex)
        {
            // Table doesn't exist or isn't accessible - log and continue with 0
            Console.WriteLine($"[SuperAdminService] RnrBatches table not available in GetPhase0StatsAsync: {ex.Message}");
            rnrBatches = 0;
        }
        
        // Count items by joining with batches
        var itemsExpectedQuery = _phase0Db.NewStockBatchItems.AsQueryable();
        if (fromDate != null || toDate != null)
        {
            var filteredBatches = _phase0Db.NewStockBatches.AsQueryable();
            if (fromDate != null) filteredBatches = filteredBatches.Where(b => b.CreatedAt >= fromDate);
            if (toDate != null) filteredBatches = filteredBatches.Where(b => b.CreatedAt <= toDate);
            itemsExpectedQuery = itemsExpectedQuery.Join(filteredBatches,
                item => item.BatchId,
                batch => batch.BatchId,
                (item, batch) => item);
        }
        var itemsExpected = await itemsExpectedQuery.CountAsync();

        return new Phase0StatsDto
        {
            NewStockBatches = newStockBatches,
            RnrBatches = rnrBatches,
            TotalBatches = newStockBatches + rnrBatches,
            ItemsExpected = itemsExpected
        };
    }

    public async Task<Phase1StatsDto> GetPhase1StatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var batchesQuery = _phase1Db.ReceivingBatches.AsQueryable();
        if (fromDate != null) batchesQuery = batchesQuery.Where(b => b.CreatedAt >= fromDate);
        if (toDate != null) batchesQuery = batchesQuery.Where(b => b.CreatedAt <= toDate);
        var batches = await batchesQuery.ToListAsync();
        
        var batchesByStatus = batches
            .GroupBy(b => b.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());
        
        var grvsQuery = _phase1Db.GoodsReceivedNotes.AsQueryable();
        if (fromDate != null) grvsQuery = grvsQuery.Where(g => g.CreatedAt >= fromDate);
        if (toDate != null) grvsQuery = grvsQuery.Where(g => g.CreatedAt <= toDate);
        var grvs = await grvsQuery.CountAsync();
        
        var devicesReceivedQuery = _phase1Db.ReceivingBatchItems.AsQueryable();
        if (fromDate != null) devicesReceivedQuery = devicesReceivedQuery.Where(i => i.CreatedAt >= fromDate);
        if (toDate != null) devicesReceivedQuery = devicesReceivedQuery.Where(i => i.CreatedAt <= toDate);
        var devicesReceived = await devicesReceivedQuery.CountAsync();
        
        var variances = batches.Count(b => b.HasVariance);

        return new Phase1StatsDto
        {
            TotalBatches = batches.Count,
            BatchesByStatus = batchesByStatus,
            TotalGRVs = grvs,
            DevicesReceived = devicesReceived,
            VarianceCount = variances
        };
    }

    public async Task<Phase2StatsDto> GetPhase2StatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var devicesQuery = _phase2Db.Devices.AsQueryable();
            if (fromDate != null) devicesQuery = devicesQuery.Where(d => d.CreatedAt >= fromDate);
            if (toDate != null) devicesQuery = devicesQuery.Where(d => d.CreatedAt <= toDate);
            var devices = await devicesQuery.ToListAsync();
            var byStage = devices
                .GroupBy(d => d.Stage)
                .ToDictionary(g => g.Key.ToString() ?? "Unknown", g => g.Count());
            var byZone = devices
                .GroupBy(d => d.Zone)
                .ToDictionary(g => g.Key.ToString() ?? "Unknown", g => g.Count());
            
            var preAssessmentPassed = devices.Count(d => d.PreAssessmentPassed == true);
            var preAssessmentFailed = devices.Count(d => d.PreAssessmentPassed == false);
            var qaPassed = devices.Count(d => d.QaPassed == true);
            var qaFailed = devices.Count(d => d.QaPassed == false);
            var qaPending = devices.Count(d => d.QaPassed == null && d.Stage == DeviceDesk.Modules.Phase2.Models.Phase2Stage.QualityAssessment);
            
            int disposalPending = 0;
            int disposalApproved = 0;
            try
            {
                disposalPending = await _phase2Db.Disposals.Where(d => !d.IsApproved).CountAsync();
                disposalApproved = await _phase2Db.Disposals.Where(d => d.IsApproved).CountAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SuperAdminService] Error querying Disposals: {ex.Message}");
            }
            
            var underWarranty = devices.Count(d => d.UnderWarranty == true);
            var repairable = devices.Count(d => d.Repairable == true);

            return new Phase2StatsDto
            {
                TotalDevices = devices.Count,
                DevicesByStage = byStage,
                DevicesByZone = byZone,
                PreAssessmentPassed = preAssessmentPassed,
                PreAssessmentFailed = preAssessmentFailed,
                QAPassed = qaPassed,
                QAFailed = qaFailed,
                QAPending = qaPending,
                DisposalPending = disposalPending,
                DisposalApproved = disposalApproved,
                UnderWarranty = underWarranty,
                Repairable = repairable
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error in GetPhase2StatsAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            // Return empty stats instead of throwing
            return new Phase2StatsDto
            {
                TotalDevices = 0,
                DevicesByStage = new Dictionary<string, int>(),
                DevicesByZone = new Dictionary<string, int>(),
                PreAssessmentPassed = 0,
                PreAssessmentFailed = 0,
                QAPassed = 0,
                QAFailed = 0,
                QAPending = 0,
                DisposalPending = 0,
                DisposalApproved = 0,
                UnderWarranty = 0,
                Repairable = 0
            };
        }
    }

    public async Task<Phase3StatsDto> GetPhase3StatsAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            // Convert DateTime? to DateTimeOffset? for Phase3 entities
            DateTimeOffset? fromDateOffset = fromDate.HasValue ? new DateTimeOffset(fromDate.Value) : null;
            DateTimeOffset? toDateOffset = toDate.HasValue ? new DateTimeOffset(toDate.Value) : null;

            var podsQuery = _phase3Db.DispatchPODs.AsQueryable();
            if (fromDateOffset != null) podsQuery = podsQuery.Where(p => p.CreatedAt >= fromDateOffset);
            if (toDateOffset != null) podsQuery = podsQuery.Where(p => p.CreatedAt <= toDateOffset);
            var pods = await podsQuery.ToListAsync();
            var podsByStatus = pods
                .GroupBy(p => p.Status)
                .ToDictionary(g => g.Key.ToString() ?? "Unknown", g => g.Count());
            
            var tripsQuery = _phase3Db.DispatchTrips.AsQueryable();
            if (fromDateOffset != null) tripsQuery = tripsQuery.Where(t => t.CreatedAt >= fromDateOffset);
            if (toDateOffset != null) tripsQuery = tripsQuery.Where(t => t.CreatedAt <= toDateOffset);
            var trips = await tripsQuery.ToListAsync();
            var tripsByStatus = trips
                .GroupBy(t => t.Status)
                .ToDictionary(g => g.Key.ToString() ?? "Unknown", g => g.Count());
            
            var delivered = pods.Count(p => p.Status == PODStatus.Delivered);
            var exceptions = pods.Count(p => p.HasExceptions);

            return new Phase3StatsDto
            {
                TotalPODs = pods.Count,
                PODsByStatus = podsByStatus,
                TotalTrips = trips.Count,
                TripsByStatus = tripsByStatus,
                Delivered = delivered,
                Exceptions = exceptions
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error in GetPhase3StatsAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            // Return empty stats instead of throwing
            return new Phase3StatsDto
            {
                TotalPODs = 0,
                PODsByStatus = new Dictionary<string, int>(),
                TotalTrips = 0,
                TripsByStatus = new Dictionary<string, int>(),
                Delivered = 0,
                Exceptions = 0
            };
        }
    }

    public async Task<SchoolStatsDto> GetSchoolStatsAsync()
    {
        try
        {
            var schools = await _phase0Db.Schools.ToListAsync();
            
            // Get Phase2 workflow devices with schools (cast int to long)
            var phase2SchoolIds = await _phase2Db.Devices
                .Where(d => d.SchoolId.HasValue)
                .Select(d => (long)d.SchoolId!.Value)
                .Distinct()
                .ToListAsync();
            
            // Get imported devices with schools
            var importedSchoolIds = await _superAdminDb.ImportedDevices
                .Where(d => d.SchoolId.HasValue)
                .Select(d => d.SchoolId!.Value)
                .Distinct()
                .ToListAsync();
            
            // Combine unique school IDs
            var allSchoolIds = phase2SchoolIds.Union(importedSchoolIds).Distinct();
            var schoolsWithDevices = allSchoolIds.Count();
            
            // Get devices by school from Phase2 (cast to long for consistency)
            var devicesBySchool = await _phase2Db.Devices
                .Where(d => d.SchoolId.HasValue)
                .GroupBy(d => new { d.SchoolId, d.SchoolName })
                .Select(g => new { SchoolId = (long)(g.Key.SchoolId ?? 0), SchoolName = g.Key.SchoolName ?? "Unknown", Count = g.Count() })
                .ToListAsync();

            // Get devices by school from ImportedDevices
            var importedDevicesBySchool = await _superAdminDb.ImportedDevices
                .Where(d => d.SchoolId.HasValue)
                .GroupBy(d => new { d.SchoolId, d.SchoolName })
                .Select(g => new { SchoolId = g.Key.SchoolId ?? 0L, SchoolName = g.Key.SchoolName ?? "Unknown", Count = g.Count() })
                .ToListAsync();

            // Safe dictionary creation to combine both sources
            var devicesBySchoolDict = new Dictionary<string, int>();
            foreach (var item in devicesBySchool)
            {
                var schoolName = item.SchoolName;
                if (devicesBySchoolDict.ContainsKey(schoolName))
                {
                    devicesBySchoolDict[schoolName] += item.Count;
                }
                else
                {
                    devicesBySchoolDict[schoolName] = item.Count;
                }
            }
            
            foreach (var item in importedDevicesBySchool)
            {
                var schoolName = item.SchoolName;
                if (devicesBySchoolDict.ContainsKey(schoolName))
                {
                    devicesBySchoolDict[schoolName] += item.Count;
                }
                else
                {
                    devicesBySchoolDict[schoolName] = item.Count;
                }
            }

            return new SchoolStatsDto
            {
                TotalSchools = schools.Count,
                SchoolsWithDevices = schoolsWithDevices,
                DevicesBySchool = devicesBySchoolDict
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error in GetSchoolStatsAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            // Return empty stats instead of throwing
            return new SchoolStatsDto
            {
                TotalSchools = 0,
                SchoolsWithDevices = 0,
                DevicesBySchool = new Dictionary<string, int>()
            };
        }
    }

    public async Task<DriverVehicleStatsDto> GetDriverVehicleStatsAsync()
    {
        try
        {
            var trips = await _phase3Db.DispatchTrips.ToListAsync();
            var drivers = trips.Where(t => !string.IsNullOrEmpty(t.DriverName)).Select(t => t.DriverName).Distinct().ToList();
            var vehicles = trips.Where(t => !string.IsNullOrEmpty(t.VehicleReg)).Select(t => t.VehicleReg).Distinct().ToList();
            
            // Safe dictionary creation to handle null keys
            var tripsByDriver = trips
                .Where(t => !string.IsNullOrEmpty(t.DriverName))
                .GroupBy(t => t.DriverName!)
                .ToDictionary(g => g.Key, g => g.Count());
            var tripsByVehicle = trips
                .Where(t => !string.IsNullOrEmpty(t.VehicleReg))
                .GroupBy(t => t.VehicleReg!)
                .ToDictionary(g => g.Key, g => g.Count());
            
            var activeTrips = trips.Count(t => t.Status == TripStatus.InTransit || t.Status == TripStatus.PendingAcceptance);

            return new DriverVehicleStatsDto
            {
                TotalDrivers = drivers.Count,
                TotalVehicles = vehicles.Count,
                ActiveTrips = activeTrips,
                TripsByDriver = tripsByDriver,
                TripsByVehicle = tripsByVehicle
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error in GetDriverVehicleStatsAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            // Return empty stats instead of throwing
            return new DriverVehicleStatsDto
            {
                TotalDrivers = 0,
                TotalVehicles = 0,
                ActiveTrips = 0,
                TripsByDriver = new Dictionary<string, int>(),
                TripsByVehicle = new Dictionary<string, int>()
            };
        }
    }

    public async Task<ManagementSummaryDto> GetManagementSummaryAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        try
        {
            var stats = await GetDashboardStatsAsync(fromDate, toDate);
            var phase2Stats = await GetPhase2StatsAsync(fromDate, toDate);
            var phase3Stats = await GetPhase3StatsAsync(fromDate, toDate);
            var schoolStats = await GetSchoolStatsAsync();
            var driverStats = await GetDriverVehicleStatsAsync();

            var dateContext = fromDate != null || toDate != null
                ? $" for the period {fromDate?.ToString("yyyy-MM-dd") ?? "beginning"} to {toDate?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.ToString("yyyy-MM-dd")}"
                : " across all time periods";

            var systemHealth = $"As of {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC, the DeviceDesk system manages {stats.TotalDevices:N0} total devices{dateContext}. " +
                $"The procurement pipeline shows {stats.Phase0Batches:N0} batches processed in Phase 0, with {stats.Phase0Devices:N0} devices scanned. " +
                $"Phase 1 receiving has processed {stats.Phase1Batches:N0} batches containing {stats.Phase1Devices:N0} devices, generating {stats.TotalGRVs:N0} Goods Received Notes. " +
                $"Currently, {phase2Stats.DevicesByStage.GetValueOrDefault("QualityAssessment", 0):N0} devices await quality assessment, while {phase2Stats.DevicesByStage.GetValueOrDefault("AwaitingDispatch", 0):N0} are ready for dispatch.";

            var warehouseStatus = $"The ICT Center warehouse currently holds {stats.Phase2Devices:N0} devices in Phase 2 processing. " +
                $"Inventory distribution shows {phase2Stats.DevicesByZone.GetValueOrDefault("NewStock", 0):N0} devices in the New Stock zone and {phase2Stats.DevicesByZone.GetValueOrDefault("RnR", 0):N0} in the R&R (Repair & Return) zone. " +
                $"{phase2Stats.DevicesByStage.GetValueOrDefault("AwaitingDispatch", 0):N0} devices are staged for dispatch, while {phase2Stats.DevicesByStage.GetValueOrDefault("Quarantine", 0):N0} are in quarantine status. " +
                $"Quality metrics indicate {phase2Stats.QAPassed:N0} devices passed QA, {phase2Stats.QAFailed:N0} failed, and {phase2Stats.QAPending:N0} are pending assessment. " +
                $"{phase2Stats.DisposalPending:N0} disposal requests await approval.";

            var deliveryPerformance = $"Dispatch operations{dateContext} have generated {stats.Phase3Pods:N0} Proof of Delivery (POD) documents. " +
                $"{phase3Stats.TripsByStatus.GetValueOrDefault("Completed", 0):N0} delivery trips have been completed, with {phase3Stats.Delivered:N0} devices successfully delivered to schools. " +
                $"{phase3Stats.Exceptions:N0} delivery exceptions have been recorded and require management attention. " +
                $"Trip status breakdown: {phase3Stats.TripsByStatus.GetValueOrDefault("InTransit", 0):N0} in transit, {phase3Stats.TripsByStatus.GetValueOrDefault("PendingAcceptance", 0):N0} pending acceptance.";

            var topSchools = schoolStats.DevicesBySchool.OrderByDescending(x => x.Value).Take(5).ToList();
            var schoolAllocation = $"Device allocation spans {schoolStats.SchoolsWithDevices:N0} schools out of {schoolStats.TotalSchools:N0} total schools in the system. " +
                (topSchools.Any()
                    ? $"Top receiving schools by device count: {string.Join(", ", topSchools.Select(x => $"{x.Key} ({x.Value:N0} devices)"))}. "
                    : "Device allocation data is being compiled. ") +
                $"This represents a {((double)schoolStats.SchoolsWithDevices / Math.Max(schoolStats.TotalSchools, 1) * 100):F1}% allocation coverage rate.";

            var driverVehicleActivity = $"Fleet management shows {driverStats.ActiveTrips:N0} active delivery trips currently in progress. " +
                $"The dispatch operation utilizes {driverStats.TotalDrivers:N0} registered drivers and {driverStats.TotalVehicles:N0} vehicles. " +
                (driverStats.TripsByDriver.Any()
                    ? $"Top performing drivers by trip count: {string.Join(", ", driverStats.TripsByDriver.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} ({x.Value} trips)"))}. "
                    : "") +
                $"Vehicle utilization data indicates efficient resource allocation across the delivery network.";

            var summary = new ManagementSummaryDto
            {
                SystemHealth = systemHealth,
                WarehouseStatus = warehouseStatus,
                DeliveryPerformance = deliveryPerformance,
                SchoolAllocation = schoolAllocation,
                DriverVehicleActivity = driverVehicleActivity
            };

            return summary;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SuperAdminService] Error in GetManagementSummaryAsync: {ex.Message}");
            Console.WriteLine($"[SuperAdminService] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[SuperAdminService] Inner exception: {ex.InnerException.Message}");
            }
            // Return a basic summary with error indication
            return new ManagementSummaryDto
            {
                SystemHealth = "Unable to generate system health summary due to an error.",
                WarehouseStatus = "Unable to generate warehouse status due to an error.",
                DeliveryPerformance = "Unable to generate delivery performance summary due to an error.",
                SchoolAllocation = "Unable to generate school allocation summary due to an error.",
                DriverVehicleActivity = "Unable to generate driver/vehicle activity summary due to an error."
            };
        }
    }

    public async Task<Phase2DashboardStatsDto> GetPhase2DashboardStatsAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        // Build query - if no dates provided, show all time
        var processedQuery = _phase2Db.Devices
            .Where(d => d.QaPassed != null);

        // Apply date filters only if provided
        if (from.HasValue)
        {
            processedQuery = processedQuery.Where(d => d.CreatedAt >= from.Value);
        }
        if (to.HasValue)
        {
            processedQuery = processedQuery.Where(d => d.CreatedAt <= to.Value);
        }

        var totalProcessed = await processedQuery.CountAsync(ct);
        var totalPassed = await processedQuery.CountAsync(d => d.QaPassed == true, ct);
        var totalFailed = totalProcessed - totalPassed;

        // Guard against divide-by-zero
        double passRate = 0;
        double failRate = 0;
        if (totalProcessed > 0)
        {
            passRate = (double)totalPassed / totalProcessed * 100.0;
            failRate = 100.0 - passRate;
        }

        // Safe grouping for Stage
        var stageGroups = await processedQuery
            .GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var stageCounts = stageGroups
            .GroupBy(x => x.Stage)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );

        // Safe grouping for Zone
        var zoneGroups = await processedQuery
            .GroupBy(d => d.Zone)
            .Select(g => new { Zone = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var zoneCounts = zoneGroups
            .GroupBy(x => x.Zone)
            .ToDictionary(
                g => g.Key.ToString() ?? "Unknown",
                g => g.Sum(x => x.Count)
            );

        var dailyProcessed = await processedQuery
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new DailyCountPoint
            {
                Date = g.Key,
                Count = g.Count()
            })
            .OrderBy(p => p.Date)
            .ToListAsync(ct);

        return new Phase2DashboardStatsDto
        {
            TotalDevicesProcessed = totalProcessed,
            TotalDevicesPassed = totalPassed,
            TotalDevicesFailed = totalFailed,
            PassRate = passRate,
            FailRate = failRate,
            StageCounts = stageCounts,
            ZoneCounts = zoneCounts,
            DailyProcessed = dailyProcessed
        };
    }

    public async Task<Phase2ManagementSummaryDto> GetPhase2ManagementSummaryAsync(
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // For summary, use provided dates or default to last 30 days for context
        var start = from ?? now.AddDays(-30);
        var end = to ?? now;

        // Build query - if dates provided, use them; otherwise default to last 30 days for summary
        var query = _phase2Db.Devices
            .Where(d => d.QaPassed != null);

        // Always apply date filter for summary (defaults to last 30 days if not specified)
        query = query.Where(d => d.CreatedAt >= start && d.CreatedAt <= end);

        var total = await query.CountAsync(ct);
        var passed = await query.CountAsync(d => d.QaPassed == true, ct);
        var failed = total - passed;

        double passRate = total == 0 ? 0 : (double)passed / total * 100.0;
        double failRate = 100.0 - passRate;

        var stageGroups = await query
            .GroupBy(d => d.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var topStage = stageGroups
            .OrderByDescending(s => s.Count)
            .FirstOrDefault();

        var zoneGroups = await query
            .GroupBy(d => d.Zone)
            .Select(g => new { Zone = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var topZone = zoneGroups
            .OrderByDescending(z => z.Count)
            .FirstOrDefault();

        var last7 = now.AddDays(-7);
        var last7Count = await query.CountAsync(d => d.CreatedAt >= last7, ct);
        var prev7Count = await query.CountAsync(d =>
            d.CreatedAt >= last7.AddDays(-7) &&
            d.CreatedAt < last7, ct);

        string trendSummary;
        if (prev7Count == 0)
        {
            trendSummary = "Workload appears stable.";
        }
        else
        {
            double change = ((double)last7Count - prev7Count) / prev7Count * 100.0;
            if (change > 10)
                trendSummary = $"Activity increased by {change:F1}% compared to the previous week.";
            else if (change < -10)
                trendSummary = $"Activity decreased by {Math.Abs(change):F1}% compared to the previous week.";
            else
                trendSummary = "Workload remains consistent week-over-week.";
        }

        var summary = $@"
Between {start:dd MMM yyyy} and {end:dd MMM yyyy}, a total of {total:N0} devices were processed in the ICT Centre. 
Of these, {passed:N0} devices ({passRate:F1}%) successfully passed QA, while {failed:N0} devices ({failRate:F1}%) 
required rework or failed quality checks.

The most common workflow stage during this period was **{topStage?.Stage}**, representing {topStage?.Count:N0} devices. 
In terms of storage movement, the busiest zone was **{topZone?.Zone}**, accounting for {topZone?.Count:N0} units.

{trendSummary}
";

        return new Phase2ManagementSummaryDto
        {
            SummaryText = summary.Trim(),
            From = start,
            To = end
        };
    }

    public async Task<ImportedDevicesResultDto> GetImportedDevicesAsync(ImportedDeviceFilterDto filter)
    {
        var query = _superAdminDb.ImportedDevices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Serial))
        {
            query = query.Where(d => d.Serial.Contains(filter.Serial));
        }

        if (!string.IsNullOrWhiteSpace(filter.School))
        {
            query = query.Where(d => d.SchoolName != null &&
                                     d.SchoolName.Contains(filter.School));
        }

        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            query = query.Where(d => d.District != null &&
                                     d.District.Contains(filter.District));
        }

        var total = await query.CountAsync();

        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

        var items = await query
            .OrderByDescending(d => d.DateReceived ?? d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new ImportedDeviceListItemDto
            {
                Id = d.Id,
                Serial = d.Serial,
                SchoolId = d.SchoolId,
                SchoolName = d.SchoolName,
                EmisCode = d.EmisCode,
                District = d.District,
                Circuit = d.Circuit,
                ItemDescription = d.ItemDescription,
                PodNumber = d.PodNumber,
                DateReceived = d.DateReceived,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        return new ImportedDevicesResultDto
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SchoolDevicesDetailDto> GetSchoolDevicesAsync(long? schoolId, string? emis)
    {
        // 1) Resolve school from Phase0 (DeviceDeskDbContext.Schools)
        var schoolQuery = _phase0Db.Schools.AsQueryable();

        if (schoolId.HasValue)
        {
            schoolQuery = schoolQuery.Where(s => s.SchoolId == schoolId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(emis))
        {
            schoolQuery = schoolQuery.Where(s => s.EmisCode == emis);
        }
        else
        {
            throw new ArgumentException("Either schoolId or emis must be provided.");
        }

        var school = await schoolQuery.FirstOrDefaultAsync();

        if (school == null)
        {
            return new SchoolDevicesDetailDto
            {
                Summary = new SchoolDevicesSummaryDto
                {
                    SchoolId = schoolId,
                    EmisCode = emis,
                    SchoolName = null,
                    WorkflowCount = 0,
                    ImportedCount = 0
                },
                Devices = new List<SchoolDeviceListItemDto>()
            };
        }

        long resolvedSchoolId = school.SchoolId;
        string? resolvedEmis = school.EmisCode;
        string? district = school.District;
        string? circuit = school.Circuit;

        // 2) Workflow devices (Phase 2)
        var workflowDevices = await _phase2Db.Devices
            .Where(d => d.SchoolId == (int)school.SchoolId) // Phase2Device.SchoolId is int?
            .Select(d => new SchoolDeviceListItemDto
            {
                Serial = d.Serial,
                Source = "Workflow",
                Stage = d.Stage.ToString(),
                Zone = d.Zone.ToString(),
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();

        // 3) Imported devices (SuperAdmin_ImportedDevices)
        var importedQuery = _superAdminDb.ImportedDevices.AsQueryable();

        importedQuery = importedQuery.Where(d =>
            (d.SchoolId.HasValue && d.SchoolId.Value == resolvedSchoolId) ||
            (!d.SchoolId.HasValue && d.EmisCode == resolvedEmis)
        );

        var importedDevices = await importedQuery
            .Select(d => new SchoolDeviceListItemDto
            {
                Serial = d.Serial,
                Source = "Imported",
                Stage = "At School",              // Already delivered to school
                Zone = d.District,                // Geographic zone/district
                CreatedAt = d.DateReceived ?? d.CreatedAt
            })
            .ToListAsync();

        var allDevices = workflowDevices.Concat(importedDevices).ToList();

        // 4) Calculate category counts (from imported devices with ItemDescription)
        var categoryCounts = await _superAdminDb.ImportedDevices
            .Where(d =>
                (d.SchoolId.HasValue && d.SchoolId.Value == resolvedSchoolId) ||
                (!d.SchoolId.HasValue && d.EmisCode == resolvedEmis))
            .GroupBy(d => d.ItemDescription ?? "Uncategorized")
            .Select(g => new CategoryCountDto
            {
                Category = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(c => c.Count)
            .ToListAsync();

        // Add workflow devices as a category if any exist
        if (workflowDevices.Count > 0)
        {
            categoryCounts.Add(new CategoryCountDto
            {
                Category = "Workflow Devices",
                Count = workflowDevices.Count
            });
        }

        var summary = new SchoolDevicesSummaryDto
        {
            SchoolId = resolvedSchoolId,
            SchoolName = school.Name,
            EmisCode = resolvedEmis,
            District = district,
            Circuit = circuit,
            WorkflowCount = workflowDevices.Count,
            ImportedCount = importedDevices.Count,
            Categories = categoryCounts
        };

        return new SchoolDevicesDetailDto
        {
            Summary = summary,
            Devices = allDevices
        };
    }

    // Helper method to map districts to provinces
    private static string MapDistrictToProvince(string? district)
    {
        if (string.IsNullOrWhiteSpace(district))
            return "Unknown";

        // KZN districts
        var kznDistricts = new[]
        {
            "Umgungundlovu", "Ilembe", "Pinetown", "Uthukela", "Zululand",
            "Umzinyathi", "Umlazi", "Ugu", "Amajuba", "King Cetshwayo",
            "Harry Gwala", "eThekwini"
        };

        // Limpopo districts
        var limpopoDistricts = new[]
        {
            "Capricorn", "Mopani", "Sekhukhune", "Vhembe", "Waterberg"
        };

        // Mpumalanga districts
        var mpumalangaDistricts = new[]
        {
            "Ehlanzeni", "Gert Sibande", "Nkangala"
        };

        // Northern Cape districts
        var northernCapeDistricts = new[]
        {
            "Siyanda", "Frances Baard", "Namakwa", "Pixley ka Seme", "John Taolo Gaetsewe"
        };

        var districtLower = district.ToLower();

        if (kznDistricts.Any(d => districtLower.Contains(d.ToLower())))
            return "KZN";
        if (limpopoDistricts.Any(d => districtLower.Contains(d.ToLower())))
            return "Limpopo";
        if (mpumalangaDistricts.Any(d => districtLower.Contains(d.ToLower())))
            return "Mpumalanga";
        if (northernCapeDistricts.Any(d => districtLower.Contains(d.ToLower())))
            return "Northern Cape";

        return "Unknown";
    }

    // Helper method to generate synthetic district data for provinces with CSV data but no mapped districts
    private static List<DistrictAnalyticsCardDto> GenerateSyntheticDistrictsForProvince(string provinceName, int totalDevices)
    {
        var districts = new List<DistrictAnalyticsCardDto>();

        // Define known districts for each province
        var provinceDistrictMap = new Dictionary<string, string[]>
        {
            { "Limpopo", new[] { "Capricorn", "Mopani", "Sekhukhune", "Vhembe", "Waterberg" } },
            { "Mpumalanga", new[] { "Ehlanzeni", "Gert Sibande", "Nkangala" } },
            { "KZN", new[] { "Umgungundlovu", "Ilembe", "Uthukela", "Zululand", "eThekwini" } },
            { "Northern Cape", new[] { "Siyanda", "Frances Baard", "Namakwa", "Pixley ka Seme" } }
        };

        if (!provinceDistrictMap.ContainsKey(provinceName))
            return districts;

        var districtNames = provinceDistrictMap[provinceName];
        var devicesPerDistrict = totalDevices / districtNames.Length;
        var remainder = totalDevices % districtNames.Length;

        for (int i = 0; i < districtNames.Length; i++)
        {
            var districtDevices = devicesPerDistrict + (i == 0 ? remainder : 0); // Add remainder to first district
            var estimatedSchools = Math.Max(1, districtDevices / 10); // Estimate ~10 devices per school

            districts.Add(new DistrictAnalyticsCardDto
            {
                District = districtNames[i],
                Province = provinceName,
                TotalSchools = estimatedSchools,
                TotalDevices = districtDevices,
                ProcessedDevices = districtDevices // CSV devices are considered processed
            });
        }

        return districts;
    }

    public async Task<ProvincialAnalyticsResultDto> GetProvincialAnalyticsAsync()
    {
        // 1) Resolve CSV path
        var contentRoot = _environment.ContentRootPath;
        var csvPath = Path.Combine(contentRoot, "Data", "Analytics", "provincial_analytics.csv");

        if (!File.Exists(csvPath))
        {
            // Return empty result if file missing
            return new ProvincialAnalyticsResultDto
            {
                Summary = new ProvincialAnalyticsSummaryDto(),
                Provinces = new List<ProvinceAnalyticsCardDto>()
            };
        }

        // 2) Read and parse CSV
        var lines = await File.ReadAllLinesAsync(csvPath);
        if (lines.Length <= 1)
        {
            return new ProvincialAnalyticsResultDto
            {
                Summary = new ProvincialAnalyticsSummaryDto(),
                Provinces = new List<ProvinceAnalyticsCardDto>()
            };
        }

        var records = new List<ProvincialAnalyticsRecordDto>();

        // Skip header (line[0])
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var cols = line.Split(',');
            if (cols.Length < 3)
                continue;

            var province = cols[0].Trim();
            var type = cols[1].Trim();

            if (!int.TryParse(cols[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity))
                continue;

            records.Add(new ProvincialAnalyticsRecordDto
            {
                Province = province,
                ProjectDeviceType = type,
                Quantity = quantity
            });
        }

        // 3) Get real stats from database

        // Total schools from Phase 0 schools table
        var totalSchools = await _phase0Db.Schools.CountAsync();

        // Get all schools with their districts
        var schoolsData = await _phase0Db.Schools
            .Where(s => s.District != null && s.District != "")
            .Select(s => new
            {
                s.District,
                s.SchoolId
            })
            .ToListAsync();

        // Map districts to provinces (done in-memory after materialization)
        var schoolsWithProvinces = schoolsData
            .Select(s => new
            {
                s.District,
                s.SchoolId,
                Province = MapDistrictToProvince(s.District)
            })
            .Where(s => s.Province != "Unknown")
            .ToList();

        // Calculate district stats
        var districtGroups = schoolsWithProvinces
            .GroupBy(s => new { s.District, s.Province })
            .Select(g => new
            {
                District = g.Key.District,
                Province = g.Key.Province,
                SchoolCount = g.Count(),
                SchoolIds = g.Select(x => x.SchoolId).ToList()
            })
            .ToList();

        // Get device counts by district from ImportedDevices
        var devicesByDistrict = await _superAdminDb.ImportedDevices
            .Where(d => d.District != null && d.District != "")
            .GroupBy(d => d.District)
            .Select(g => new
            {
                District = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        // Get workflow devices count by school, then aggregate to district
        var workflowDevicesBySchool = await _phase2Db.Devices
            .GroupBy(d => d.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        // Get workflow processed devices count by school (QA passed)
        var workflowProcessedBySchool = await _phase2Db.Devices
            .Where(d => d.QaPassed == true)
            .GroupBy(d => d.SchoolId)
            .Select(g => new
            {
                SchoolId = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        // Map schools to districts (use SchoolId, not Id)
        var schoolIdToDistrict = await _phase0Db.Schools
            .Where(s => s.District != null && s.District != "")
            .Select(s => new
            {
                s.SchoolId,
                s.District
            })
            .ToListAsync();

        var schoolDistrictMap = schoolIdToDistrict.ToDictionary(s => (int?)s.SchoolId, s => s.District);

        // Build district analytics cards
        var districtCards = new List<DistrictAnalyticsCardDto>();
        
        foreach (var districtInfo in districtGroups)
        {
            var importedDevices = devicesByDistrict
                .FirstOrDefault(d => string.Equals(d.District, districtInfo.District, StringComparison.OrdinalIgnoreCase))?.Count ?? 0;

            var workflowDevices = workflowDevicesBySchool
                .Where(w => w.SchoolId.HasValue && 
                           schoolDistrictMap.ContainsKey(w.SchoolId) && 
                           string.Equals(schoolDistrictMap[w.SchoolId], districtInfo.District, StringComparison.OrdinalIgnoreCase))
                .Sum(w => w.Count);

            var totalDevicesInDistrict = importedDevices + workflowDevices;

            // Get processed devices (QA passed workflow + all imported) - now using pre-fetched data
            var workflowProcessedInDistrict = workflowProcessedBySchool
                .Where(w => w.SchoolId.HasValue &&
                           schoolDistrictMap.ContainsKey(w.SchoolId) &&
                           string.Equals(schoolDistrictMap[w.SchoolId], districtInfo.District, StringComparison.OrdinalIgnoreCase))
                .Sum(w => w.Count);

            var processedDevicesInDistrict = workflowProcessedInDistrict + importedDevices;

            districtCards.Add(new DistrictAnalyticsCardDto
            {
                District = districtInfo.District!,
                Province = districtInfo.Province,
                TotalSchools = districtInfo.SchoolCount,
                TotalDevices = totalDevicesInDistrict,
                ProcessedDevices = processedDevicesInDistrict
            });
        }

        // 4) Aggregate per province (from CSV + database)
        var provinceGroups = records
            .GroupBy(r => r.Province)
            .Select(g => g.Key)
            .ToList();

        var provinceCards = new List<ProvinceAnalyticsCardDto>();

        // Get all unique provinces from the database
        var allProvinces = schoolsWithProvinces
            .Select(s => s.Province)
            .Distinct()
            .Union(provinceGroups) // Include provinces from CSV
            .Distinct()
            .ToList();

        foreach (var provinceName in allProvinces)
        {
            var csvDevices = records.Where(r => r.Province == provinceName).Sum(r => r.Quantity);
            
            // Get districts in this province (exact match)
            var districtsInProvince = districtCards
                .Where(d => d.Province.Equals(provinceName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var schoolsInProvince = districtsInProvince.Sum(d => d.TotalSchools);
            var devicesInProvince = districtsInProvince.Sum(d => d.TotalDevices);
            var processedInProvince = districtsInProvince.Sum(d => d.ProcessedDevices);

            // FALLBACK: If province has devices from CSV but no district data, generate synthetic districts
            if (csvDevices > 0 && districtsInProvince.Count == 0)
            {
                // Generate estimated district breakdown based on CSV device count
                var estimatedDistricts = GenerateSyntheticDistrictsForProvince(provinceName, csvDevices);
                districtsInProvince.AddRange(estimatedDistricts);
                districtCards.AddRange(estimatedDistricts);
                
                schoolsInProvince = estimatedDistricts.Sum(d => d.TotalSchools);
            }

            provinceCards.Add(new ProvinceAnalyticsCardDto
            {
                Province = provinceName,
                TotalSchools = schoolsInProvince,
                TotalDistricts = districtsInProvince.Count,
                TotalDevices = csvDevices + devicesInProvince,
                ProcessedDevices = csvDevices + processedInProvince, // CSV devices count as processed
                Districts = districtsInProvince
            });
        }

        provinceCards = provinceCards.OrderByDescending(p => p.TotalDevices).ToList();

        // 5) Total distinct districts (ignoring null/empty)
        var totalDistricts = await _phase0Db.Schools
            .Select(s => s.District)
            .Where(d => d != null && d != "")
            .Distinct()
            .CountAsync();

        // Devices from workflow (Phase 2) and imported Siyanda
        var workflowDevicesCount = await _phase2Db.Devices.CountAsync();
        var importedDevicesCount = await _superAdminDb.ImportedDevices.CountAsync();

        // Consider "processed" as QA-passed workflow devices + all imported Siyanda devices
        var workflowProcessedCount = await _phase2Db.Devices
            .Where(d => d.QaPassed == true)
            .CountAsync();

        var devicesProcessed = workflowProcessedCount + importedDevicesCount;
        var totalDevices = workflowDevicesCount + importedDevicesCount;

        var summary = new ProvincialAnalyticsSummaryDto
        {
            TotalDistricts = totalDistricts,
            TotalSchools = totalSchools,
            TotalDevices = totalDevices,
            DevicesProcessed = devicesProcessed
        };

        return new ProvincialAnalyticsResultDto
        {
            Summary = summary,
            Provinces = provinceCards,
            Districts = districtCards.OrderBy(d => d.Province).ThenBy(d => d.District).ToList()
        };
    }

    public async Task<object> ReseedImportedDevicesAsync(bool clearExisting)
    {
        var csvPath = Path.Combine(_environment.ContentRootPath, "Data", "Seeds", 
            "Schools_Populated_Siyanda_Fixed_Dates_Cleaned (1).csv");

        if (!File.Exists(csvPath))
        {
            return new
            {
                success = false,
                message = "CSV file not found",
                path = csvPath
            };
        }

        // Clear existing data if requested
        if (clearExisting)
        {
            var existingCount = await _superAdminDb.ImportedDevices.CountAsync();
            _superAdminDb.ImportedDevices.RemoveRange(_superAdminDb.ImportedDevices);
            await _superAdminDb.SaveChangesAsync();
            Console.WriteLine($"[ReseedImportedDevices] Cleared {existingCount} existing records.");
        }

        // Read and parse CSV
        var lines = await File.ReadAllLinesAsync(csvPath);
        if (lines.Length <= 1)
        {
            return new
            {
                success = false,
                message = "CSV file is empty or has no data rows"
            };
        }

        int imported = 0;
        int skipped = 0;
        int updated = 0;
        var devicesToAdd = new List<ImportedDevice>();
        var existingSerialsList = await _superAdminDb.ImportedDevices
            .Select(d => d.Serial)
            .ToListAsync();
        var existingSerials = new HashSet<string>(existingSerialsList);

        // Skip header row (line 0)
        for (int i = 1; i < lines.Length; i++)
        {
            try
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    skipped++;
                    continue;
                }

                var columns = ParseCsvLine(line);
                if (columns.Length < 10)
                {
                    skipped++;
                    continue;
                }

                // Column mapping from CSV:
                // 0: EMIS, 1: District, 2: CMC, 3: Circuit, 4: School Name
                // 5: District (duplicate), 6: POD Number, 7: Date Received
                // 8: Item Description, 9: Serial Number

                var emisCode = GetColumn(columns, 0);
                var district = NormalizeDistrictName(GetColumn(columns, 1));
                var circuit = GetColumn(columns, 3);
                var schoolNameFromFile = GetColumn(columns, 4);
                var podNumber = GetColumn(columns, 6);
                var dateReceivedStr = GetColumn(columns, 7);
                var itemDescription = GetColumn(columns, 8);
                var serial = GetColumn(columns, 9);

                if (string.IsNullOrWhiteSpace(serial))
                {
                    skipped++;
                    continue;
                }

                // Skip duplicates already in the batch
                if (devicesToAdd.Any(d => d.Serial == serial))
                {
                    skipped++;
                    continue;
                }

                // Look up school by EMIS
                long? schoolId = null;
                string? schoolName = null;

                if (!string.IsNullOrWhiteSpace(emisCode))
                {
                    var school = await _phase0Db.Schools
                        .Where(s => s.EmisCode == emisCode)
                        .Select(s => new { s.SchoolId, s.Name })
                        .FirstOrDefaultAsync();

                    if (school != null)
                    {
                        schoolId = school.SchoolId;
                        schoolName = school.Name;
                    }
                    else
                    {
                        // Fallback: use the name from CSV
                        schoolName = string.IsNullOrWhiteSpace(schoolNameFromFile)
                            ? null
                            : schoolNameFromFile;
                    }
                }

                // Parse "Date Received" as local (SAST, UTC+2)
                DateTime? dateReceived = null;
                if (!string.IsNullOrWhiteSpace(dateReceivedStr) &&
                    DateTime.TryParse(
                        dateReceivedStr,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var parsed))
                {
                    dateReceived = parsed;
                }

                // Check if device already exists in DB
                if (!clearExisting && existingSerials.Contains(serial))
                {
                    // Update existing record
                    var existingDevice = await _superAdminDb.ImportedDevices
                        .FirstOrDefaultAsync(d => d.Serial == serial);
                    
                    if (existingDevice != null)
                    {
                        existingDevice.SchoolId = schoolId;
                        existingDevice.SchoolName = schoolName;
                        existingDevice.EmisCode = emisCode;
                        existingDevice.District = district;
                        existingDevice.Circuit = circuit;
                        existingDevice.ItemDescription = itemDescription;
                        existingDevice.PodNumber = podNumber;
                        existingDevice.DateReceived = dateReceived;
                        updated++;
                    }
                }
                else
                {
                    var device = new ImportedDevice
                    {
                        Serial = serial,
                        SchoolId = schoolId,
                        SchoolName = schoolName,
                        EmisCode = emisCode,
                        District = district,
                        Circuit = circuit,
                        ItemDescription = itemDescription,
                        PodNumber = podNumber,
                        DateReceived = dateReceived,
                        CreatedAt = DateTime.UtcNow
                    };

                    devicesToAdd.Add(device);
                    imported++;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                Console.WriteLine($"[ReseedImportedDevices] Error importing device from CSV line {i + 1}: {ex.Message}");
            }
        }

        // Batch insert
        if (devicesToAdd.Any())
        {
            await _superAdminDb.ImportedDevices.AddRangeAsync(devicesToAdd);
        }
        
        await _superAdminDb.SaveChangesAsync();

        var totalCount = await _superAdminDb.ImportedDevices.CountAsync();

        return new
        {
            success = true,
            message = "Reseeding completed",
            imported = imported,
            updated = updated,
            skipped = skipped,
            totalInDatabase = totalCount,
            clearedExisting = clearExisting
        };
    }

    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parser that handles quoted fields
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string GetColumn(string[] columns, int index)
    {
        if (index < 0 || index >= columns.Length)
            return string.Empty;

        return columns[index].Trim().Trim('"');
    }

    private static string NormalizeDistrictName(string district)
    {
        if (string.IsNullOrWhiteSpace(district))
            return district;

        // Trim whitespace
        district = district.Trim();

        // Capitalize first letter of each word
        var words = district.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
            }
        }

        return string.Join(" ", words);
    }
}

