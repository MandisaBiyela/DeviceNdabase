using DeviceDesk.Modules.Phase2.Data;
using DeviceDesk.Modules.Phase2.Models;
using DeviceDesk.Modules.Phase3.Data;
using DeviceDesk.Modules.Phase3.Models;
using DeviceDesk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DeviceDesk.Modules.Phase3.Services;

public class MatchingDeviceDto
{
    public int Id { get; set; }
    public string Serial { get; set; } = string.Empty;
}

public class MatchingDevicesResult
{
    public int TotalInGrv { get; set; }
    public int EligibleCount { get; set; }
    public int InIctCount { get; set; }
    public List<MatchingDeviceDto> EligibleDevices { get; set; } = new();
}

public class DispatchBatchService
{
    private readonly Phase3DbContext _phase3Db;
    private readonly Phase2DbContext _phase2Db;
    private readonly DeviceDeskDbContext _coreDb;
    private readonly Phase1DbContext _phase1Db;
    private readonly ILogger<DispatchBatchService> _logger;

    public DispatchBatchService(
        Phase3DbContext phase3Db,
        Phase2DbContext phase2Db,
        DeviceDeskDbContext coreDb,
        Phase1DbContext phase1Db,
        ILogger<DispatchBatchService> logger)
    {
        _phase3Db = phase3Db;
        _phase2Db = phase2Db;
        _coreDb = coreDb;
        _phase1Db = phase1Db;
        _logger = logger;
    }

    /// <summary>
    /// Get all batches, optionally filtered by status
    /// </summary>
    public async Task<List<object>> GetBatchesAsync(BatchStatus? status = null)
    {
        var query = _phase3Db.DispatchBatches.AsQueryable();
        
        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);
        
        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                batchId = b.BatchId,
                status = b.Status,
                schoolName = b.SchoolName,
                district = b.District,
                emisCode = b.EmisCode,
                stockType = b.StockType,
                sourceReference = b.SourceReference,
                podNumber = b.PODNumber,
                deliveryNoteNumber = b.DeliveryNoteNumber,
                deviceCount = b.Devices.Count,
                createdAt = b.CreatedAt,
                lockedAt = b.LockedAt,
                completedAt = b.CompletedAt
            })
            .ToListAsync();
        
        return batches.Cast<object>().ToList();
    }

    /// <summary>
    /// Get all incomplete batches (not Completed and not Cancelled)
    /// </summary>
    public async Task<List<object>> GetIncompleteBatchesAsync()
    {
        // Anything that is NOT Completed and NOT Cancelled
        var incompleteStatuses = new[]
        {
            BatchStatus.Draft,
            BatchStatus.Locked,
            BatchStatus.AuditInProgress,
            BatchStatus.AuditFailed,
            BatchStatus.AuditPassed,
            BatchStatus.EnRoute,
            BatchStatus.Delivered // delivered but no debrief yet
        };

        var batches = await _phase3Db.DispatchBatches
            .Where(b => incompleteStatuses.Contains(b.Status))
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                batchId = b.BatchId,
                status = b.Status,
                schoolName = b.SchoolName,
                district = b.District,
                emisCode = b.EmisCode,
                stockType = b.StockType,
                sourceReference = b.SourceReference, // GRV
                deviceCount = b.Devices.Count,
                createdAt = b.CreatedAt,
                lockedAt = b.LockedAt,
                completedAt = b.CompletedAt
            })
            .ToListAsync();

        return batches.Cast<object>().ToList();
    }

    /// <summary>
    /// Get batch details by ID
    /// </summary>
    public async Task<object?> GetBatchAsync(Guid id)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == id);
        
        if (batch == null)
            return null;
        
        return new
        {
            batchId = batch.BatchId,
            status = batch.Status,
            schoolName = batch.SchoolName,
            district = batch.District,
            emisCode = batch.EmisCode,
            stockType = batch.StockType,
            sourceReference = batch.SourceReference,
            podNumber = batch.PODNumber,
            deliveryNoteNumber = batch.DeliveryNoteNumber,
            tripReference = batch.TripReference,
            driverName = batch.DriverName,
            driverUserId = batch.DriverUserId,
            vehicleReg = batch.VehicleReg,
            auditPassed = batch.AuditPassed,
            enRoute = batch.EnRoute,
            enRouteAt = batch.EnRouteAt,
            arrivedAt = batch.ArrivedAt,
            schoolSigned = batch.SchoolSigned,
            schoolSignatoryName = batch.SchoolSignatoryName,
            debriefCompleted = batch.DebriefCompleted,
            debriefNotes = batch.DebriefNotes,
            hasExceptions = batch.HasExceptions,
            exceptionNotes = batch.ExceptionNotes,
            createdAt = batch.CreatedAt,
            createdByUserId = batch.CreatedByUserId,
            lockedAt = batch.LockedAt,
            completedAt = batch.CompletedAt,
            devices = batch.Devices.Select(d => new
            {
                batchDeviceId = d.BatchDeviceId,
                deviceId = d.DeviceId,
                serial = d.Serial,
                model = d.Model,
                condition = d.Condition,
                scannedInAudit = d.ScannedInAudit,
                scannedAt = d.ScannedAt,
                addedAt = d.AddedAt
            }).ToList()
        };
    }

    /// <summary>
    /// Get devices in dispatch queue:
    /// any Phase 2 device that has been scanned out (ScannedOutAt != null),
    /// is not already in a non-cancelled batch and is not marked Delivered.
    /// </summary>
    public async Task<List<object>> GetDispatchQueueAsync()
    {
        try
        {
            _logger.LogInformation("[DispatchQueue] Starting queue load...");

            // Device IDs already in active (non-cancelled) batches
            // Use AsNoTracking() to ensure fresh data
            var deviceIdsInBatches = await _phase3Db.BatchDevices
                .AsNoTracking() // Force fresh query
                .Where(bd => bd.Batch.Status != BatchStatus.Cancelled)
                .Select(bd => bd.DeviceId)
                .Distinct()
                .ToListAsync();
            
            _logger.LogInformation("[DispatchQueue] Devices already in batches: {Count}", deviceIdsInBatches.Count);

            // Before the main query, get counts (use AsNoTracking for fresh counts)
            // First, get total scanned out (without filters) for comparison
            var totalScannedOutUnfiltered = await _phase2Db.Devices
                .AsNoTracking()
                .CountAsync(d => d.ScannedOutAt != null);
            _logger.LogInformation("[DispatchQueue] Total devices with ScannedOutAt (unfiltered): {Count}", totalScannedOutUnfiltered);
            
            // Count devices without ScannedOutByUserId (legacy data - will be included)
            var scannedOutWithoutUser = await _phase2Db.Devices
                .AsNoTracking()
                .CountAsync(d => d.ScannedOutAt != null && d.ScannedOutByUserId == null);
            _logger.LogInformation("[DispatchQueue] Devices scanned out but missing ScannedOutByUserId (legacy data, will be included): {Count}", scannedOutWithoutUser);
            
            // Count devices with ScannedOutByUserId set (ICT clerk scan-outs)
            var scannedOutWithUser = await _phase2Db.Devices
                .AsNoTracking()
                .CountAsync(d => d.ScannedOutAt != null && d.ScannedOutByUserId != null);
            _logger.LogInformation("[DispatchQueue] Devices scanned out with ScannedOutByUserId set (ICT clerk scan-outs): {Count}", scannedOutWithUser);
            
            // Exclude synthetic schools - count all scanned out devices (with or without ScannedOutByUserId)
            var totalScannedOut = await _phase2Db.Devices
                .AsNoTracking()
                .CountAsync(d => d.ScannedOutAt != null && 
                    (d.SchoolName == null || !d.SchoolName.StartsWith("Synthetic School")));
            _logger.LogInformation("[DispatchQueue] Total devices with ScannedOutAt (excluding synthetic): {Count}", totalScannedOut);

            var deliveredCount = await _phase2Db.Devices
                .AsNoTracking()
                .CountAsync(d => 
                    d.ScannedOutAt != null && 
                    d.DispatchStatus == DispatchDeviceState.Delivered &&
                    (d.SchoolName == null || !d.SchoolName.StartsWith("Synthetic School")));
            _logger.LogInformation("[DispatchQueue] Devices scanned out but marked as Delivered (excluding synthetic): {Count}", deliveredCount);

            // Any device that has been scanned out of Phase 2
            // Include Receipt to get GRV number (Source Reference)
            // Use AsNoTracking() to ensure fresh data from database and avoid EF Core change tracking issues
            // Filter out synthetic schools (names starting with "Synthetic School")
            // Prefer devices scanned out by ICT clerks (ScannedOutByUserId != null), but also include devices
            // that were scanned out but don't have ScannedOutByUserId set (legacy data or different workflow)
            var devices = await _phase2Db.Devices
                .AsNoTracking() // Force fresh query, bypass change tracker cache
                .Include(d => d.Receipt) // Include Receipt to get GrvNumber
                .Where(d => 
                    d.ScannedOutAt != null &&                          // scanned out from Phase 2
                    !deviceIdsInBatches.Contains(d.Id) &&              // not already in a batch
                    (d.DispatchStatus == null ||
                     d.DispatchStatus != DispatchDeviceState.Delivered) && // not delivered
                    (d.SchoolName == null || !d.SchoolName.StartsWith("Synthetic School")) // exclude synthetic schools
                )
                .Select(d => new
                {
                    id = d.Id,
                    serial = d.Serial,
                    stage = d.Stage,
                    qaPassed = d.QaPassed,
                    dispatchStatus = d.DispatchStatus,
                    schoolName = d.SchoolName,
                    zone = d.Zone, // Phase2Zone: NewStock or RnR
                    scannedOutAt = d.ScannedOutAt,
                    grvNumber = d.Receipt != null ? d.Receipt.GrvNumber : null // GRV number from receipt
                })
                .OrderByDescending(d => d.scannedOutAt ?? DateTime.MinValue)
                .Take(1000) // Limit to first 1000 devices for performance
                .ToListAsync();
            
            _logger.LogInformation("[DispatchQueue] Found {Count} devices in queue after filtering (excluding synthetic schools, not in batches, not delivered)", devices.Count);
            
            // Log breakdown by ScannedOutByUserId status
            var withUserId = devices.Count(d => !string.IsNullOrEmpty(d.grvNumber) || true); // Just count all for now
            _logger.LogInformation("[DispatchQueue] Devices breakdown - Total: {Total}", devices.Count);
            
            // Log sample of devices found for debugging
            if (devices.Count > 0)
            {
                var sampleDevices = devices.Take(5).Select(d => new { d.id, d.serial, d.scannedOutAt, d.stage, d.schoolName }).ToList();
                _logger.LogInformation("[DispatchQueue] Sample devices in queue: {Devices}", 
                    string.Join(", ", sampleDevices.Select(d => $"ID:{d.id} Serial:{d.serial} ScannedOut:{d.scannedOutAt} Stage:{d.stage} School:{d.schoolName}")));
            }
            else
            {
                _logger.LogWarning("[DispatchQueue] No devices found in queue. Checking why...");
                
                // Diagnostic: check how many devices match each filter criteria
                var diagnosticTotalScannedOut = await _phase2Db.Devices
                    .AsNoTracking()
                    .CountAsync(d => d.ScannedOutAt != null);
                _logger.LogWarning("[DispatchQueue] Diagnostic - Total with ScannedOutAt: {Count}", diagnosticTotalScannedOut);
                
                var inBatches = deviceIdsInBatches.Count;
                _logger.LogWarning("[DispatchQueue] Diagnostic - Devices already in batches: {Count}", inBatches);
                
                var delivered = await _phase2Db.Devices
                    .AsNoTracking()
                    .CountAsync(d => d.ScannedOutAt != null && d.DispatchStatus == DispatchDeviceState.Delivered);
                _logger.LogWarning("[DispatchQueue] Diagnostic - Devices marked as Delivered: {Count}", delivered);
                
                var synthetic = await _phase2Db.Devices
                    .AsNoTracking()
                    .CountAsync(d => d.ScannedOutAt != null && d.SchoolName != null && d.SchoolName.StartsWith("Synthetic School"));
                _logger.LogWarning("[DispatchQueue] Diagnostic - Devices from synthetic schools: {Count}", synthetic);
            }
            
            // Enrich with school district and EMIS code from Schools table
            var enrichedDevices = new List<object>();
            foreach (var device in devices)
            {
                string? district = null;
                string? emisCode = null;

                if (!string.IsNullOrEmpty(device.schoolName))
                {
                    var school = await _coreDb.Schools
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Name == device.schoolName);
                    
                    if (school != null)
                    {
                        district = school.District;
                        emisCode = school.EmisCode;
                    }
                }

                enrichedDevices.Add(new
                {
                    device.id,
                    device.serial,
                    device.stage,
                    device.qaPassed,
                    device.dispatchStatus,
                    device.schoolName,
                    device.zone,
                    device.scannedOutAt,
                    district = district,
                    emisCode = emisCode,
                    // Map Zone to Stock Type: NewStock -> "NEW", RnR -> "RNR"
                    stockType = device.zone == Phase2Zone.NewStock ? "NEW" : 
                               device.zone == Phase2Zone.RnR ? "RNR" : null,
                    // GRV number from receipt (used as Source Reference)
                    grvNumber = device.grvNumber
                });
            }
            
            _logger.LogInformation("[DispatchQueue] Returning {Count} enriched devices to frontend", enrichedDevices.Count);
            return enrichedDevices.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dispatch queue: {Error}", ex.Message);
            return new List<object>();
        }
    }

    /// <summary>
    /// Create a draft batch with devices, extracting school info from the devices
    /// </summary>
    public async Task<DispatchBatch?> CreateDraftBatchWithDevicesAsync(
        List<int> deviceIds, 
        string stockType, 
        string? sourceReference, 
        string userId)
    {
        if (deviceIds == null || deviceIds.Count == 0)
            throw new ArgumentException("At least one device ID is required", nameof(deviceIds));

        // 1. Get devices and verify they exist
        var devices = await _phase2Db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ToListAsync();

        if (devices.Count != deviceIds.Count)
            throw new InvalidOperationException("Some devices were not found");

        // 2. Extract school information - all devices must belong to the same school
        var schoolNames = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.SchoolName))
            .Select(d => d.SchoolName!)
            .Distinct()
            .ToList();

        if (schoolNames.Count == 0)
            return null; // No school information found
        
        if (schoolNames.Count > 1)
            throw new InvalidOperationException("Devices belong to different schools. All devices must belong to the same school.");
        
        var schoolName = schoolNames.First();

        // 3. Get school info from database
        string? district = null;
        string? emisCode = null;

            var school = await _coreDb.Schools
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == schoolName);

            if (school != null)
            {
                district = school.District;
                emisCode = school.EmisCode;
        }
        
        var now = DateTimeOffset.UtcNow;

        // 4. Create batch (NOT saving yet)
        var batch = new DispatchBatch
        {
            // BatchId will be a Guid (default) – EF will use it directly
            Status = BatchStatus.Draft,
            SchoolName = schoolName,
            District = district,
            EmisCode = emisCode,
            StockType = stockType,
            SourceReference = sourceReference,
            CreatedByUserId = userId,
            CreatedAt = now,
            UpdatedAt = now,

            // Initialize booleans so DB doesn't get nulls
            AuditPassed = false,
            EnRoute = false,
            SchoolSigned = false,
            DebriefCompleted = false,
            HasExceptions = false
        };

        _phase3Db.DispatchBatches.Add(batch);

        // 5. Build BatchDevice rows EXPLICITLY and add via DbSet
        var batchDevices = new List<BatchDevice>();

        foreach (var device in devices)
        {
            var batchDevice = new BatchDevice
            {
                // PK is Guid (BatchDeviceId) – default constructor already sets Guid.NewGuid()
                BatchId = batch.BatchId,
                DeviceId = device.Id,
                Serial = device.Serial ?? string.Empty,
                AddedByUserId = userId,
                AddedAt = now,
                ScannedInAudit = false,
                // Model / Condition can be filled later if needed
            };
            
            batchDevices.Add(batchDevice);
            
            // Update Phase 2 device dispatch status
            device.DispatchStatus = DispatchDeviceState.Batch_Assigned;
            device.UpdatedAt = DateTime.UtcNow;
        }
        
        _phase3Db.BatchDevices.AddRange(batchDevices);

        // 6. Save both contexts
        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation(
            "Created draft batch {BatchId} with {DeviceCount} devices for school {SchoolName}",
            batch.BatchId, devices.Count, schoolName);
        
        return batch;
    }

    /// <summary>
    /// Add devices to an existing batch
    /// </summary>
    public async Task<(bool success, string message, int addedCount)> AddDevicesToBatchAsync(
        Guid batchId, 
        List<int> deviceIds, 
        string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found", 0);

        if (batch.Status != BatchStatus.Draft)
            return (false, "Can only add devices to draft batches", 0);

        // Get devices
        var devices = await _phase2Db.Devices
            .Where(d => deviceIds.Contains(d.Id))
            .ToListAsync();

        if (devices.Count != deviceIds.Count)
            return (false, "Some devices were not found", 0);

        // Verify school matches
        var existingSchoolName = batch.SchoolName;
        var mismatchedDevices = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.SchoolName) && d.SchoolName != existingSchoolName)
            .ToList();

        if (mismatchedDevices.Any())
            return (false, $"Some devices belong to different schools. Batch is for {existingSchoolName}", 0);

        // Check if devices are already in a batch
        var existingDeviceIds = batch.Devices.Select(bd => bd.DeviceId).ToList();
        var alreadyInBatch = devices.Where(d => existingDeviceIds.Contains(d.Id)).ToList();
        
        if (alreadyInBatch.Any())
            return (false, $"Some devices are already in this batch", 0);

        // Check if devices are in other batches
        var deviceIdsInOtherBatches = await _phase3Db.BatchDevices
            .Where(bd => deviceIds.Contains(bd.DeviceId) && bd.BatchId != batchId && bd.Batch.Status != BatchStatus.Cancelled)
            .Select(bd => bd.DeviceId)
            .ToListAsync();

        if (deviceIdsInOtherBatches.Any())
            return (false, "Some devices are already assigned to other batches", 0);

        // Add devices
        int added = 0;
        foreach (var device in devices)
        {
            if (existingDeviceIds.Contains(device.Id))
                continue;
            
            var batchDevice = new BatchDevice
            {
                BatchId = batchId,
                DeviceId = device.Id,
                Serial = device.Serial,
                AddedByUserId = userId,
                AddedAt = DateTimeOffset.UtcNow,
                // Initialize required boolean field to prevent NULL constraint violations
                ScannedInAudit = false
            };
            
            _phase3Db.BatchDevices.Add(batchDevice);

            // Update device dispatch status
            device.DispatchStatus = DispatchDeviceState.Batch_Assigned;
            device.UpdatedAt = DateTime.UtcNow;
            
            added++;
        }

        batch.UpdatedAt = DateTimeOffset.UtcNow;
        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Added {Count} devices to batch {BatchId}", added, batchId);
        
        return (true, $"Added {added} device(s) to batch", added);
    }

    /// <summary>
    /// Find devices for the batch's GRV (school + SourceReference) and classify them.
    /// - TotalInGrv: all devices on that GRV in Phase 2
    /// - EligibleDevices: can be added to this batch now (in dispatch queue)
    /// - InIctCount: still in ICT (ScannedOutAt == null)
    /// </summary>
    public async Task<MatchingDevicesResult> GetMatchingDevicesForBatchAsync(Guid batchId)
    {
        var result = new MatchingDevicesResult();

        var batch = await _phase3Db.DispatchBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
        {
            _logger.LogWarning("GetMatchingDevicesForBatchAsync: Batch {BatchId} not found", batchId);
            return result;
        }

        if (string.IsNullOrWhiteSpace(batch.SchoolName) || string.IsNullOrWhiteSpace(batch.SourceReference))
        {
            _logger.LogWarning(
                "GetMatchingDevicesForBatchAsync: Batch {BatchId} missing SchoolName or SourceReference",
                batchId
            );
            return result;
        }

        // All device IDs already in any active (non-cancelled) batch
        var deviceIdsInBatches = await _phase3Db.BatchDevices
            .Where(bd => bd.Batch.Status != BatchStatus.Cancelled)
            .Select(bd => bd.DeviceId)
            .Distinct()
            .ToListAsync();

        // All devices for this GRV in Phase 2 (regardless of ScannedOutAt)
        var allDevicesForGrv = await _phase2Db.Devices
            .Include(d => d.Receipt)
            .Where(d =>
                d.SchoolName == batch.SchoolName &&
                d.Receipt != null &&
                d.Receipt.GrvNumber == batch.SourceReference
            )
            .ToListAsync();

        result.TotalInGrv = allDevicesForGrv.Count;
        result.InIctCount = allDevicesForGrv.Count(d => d.ScannedOutAt == null);

        // Eligible to add to this batch:
        // - scanned out
        // - not in another active batch
        // - not delivered
        var eligible = allDevicesForGrv
            .Where(d =>
                d.ScannedOutAt != null &&
                !deviceIdsInBatches.Contains(d.Id) &&
                (d.DispatchStatus == null || d.DispatchStatus != DispatchDeviceState.Delivered)
            )
            .Select(d => new MatchingDeviceDto
            {
                Id = d.Id,
                Serial = d.Serial
            })
            .ToList();

        result.EligibleDevices = eligible;
        result.EligibleCount = eligible.Count;

        _logger.LogInformation(
            "GetMatchingDevicesForBatchAsync: Batch {BatchId} GRV summary - Total={Total}, Eligible={Eligible}, InICT={InIct}",
            batchId,
            result.TotalInGrv,
            result.EligibleCount,
            result.InIctCount
        );

        return result;
    }

    /// <summary>
    /// Cancel a batch and return devices to dispatch queue
    /// </summary>
    public async Task<(bool success, string message)> CancelBatchAsync(Guid batchId, string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found");

        // Only Draft batches can be cancelled (no POD generated yet)
        if (batch.Status != BatchStatus.Draft)
            return (false, "Only draft batches (no POD generated) can be cancelled.");

        // Reset device dispatch statuses back to queue
        foreach (var bd in batch.Devices)
        {
            var device = await _phase2Db.Devices.FindAsync(bd.DeviceId);
            if (device != null)
            {
                device.DispatchStatus = DispatchDeviceState.Dispatch_Queue;
                device.UpdatedAt = DateTime.UtcNow;
            }
        }

        batch.Status = BatchStatus.Cancelled;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Batch {BatchId} cancelled by {UserId}", batchId, userId);

        return (true, "Batch cancelled and devices returned to dispatch queue");
    }

    /// <summary>
    /// Remove a device from a batch
    /// </summary>
    public async Task<(bool success, string message)> RemoveDeviceFromBatchAsync(
        Guid batchId, 
        int deviceId, 
        string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.Draft)
            return (false, "Can only remove devices from draft batches");

        var batchDevice = batch.Devices.FirstOrDefault(bd => bd.DeviceId == deviceId);
        if (batchDevice == null)
            return (false, "Device not found in batch");

        _phase3Db.BatchDevices.Remove(batchDevice);

        // Update device dispatch status back to queue
        var device = await _phase2Db.Devices.FindAsync(deviceId);
        if (device != null)
        {
            device.DispatchStatus = DispatchDeviceState.Dispatch_Queue;
            device.UpdatedAt = DateTime.UtcNow;
        }

        batch.UpdatedAt = DateTimeOffset.UtcNow;
        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Removed device {DeviceId} from batch {BatchId}", deviceId, batchId);
        
        return (true, "Device removed from batch");
    }

    /// <summary>
    /// Update batch details
    /// </summary>
    public async Task<(bool success, string message)> UpdateBatchDetailsAsync(
        Guid batchId, 
        string? district, 
        string? emisCode,
        string? tripReference,
        string? driverName,
        string? driverUserId,
        string? vehicleReg)
    {
        var batch = await _phase3Db.DispatchBatches.FindAsync(batchId);
        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.Draft)
            return (false, "Can only update details for draft batches");

        batch.District = district ?? batch.District;
        batch.EmisCode = emisCode ?? batch.EmisCode;
        batch.TripReference = tripReference ?? batch.TripReference;
        batch.DriverName = driverName ?? batch.DriverName;
        batch.DriverUserId = driverUserId ?? batch.DriverUserId;
        batch.VehicleReg = vehicleReg ?? batch.VehicleReg;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Updated details for batch {BatchId}", batchId);
        
        return (true, "Batch details updated");
    }

    /// <summary>
    /// Lock batch and generate POD number
    /// </summary>
    public async Task<(bool success, string message)> LockBatchAsync(Guid batchId, string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.Draft)
            return (false, "Can only lock draft batches");

        if (batch.Devices.Count == 0)
            return (false, "Cannot lock batch with no devices");

        // Generate POD number
        var now = DateTime.UtcNow;
        var yearPrefix = $"POD-{now:yyyy}-";
        var dailyCount = await _phase3Db.DispatchBatches
            .Where(b => b.PODNumber != null && b.PODNumber.StartsWith(yearPrefix))
            .CountAsync();
        
        var podNumber = $"POD-{now:yyyy}-{(dailyCount + 1):D3}";
        var deliveryNoteNumber = $"DN-{now:yyyy}-{(dailyCount + 1):D3}";
        
        // Update batch
        batch.Status = BatchStatus.Locked;
        batch.PODNumber = podNumber;
        batch.DeliveryNoteNumber = deliveryNoteNumber;
        batch.LockedAt = DateTimeOffset.UtcNow;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        // Update device statuses
        foreach (var batchDevice in batch.Devices)
        {
            var device = await _phase2Db.Devices.FindAsync(batchDevice.DeviceId);
            if (device != null)
        {
            device.DispatchStatus = DispatchDeviceState.Ready_For_Audit;
            device.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Locked batch {BatchId} with POD number {PODNumber}", batchId, podNumber);
        
        return (true, $"Batch locked. POD number: {podNumber}");
    }

    /// <summary>
    /// Perform loading audit scan
    /// </summary>
    public async Task<(bool success, string message, object? auditResult)> PerformLoadingAuditAsync(
        Guid batchId, 
        List<string> scannedSerials, 
        string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found", null);

        // Check if batch is locked - either by status or by having a POD number (which is only set when locked)
        bool isLocked = batch.Status == BatchStatus.Locked || batch.Status == BatchStatus.AuditInProgress;
        bool hasPODNumber = !string.IsNullOrWhiteSpace(batch.PODNumber);
        
        if (!isLocked && !hasPODNumber)
        {
            _logger.LogWarning(
                "Attempted to perform audit on batch {BatchId} with status {Status} (expected Locked=1 or AuditInProgress=2). PODNumber: {PODNumber}",
                batchId, batch.Status, batch.PODNumber ?? "null");
            return (false, $"Batch must be locked before performing audit. Current status: {batch.Status} ({(int)batch.Status})", null);
        }

        // If batch has POD number but status is wrong, fix the status
        if (hasPODNumber && batch.Status == BatchStatus.Draft)
        {
            _logger.LogWarning(
                "Batch {BatchId} has PODNumber {PODNumber} but status is Draft. Correcting status to Locked.",
                batchId, batch.PODNumber);
            batch.Status = BatchStatus.Locked;
            batch.UpdatedAt = DateTimeOffset.UtcNow;
            await _phase3Db.SaveChangesAsync();
        }

        var expectedSerials = batch.Devices.Select(bd => bd.Serial).ToList();
        var scannedSet = scannedSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedSet = expectedSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        var missing = expectedSerials.Where(s => !scannedSet.Contains(s)).ToList();
        var extra = scannedSerials.Where(s => !expectedSet.Contains(s)).ToList();
        var duplicates = scannedSerials
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        bool auditPassed = missing.Count == 0 && extra.Count == 0 && duplicates.Count == 0;

        // Create audit scan record
        var auditScan = new LoadingAuditScan
        {
            BatchId = batchId,
            ScannedSerials = JsonSerializer.Serialize(scannedSerials),
            ExpectedCount = expectedSerials.Count,
            ScannedCount = scannedSerials.Count,
            AuditPassed = auditPassed,
            MismatchDetails = auditPassed ? null : JsonSerializer.Serialize(new
            {
                missing = missing,
                extra = extra,
                duplicate = duplicates
            }),
            AuditedByUserId = userId,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _phase3Db.LoadingAuditScans.Add(auditScan);

        // Update batch status
        if (auditPassed)
        {
            batch.Status = BatchStatus.AuditPassed;
            batch.AuditPassed = true;
            batch.AuditCompletedAt = DateTimeOffset.UtcNow;
            batch.AuditCompletedByUserId = userId;

            // Update device statuses
            foreach (var batchDevice in batch.Devices)
            {
                if (scannedSet.Contains(batchDevice.Serial, StringComparer.OrdinalIgnoreCase))
            {
                batchDevice.ScannedInAudit = true;
                batchDevice.ScannedAt = DateTimeOffset.UtcNow;
                batchDevice.ScannedByUserId = userId;
                    
                    var device = await _phase2Db.Devices.FindAsync(batchDevice.DeviceId);
                    if (device != null)
            {
                device.DispatchStatus = DispatchDeviceState.Transport_Ready;
                device.UpdatedAt = DateTime.UtcNow;
            }
                }
            }
        }
        else
        {
            batch.Status = BatchStatus.AuditFailed;
            batch.AuditPassed = false;
        }

        batch.UpdatedAt = DateTimeOffset.UtcNow;
        
        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        var result = new
        {
            auditPassed = auditPassed,
            expectedCount = expectedSerials.Count,
            scannedCount = scannedSerials.Count,
            missing = missing,
            extra = extra,
            duplicates = duplicates
        };

        _logger.LogInformation("Loading audit for batch {BatchId}: {Result}", batchId, auditPassed ? "PASSED" : "FAILED");
        
        return (true, auditPassed ? "Audit passed" : "Audit failed - mismatches detected", result);
    }

    /// <summary>
    /// Mark batch as en route
    /// </summary>
    public async Task<(bool success, string message)> MarkEnRouteAsync(Guid batchId, string userId)
    {
        var batch = await _phase3Db.DispatchBatches
            .Include(b => b.Devices)
            .FirstOrDefaultAsync(b => b.BatchId == batchId);

        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.AuditPassed)
            return (false, "Batch must pass audit before marking en route");

        batch.Status = BatchStatus.EnRoute;
        batch.EnRoute = true;
        batch.EnRouteAt = DateTimeOffset.UtcNow;
        batch.EnRouteByUserId = userId;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        // Update device statuses
        foreach (var batchDevice in batch.Devices)
        {
            var device = await _phase2Db.Devices.FindAsync(batchDevice.DeviceId);
            if (device != null)
        {
            device.DispatchStatus = DispatchDeviceState.En_Route;
            device.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Marked batch {BatchId} as en route", batchId);
        
        return (true, "Batch marked as en route");
    }

    /// <summary>
    /// Mark batch as delivered/arrived
    /// </summary>
    public async Task<(bool success, string message)> MarkDeliveredAsync(Guid batchId, string userId)
    {
        var batch = await _phase3Db.DispatchBatches.FindAsync(batchId);
        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.EnRoute)
            return (false, "Batch must be en route before marking as delivered");

        batch.Status = BatchStatus.Delivered;
        batch.ArrivedAt = DateTimeOffset.UtcNow;
        batch.ArrivedByUserId = userId;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        // Update device statuses
        var batchDevices = await _phase3Db.BatchDevices
            .Where(bd => bd.BatchId == batchId)
            .ToListAsync();
        
        foreach (var batchDevice in batchDevices)
        {
            var device = await _phase2Db.Devices.FindAsync(batchDevice.DeviceId);
            if (device != null)
            {
                device.DispatchStatus = DispatchDeviceState.Delivered;
                device.UpdatedAt = DateTime.UtcNow;
            }
        }
        
        await _phase2Db.SaveChangesAsync();
        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Marked batch {BatchId} as delivered", batchId);
        
        return (true, "Batch marked as delivered");
    }

    /// <summary>
    /// Complete debrief for delivered batch
    /// </summary>
    public async Task<(bool success, string message)> CompleteDebriefAsync(
        Guid batchId, 
        bool schoolSigned,
        string? schoolSignatoryName,
        string? debriefNotes,
        bool hasExceptions,
        string? exceptionNotes,
        string userId)
    {
        var batch = await _phase3Db.DispatchBatches.FindAsync(batchId);
        if (batch == null)
            return (false, "Batch not found");

        if (batch.Status != BatchStatus.Delivered)
            return (false, "Batch must be delivered before completing debrief");

        batch.DebriefCompleted = true;
        batch.DebriefCompletedAt = DateTimeOffset.UtcNow;
        batch.DebriefCompletedByUserId = userId;
        batch.DebriefNotes = debriefNotes;
        batch.SchoolSigned = schoolSigned;
        batch.SchoolSignatoryName = schoolSignatoryName;
        if (schoolSigned)
            batch.SchoolSignedAt = DateTimeOffset.UtcNow;
        batch.HasExceptions = hasExceptions;
        batch.ExceptionNotes = exceptionNotes;
        batch.Status = BatchStatus.Completed;
        batch.CompletedAt = DateTimeOffset.UtcNow;
        batch.UpdatedAt = DateTimeOffset.UtcNow;

        await _phase3Db.SaveChangesAsync();

        _logger.LogInformation("Completed debrief for batch {BatchId}", batchId);
        
        return (true, "Debrief completed successfully");
    }
}
